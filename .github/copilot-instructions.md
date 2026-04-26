# NialoLand — Copilot Instructions

NialoLand is a vibe-coding environment and public educational showcase for YouTube streaming sessions. The goal is to build entertaining, working applications **fast** — things that look great on stream, demonstrate real concepts, and ship live — with code commented well enough for new developers to learn from.

## Philosophy

- **Ship it fast** — working code over perfect code; demos over architecture diagrams
- **Keep it visible** — prefer console output, rich terminal UI, and visual feedback over silent background processes
- **Entertain and educate** — every feature should be explainable in 30 seconds on camera
- **One session, one project** — each app is self-contained under `sessions/`

## Tech Stack

| Language | Use For |
|----------|---------|
| C# (.NET 8+) | Desktop tools, APIs, console apps, games |
| Python 3.12+ | Scripts, data stuff, AI integrations, quick prototypes |
| TypeScript / JavaScript | Web frontends, Node APIs, browser experiments |

**Deployment platform**: Railway (primary), with GitHub as the source of truth.

## Backstage (../Backstage/)

Backstage is a **separate repo** in the workspace — Python-based PiShock automation for shock-content streaming. It lives alongside NialoLand but is not part of it.

**Architecture:**
- `cli.py` — Click-based CLI (`shock-button`, `rotate-token`, `get-url`, `link *`)
- `pishock_client.py` — PiShock REST API wrapper; credentials from `.env`
- `obs_client.py` — OBS WebSocket client; degrades gracefully if OBS not running
- `server/main.py` — FastAPI relay deployed on Railway; holds **no PiShock credentials**
- `config.toml` — non-secret per-user config (shock defaults, OBS connection); **gitignored**
- `.env` — secrets (PISHOCK_USERNAME, PISHOCK_API_KEY, PISHOCK_SHARE_CODE, ARM_SECRET); **gitignored**

**Key principle:** PiShock credentials never leave the streamer's machine. The Railway server is a stateless relay — it only brokers the viewer ↔ CLI handshake.

**Multi-user customization pattern:**
Each streamer needs their own:
1. `.env` — their PiShock credentials + Railway ARM_SECRET
2. `config.toml` — their shock limits, OBS source names, label text
3. Railway deployment — their own instance with `CHANNEL_TOKEN` + `ARM_SECRET` env vars

Customization touchpoints are intentionally limited to these two files.

**Kitting up a new user (the standard workflow):**
1. Give them a copy of `projects/backstage-template/` (or have them fork the NialoLand repo and use that subfolder)
2. Collect their customization variables — see `projects/backstage-template/.github/copilot-instructions.md` for the full list
3. Generate their `.env` from `projects/backstage-template/.env.example` with real values
4. Generate their `config.toml` from `projects/backstage-template/config.toml.example` with their shock prefs and OBS settings
5. Walk them through `docs/deployment.md` to get the Railway server live
6. Run `python cli.py get-url` and give them the permanent viewer URL to pin in chat

The Python source files in the template do NOT change per user. Only `.env` and `config.toml` are user-specific.

## Project Structure

The workspace has two top-level areas:

```
NialoLand/
  tools/                          ← NialoTools: stream automation toolkit (C# .NET)
    NialoTools.sln
    NialoTools/
      Program.cs                  ← CLI entry point (Spectre.Console.Cli)
      Commands/                   ← One file per command; grow this as needed
        StreamStatusCommand.cs    ← ntools stream status
        GhSummaryCommand.cs       ← ntools gh summary
  projects/
    backstage-template/           ← Deployable template; kit up a new streamer from here
      cli.py                      ← All CLI commands (shock-button, rotate-token, get-url, link *)
      pishock_client.py           ← PiShock REST API wrapper
      obs_client.py               ← OBS WebSocket wrapper (degrades gracefully)
      link_watcher.py             ← PiShock link polling loop
      server/
        main.py                   ← FastAPI relay for Railway (no PiShock credentials)
        railway.toml
      .env.example                ← Template for streamer's credentials
      config.toml.example         ← Template for streamer's preferences
      docs/deployment.md          ← Step-by-step Railway setup
      .github/copilot-instructions.md ← Copilot instructions scoped to this template
  sessions/
    2026-04-25-chat-overlay/      ← YYYY-MM-DD-project-name
      src/
      README.md
      railway.toml                ← if deploying to Railway
```

### NialoTools (tools/)

A .NET 9 CLI (`ntools`) that reduces repetitive token usage and provides stream-specific automation. Safe for public consumption — no secrets in source.

**Running commands:**
```powershell
# During development
dotnet run --project tools/NialoTools -- <command> [options]

# After publish (faster)
dotnet publish tools/NialoTools -c Release -o .tools/ntools
.tools/ntools/ntools <command>
```

**Available commands:**
| Command | What it does |
|---|---|
| `ntools stream status` | Branch, recent commits, uncommitted changes |
| `ntools gh summary` | Open PRs and issues for the NialoLand repo |

**Adding a new command:**
1. Create `tools/NialoTools/Commands/MyNewCommand.cs`
2. Add `config.AddCommand<MyNewCommand>("name")` (or into a branch) in `Program.cs`
3. `dotnet build tools/NialoTools` to verify

All commands read secrets from env vars — never from CLI args or config files.

## Naming

- Sessions: `YYYY-MM-DD-kebab-name` (e.g., `2026-04-25-chat-overlay`)
- Git branches: `main` for stable, `live/YYYY-MM-DD-name` for active stream work
- Commits: Conventional Commits format (`feat:`, `fix:`, `chore:`, `docs:`)

## Output Style

- Use `Spectre.Console` for C# terminal UIs
- Use `rich` library for Python terminal output
- Use color, progress bars, and live updates whenever practical — it looks great on stream

## Educational Comments

Since this is a public repo, code should be commented for new developers:
- Explain the **why** behind non-obvious decisions
- Call out security patterns (env vars, input validation)
- Note library choices and what alternatives exist

## Projects Folder — Beginner-First Audience

Code under `projects/` is designed to be handed to complete beginners — people who
don't know Python, don't know what Railway is, and have never opened a terminal.

**README files in `projects/` must be fully self-contained landing pages:**
- Explain what the project does in one plain-English sentence before anything else
- Define every tool involved: what Railway is, what `.env` files are, what a server does
- Write setup steps for someone who has never deployed anything
- Position GitHub Copilot as the customisation assistant for readers who want to change things

**Code comments in `projects/` should answer "what is this and why does it exist?":**
- Don't just say `# load config` — say what config.toml is and why it's separate from .env
- Explain every non-obvious pattern: why credentials go in env vars, why the server holds no secrets, why threading is used
- Errors should be self-diagnosing: when something is missing, say where to find it and what it should look like

## Error Handling

- Surface errors loudly with context — no silent swallows
- On stream: a visible error with a helpful message is better than a crash with a stack trace

## GitHub

- Always initialize sessions with a `README.md`
- Commit often during streams — it shows progress in the git graph
- Tag project releases with semantic versions (`v1.0.0`)

## Railway Deployment

- Every deployable app must have a `railway.toml` at its session root
- Use environment variables for all secrets — never hardcode credentials
- Health check endpoints (`/health`) for all web services

## What NOT to Do

- Don't over-engineer — no DI containers, CQRS, or event sourcing unless the project calls for it
- Don't add tests unless explicitly asked — stream time is precious
- Don't install unnecessary packages — keep dependency counts low and obvious
- Don't hardcode secrets — ever
