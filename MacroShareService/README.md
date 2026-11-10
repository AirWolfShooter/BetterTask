# BetterTask Macro Sharing API

This is an optional ASP.NET Core API that stores shareable macro recordings in MongoDB. The desktop app sends macro event data to this API; it never connects directly to MongoDB and never receives database credentials.

## MongoDB setup

1. Create a MongoDB Atlas project and cluster.
2. Create a database user dedicated to this service with the built-in `readWrite` role on only the `bettertask_shares` database. Do not use an Atlas project/API key or an admin user as the runtime database account.
3. Add only the API server's outbound IP address to the Atlas IP access list. Do not expose the database to `0.0.0.0/0`.
4. Copy the Atlas application connection string. Keep it in the host's secret/environment configuration, never in source control or the BetterTask desktop settings.

## Run locally

Install the .NET 10 SDK, then from this directory set a local connection string in the current PowerShell session and run:

```powershell
$env:BETTERTASK_MONGODB_URI = 'mongodb+srv://SERVICE_USER:URL_ENCODED_PASSWORD@YOUR_CLUSTER.mongodb.net/?retryWrites=true&w=majority'
$env:BETTERTASK_MONGODB_DATABASE = 'bettertask_shares'
dotnet run
```

The local API listens at `http://localhost:5080` unless the host provides a `PORT` environment variable.

## Deploy on Render

1. Push this repository to a Git provider that Render can access.
2. In Render, create a **New → Web Service**, connect the repository, and choose **Docker**. Set the Dockerfile path to `MacroShareService/Dockerfile`; leave the Docker build context at the repository root.
3. Add `BETTERTASK_MONGODB_URI` as a secret environment variable and `BETTERTASK_MONGODB_DATABASE` with value `bettertask_shares`.
4. Set health check path to `/health`, deploy, and wait for the service to report healthy. Render will assign an HTTPS `onrender.com` address.
5. In Atlas Network Access, allowlist the outbound IP ranges shown on that Render service's **Connect → Outbound** page. Create a database user restricted to `readWrite` on `bettertask_shares`.
6. Put the resulting Render HTTPS URL in BetterTask's **Settings → General → Macro sharing service**. That URL can later be set as the app's default before publishing BetterTask for everyone.

Render's free web services may sleep after 15 minutes idle and take about a minute to wake. For always-responsive sharing, use a paid service plan. See Render's current [free service limits](https://render.com/docs/free).

Do not commit a real connection string. For local development, use a dedicated MongoDB user and narrow network access. Production must be hosted behind HTTPS; the desktop client rejects non-HTTPS addresses except loopback development URLs.

## Configure BetterTask

The app defaults to `https://bettertask.onrender.com/`. You can change or clear this under **Settings → General → Macro sharing service**. In the Macro Library, select **Share** on a saved macro and send the resulting code to someone. They can choose **Import code** and enter it. Imported macros are saved locally and must be explicitly played or set active.

## Safety and data behavior

- The API accepts only bounded macro JSON (1 MB and 10,000 events); it does not accept scripts or execute macro content. New share codes contain 26 Crockford Base32 characters (130 bits); the API still accepts old 12-character codes for older clients.
- Re-sharing identical macro data reuses its existing code and expiry instead of creating another share. If the share has expired, re-sharing refreshes its expiry to 30 days from that request.
- New codes are generated from a server-keyed HMAC of the macro data; only the SHA-256 hash of a code and a fingerprint of the macro are stored. The HMAC key is derived from the server-only MongoDB URI, so keep that URI stable to keep repeat shares deduplicated.
- Shares expire exactly 30 days after creation (or a re-share after expiry). The app displays the expiry returned by the API in the user's local time. A MongoDB TTL index removes expired share documents after their expiry time; TTL cleanup can be delayed by MongoDB, while downloads are rejected as soon as the expiry passes.
- A sharing code is a bearer secret: anyone who has it can download the macro until expiry. Share it only with people you trust.
- Share creation is limited to 60 requests per observed IP per minute and downloads to 60; a 1,200-request per-minute service-wide ceiling remains in place when the hosting proxy shares an address across clients. The API has no list, update, delete, or database administration endpoint. New clients submit codes in POST bodies so they do not appear in request URLs; the old GET route remains temporarily for compatibility.
- The API refuses non-local MongoDB connections unless TLS is enabled. Atlas `mongodb+srv` connection strings use TLS; for standard `mongodb://` strings, enable `tls=true`.
- On startup it creates only the two indexes on its own `macros` collection. The service contains no database/collection drop operation.
- Keep server logs free of connection strings and avoid logging macro bodies or sharing codes.

For a public deployment, put the API behind a TLS-enabled reverse proxy or a hosting platform that provides HTTPS, configure health checks against `/health`, and store the MongoDB URI in that platform's secret manager.
