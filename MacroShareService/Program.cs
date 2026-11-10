using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

const int maxBodyBytes = 10_000_000;
const int maxEvents = 100_000;
const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

var builder = WebApplication.CreateBuilder(args);
string port = Environment.GetEnvironmentVariable("PORT") ?? "5080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxBodyBytes);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNameCaseInsensitive = true);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("share", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
            AutoReplenishment = true
        }));
});

string mongoUri = Environment.GetEnvironmentVariable("BETTERTASK_MONGODB_URI")
    ?? throw new InvalidOperationException("Set BETTERTASK_MONGODB_URI in the service environment.");
if (!mongoUri.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase)
    && !mongoUri.StartsWith("mongodb://", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("BETTERTASK_MONGODB_URI must be a MongoDB connection string.");

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

    string code;
    string hash;
    for (int attempt = 0; ; attempt++)
    {
        code = CreateCode();
        hash = HashCode(code);
        try
        {
            DateTime expiresAt = DateTime.UtcNow.AddDays(30);
            await collection.InsertOneAsync(new ShareDocument
            {
                Id = ObjectId.GenerateNewId(), CodeHash = hash, Macro = macro,
                CreatedAt = DateTime.UtcNow, ExpiresAt = expiresAt
            }, cancellationToken: context.RequestAborted);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Created($"/api/macros/{code}", new { code = FormatCode(code), expiresAt });
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey && attempt < 3)
        {
            // Retry only an astronomically unlikely code collision.
        }
    }
}).RequireRateLimiting("share");

app.MapGet("/api/macros/{code}", async (string code, HttpContext context) =>
{
    string normalized = new(code.Where(char.IsLetterOrDigit).ToArray());
    if (normalized.Length != 12 || normalized.Any(c => !alphabet.Contains(char.ToUpperInvariant(c))))
        return Results.NotFound();

    string codeHash = HashCode(normalized);
    ShareDocument? saved = await collection.Find(x => x.CodeHash == codeHash)
        .FirstOrDefaultAsync(context.RequestAborted);
    if (saved is null || saved.ExpiresAt <= DateTime.UtcNow) return Results.NotFound();
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(saved.Macro);
}).RequireRateLimiting("share");

app.Run();

static string CreateCode()
{
    byte[] random = RandomNumberGenerator.GetBytes(12);
    return string.Concat(random.Select(value => alphabet[value & 31]));
}

static string FormatCode(string code) => $"{code[..4]}-{code[4..8]}-{code[8..]}";
static string HashCode(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.ToUpperInvariant())));

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
