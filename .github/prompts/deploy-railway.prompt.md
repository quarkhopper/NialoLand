---
description: "Deploy the current session project to Railway. Checks railway.toml, health endpoint, secrets hygiene, then runs railway up."
argument-hint: "Session folder name, e.g. '2026-04-25-chat-overlay', or leave blank for current directory"
agent: "agent"
tools: [read, edit, execute, search]
---

Deploy the specified NialoLand session to Railway: ${input}

## Pre-Flight Checks

Run these checks before deploying. Fix any issues found.

### 1. Locate the session
- If `${input}` is provided, `cd` to `sessions/{input}` from the NialoLand root
- Otherwise use the current directory
- Confirm a project exists (look for `*.csproj`, `package.json`, `main.py`, etc.)

### 2. Check `railway.toml`
- Verify it exists at the session root
- Verify `startCommand` is set
- Verify `healthcheckPath = "/health"` is present for web services

If missing, create an appropriate `railway.toml` based on the detected language. See the Railway deployment instructions.

### 3. Check for a `/health` endpoint
- Search the source code for a `/health` route
- If missing and this is a web service, add one:
  - C#: `app.MapGet("/health", () => Results.Ok(new { status = "ok" }));`
  - Python/FastAPI: `@app.get("/health") def health(): return {"status": "ok"}`
  - TypeScript/Fastify: `app.get('/health', async () => ({ status: 'ok' }));`

### 4. Secrets hygiene scan
- Search source files for hardcoded credentials
- Patterns to flag: `sk-[a-zA-Z0-9]{20,}`, `password\s*=\s*"`, `token\s*=\s*"`
- If found, flag them and stop — do NOT deploy with hardcoded secrets
- This is a public repo — any committed secret is immediately exposed

### 5. Check PORT binding
- Ensure the app reads `PORT` from environment variables
- Ensure it binds to `0.0.0.0` not `localhost`

## Deploy

Once all checks pass:

```bash
railway up
```

Watch output for errors. If deployment fails, show the relevant error and suggest a fix.

## Post-Deploy

```bash
railway open   # open in browser to verify it's live
railway logs   # show recent logs
```

Report the live URL and confirm the `/health` endpoint responds.
