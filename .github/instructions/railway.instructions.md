---
description: "Use when deploying to Railway, setting up railway.toml, configuring Procfile, Railway environment variables, or troubleshooting Railway deployments."
---

# Railway Deployment Guidelines

## Required Files

Every deployable project needs a `railway.toml` at its project root:

### C# / .NET
```toml
[build]
builder = "nixpacks"

[deploy]
startCommand = "dotnet MyApp.dll"
healthcheckPath = "/health"
healthcheckTimeout = 30
```

### Python
```toml
[build]
builder = "nixpacks"

[deploy]
startCommand = "python main.py"
# or for FastAPI:
# startCommand = "uvicorn main:app --host 0.0.0.0 --port $PORT"
```

### Node.js / TypeScript
```toml
[build]
builder = "nixpacks"
buildCommand = "npm ci && npm run build"

[deploy]
startCommand = "node dist/index.js"
healthcheckPath = "/health"
healthcheckTimeout = 30
```

## Environment Variables

- Set via Railway dashboard → project → Variables tab
- Never commit `.env` files — only commit `.env.example`
- Railway injects `PORT` automatically — always use it:
  - C#: `var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";`
  - Python: `port = int(os.environ.get("PORT", 8080))`
  - Node: `const port = Number(process.env.PORT ?? 3000)`

## Health Check Endpoints

All web services must have `/health`:

```csharp
// C# Minimal API
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
```

```python
# FastAPI
@app.get("/health")
def health(): return {"status": "ok"}
```

```typescript
// Fastify
app.get('/health', async () => ({ status: 'ok' }));
```

## Deployment Flow

```bash
# Connect a project to Railway (first time)
railway link

# Deploy from CLI
railway up

# View logs
railway logs

# Open in browser
railway open

# Set env vars from CLI
railway variables set MY_API_KEY=value
```

## railway.toml Reference

```toml
[build]
builder = "nixpacks"         # auto-detect language
buildCommand = "npm run build"  # override build command

[deploy]
startCommand = "node server.js"
restartPolicyType = "on-failure"
restartPolicyMaxRetries = 3
healthcheckPath = "/health"
healthcheckTimeout = 30      # seconds
```

## Common Gotchas

- **Port binding**: Must bind to `0.0.0.0`, not `localhost` — Railway routes external traffic to `$PORT`
- **Missing health check**: Railway will mark deployment unhealthy without it (for web services)
- **Build artifacts**: Ensure `dist/` or `bin/Release/` is not in `.gitignore` if Railway builds from source
- **nixpacks auto-detect**: Railway detects language from `package.json`, `*.csproj`, `requirements.txt` — keep one per project folder
