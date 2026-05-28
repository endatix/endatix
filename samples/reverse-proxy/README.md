# Endatix Reverse Proxy Sample

This sample runs an Nginx reverse proxy that exposes Endatix Hub and API through one local origin:

- Hub: `http://localhost:8080/app/`
- API: `http://localhost:8080/api/`

The proxy-only compose file assumes Hub and API are already running on the host machine:

- Hub on `http://localhost:3000/app/`
- API on `http://localhost:5000/api/`

## Hub Environment

Use this shape for local testing:

```env
NEXT_PUBLIC_BASE_PATH=/app
ENDATIX_BASE_URL=http://localhost:8080
ENDATIX_API_PREFIX=/api
AUTH_URL=http://localhost:8080/app/api/auth
AUTH_TRUST_HOST=true
```

## API Configuration

Enable reverse-proxy hosting for the API:

```json
{
  "Endatix": {
    "Hosting": {
      "ReverseProxy": {
        "Enabled": true,
        "TrustAllProxiesInDevelopment": true
      }
    }
  }
}
```

With this setting, `app.UseEndatix()` processes forwarded headers before Endatix API middleware and disables app-level HSTS/HTTPS redirection by default. Let the public edge own TLS redirects and HSTS when TLS terminates there.

Do not add `UsePathBase("/api")` for this sample. The proxy forwards `/api/*` to the API, and Endatix API routes already use the `api` route prefix.

## Run

Start Hub and API first, then run the proxy:

```bash
docker compose up
```

Open `http://localhost:8080/app/`.

## Forwarded Headers

The sample sends these headers to preserve public request details:

| Header | Purpose |
| :--- | :--- |
| `Host` | Keeps redirects and generated links on `localhost:8080`. |
| `X-Forwarded-For` | Preserves the original client IP chain. |
| `X-Forwarded-Host` | Preserves the external host and port. |
| `X-Forwarded-Proto` | Preserves the external scheme. |
| `X-Forwarded-Prefix` | Documents the external path prefix for `/app` and `/api`. |
