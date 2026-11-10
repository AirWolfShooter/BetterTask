using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

const int maxBodyBytes = 1_000_000;
const int maxEvents = 10_000;
const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

var builder = WebApplication.CreateBuilder(args);
string port = Environment.GetEnvironmentVariable("PORT") ?? "5080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxBodyBytes);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNameCaseInsensitive = true);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Render sits behind a proxy, so RemoteIpAddress can be shared by users.
    // Keep an aggregate ceiling even when the hosting proxy address is shared.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
        RateLimitPartition.GetFixedWindowLimiter("service", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 1200, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("share-create", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("share-download", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
            AutoReplenishment = true
        }));
});

string mongoUri = Environment.GetEnvironmentVariable("BETTERTASK_MONGODB_URI")
    ?? throw new InvalidOperationException("Set BETTERTASK_MONGODB_URI in the service environment.");
if (!mongoUri.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase)
    && !mongoUri.StartsWith("mongodb://", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("BETTERTASK_MONGODB_URI must be a MongoDB connection string.");
MongoUrl mongoUrl = new(mongoUri);
bool localMongo = mongoUrl.Servers.Any() && mongoUrl.Servers.All(server =>
    server.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
    || server.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
    || server.Host.Equals("::1", StringComparison.OrdinalIgnoreCase));
if (!localMongo && mongoUrl.UseTls != true)
    throw new InvalidOperationException("Remote MongoDB connections must use TLS. Enable tls=true in the connection string.");

string databaseName = Environment.GetEnvironmentVariable("BETTERTASK_MONGODB_DATABASE") ?? "bettertask_shares";
var mongo = new MongoClient(mongoUri);
var collection = mongo.GetDatabase(databaseName).GetCollection<ShareDocument>("macros");
var app = builder.Build();

// Startup creates only indexes on this service's collection. It never drops databases or collections.
await collection.Indexes.CreateManyAsync(new[]
{
    new CreateIndexModel<ShareDocument>(Builders<ShareDocument>.IndexKeys.Ascending(x => x.CodeHash),
        new CreateIndexOptions { Unique = true, Name = "code_hash_unique" }),
    new CreateIndexModel<ShareDocument>(Builders<ShareDocument>.IndexKeys.Ascending(x => x.ExpiresAt),
        new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "expires_at_ttl" })
});

app.UseRateLimiter();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/macros", async (MacroDto macro, HttpContext context) =>
{
    if (!IsValid(macro, out string error))
        return Results.BadRequest(new { error });

    string fingerprint = Fingerprint(macro);
    string code = CreateCode(fingerprint, mongoUri);
    string codeHash = HashCode(code);
    DateTime now = DateTime.UtcNow;

    ShareDocument? existing = await collection.Find(x => x.CodeHash == codeHash)
        .FirstOrDefaultAsync(context.RequestAborted);
    if (existing is not null)
    {
        if (existing.Fingerprint != fingerprint)
            return Results.Problem("A share-code collision occurred. Please retry.", statusCode: StatusCodes.Status503ServiceUnavailable);

        if (existing.ExpiresAt <= now)
        {
            DateTime renewedExpiry = now.AddDays(30);
            var filter = Builders<ShareDocument>.Filter.Eq(x => x.CodeHash, codeHash)
                & Builders<ShareDocument>.Filter.Eq(x => x.Fingerprint, fingerprint)
                & Builders<ShareDocument>.Filter.Lte(x => x.ExpiresAt, now);
            var update = Builders<ShareDocument>.Update
                .Set(x => x.CreatedAt, now)
                .Set(x => x.ExpiresAt, renewedExpiry)
                .Set(x => x.Macro, macro);
            existing = await collection.FindOneAndUpdateAsync(filter, update,
                new FindOneAndUpdateOptions<ShareDocument> { ReturnDocument = ReturnDocument.After },
                context.RequestAborted);
            existing ??= await collection.Find(x => x.CodeHash == codeHash)
                .FirstOrDefaultAsync(context.RequestAborted);
        }

        if (existing is not null && existing.ExpiresAt > DateTime.UtcNow)
            return ExistingShareResponse(context, code, existing.ExpiresAt);
    }

    DateTime createdAt = DateTime.UtcNow;
    DateTime expiresAt = createdAt.AddDays(30);
    try
    {
        await collection.InsertOneAsync(new ShareDocument
        {
            Id = ObjectId.GenerateNewId(), CodeHash = codeHash, Fingerprint = fingerprint,
            Macro = macro, CreatedAt = createdAt, ExpiresAt = expiresAt
        }, cancellationToken: context.RequestAborted);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Json(new
        {
            code = FormatCode(code), expiresAt, reused = false
        }, statusCode: StatusCodes.Status201Created);
    }
    catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
    {
        // Concurrent requests for identical macro data converge on the same share code.
        ShareDocument? raced = await collection.Find(x => x.CodeHash == codeHash)
            .FirstOrDefaultAsync(context.RequestAborted);
        if (raced is not null && raced.Fingerprint == fingerprint && raced.ExpiresAt > DateTime.UtcNow)
            return ExistingShareResponse(context, code, raced.ExpiresAt);

        return Results.Problem("Could not create the share. Please retry.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireRateLimiting("share-create");

app.MapPost("/api/macros/download", async (DownloadRequest request, HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-store";
    string? normalized = NormalizeCode(request.Code);
    if (normalized is null)
        return Results.NotFound();

    string codeHash = HashCode(normalized);
    ShareDocument? saved = await collection.Find(x => x.CodeHash == codeHash)
        .FirstOrDefaultAsync(context.RequestAborted);
    if (saved is null || saved.ExpiresAt <= DateTime.UtcNow) return Results.NotFound();
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(saved.Macro);
}).RequireRateLimiting("share-download");

// Compatibility endpoint for clients from releases that predate POST downloads.
// New clients send codes in a bounded JSON body to keep them out of request paths/logs.
app.MapGet("/api/macros/{code}", async (string code, HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-store";
    string? normalized = NormalizeCode(code);
    if (normalized is null || normalized.Length != 12) return Results.NotFound();
    string codeHash = HashCode(normalized);
    ShareDocument? saved = await collection.Find(x => x.CodeHash == codeHash)
        .FirstOrDefaultAsync(context.RequestAborted);
    if (saved is null || saved.ExpiresAt <= DateTime.UtcNow) return Results.NotFound();
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(saved.Macro);
}).RequireRateLimiting("share-download");

app.Run();

static IResult ExistingShareResponse(HttpContext context, string code, DateTime expiresAt)
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(new { code = FormatCode(code), expiresAt, reused = true });
}

static string CreateCode(string fingerprint, string keyMaterial)
{
    byte[] key = SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial + "|BetterTask.ShareCode.v1"));
    byte[] digest = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(fingerprint));
    return string.Concat(Enumerable.Range(0, 26).Select(index =>
    {
        int value = 0;
        for (int bit = 0; bit < 5; bit++)
        {
            int sourceBit = index * 5 + bit;
            value = (value << 1) | ((digest[sourceBit / 8] >> (7 - sourceBit % 8)) & 1);
        }
        return alphabet[value];
    }));
}

static string Fingerprint(MacroDto macro) =>
    Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(macro)));

static string FormatCode(string code) => code.Length == 12
    ? $"{code[..4]}-{code[4..8]}-{code[8..]}"
    : string.Join('-', Enumerable.Range(0, 5).Select(index => code.Substring(index * 5, 5)).Append(code[25..]));
static string HashCode(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.ToUpperInvariant())));
static string? NormalizeCode(string? code)
{
    if (string.IsNullOrWhiteSpace(code)) return null;
    string normalized = new(code.Where(char.IsLetterOrDigit).ToArray());
    if (normalized.Length is not (12 or 26)
        || normalized.Any(c => !alphabet.Contains(char.ToUpperInvariant(c)))) return null;
    return normalized.ToUpperInvariant();
}

static bool IsValid(MacroDto? macro, out string error)
{
    error = "The macro payload is invalid.";
    if (macro is null || string.IsNullOrWhiteSpace(macro.Name) || macro.Name.Length > 100
        || (macro.Version?.Length ?? 0) > 30 || (macro.Created?.Length ?? 0) > 50
        || macro.Events is null || macro.Events.Count is < 1 or > maxEvents)
        return false;

    foreach (MacroEventDto? item in macro.Events)
    {
        if (item is null || item.Time < 0 || item.Type is < 0 or > 64
            || item.KeyCode is < 0 or > 10000 || item.MouseButton is < 0 or > 16
            || item.WheelDelta is < 0 or > 65535 || item.Rotation is < short.MinValue or > short.MaxValue
            || item.ScrollDirection is < 0 or > 8 || item.ScrollType is < 0 or > 8)
            return false;
    }
    return true;
}

sealed class ShareDocument
{
    [BsonId] public ObjectId Id { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public MacroDto Macro { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

sealed class MacroDto
{
    public string Name { get; set; } = string.Empty;
    public string Created { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public List<MacroEventDto> Events { get; set; } = new();
}

sealed class DownloadRequest
{
    public string Code { get; set; } = string.Empty;
}

sealed class MacroEventDto
{
    public long Time { get; set; }
    public int Type { get; set; }
    public short? MouseX { get; set; }
    public short? MouseY { get; set; }
    public int? KeyCode { get; set; }
    public int? MouseButton { get; set; }
    public int? WheelDelta { get; set; }
    public int? Rotation { get; set; }
    public int? ScrollDirection { get; set; }
    public int? ScrollType { get; set; }
}
