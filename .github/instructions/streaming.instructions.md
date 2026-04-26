---
description: "Use when planning or running a live coding stream, structuring a vibe-coding session, picking what to build, or wrapping up session code for the NialoLand educational showcase."
---

# Streaming & Vibe-Coding Session Guidelines

## Session Rhythm

A good stream has a predictable flow that keeps viewers engaged:

1. **Hook (0–2 min)** — State what we're building and why it's cool. No setup, no preamble.
2. **Scaffold (2–10 min)** — `dotnet new`, `npm create`, or `mkdir` + first file. Get something running.
3. **Core Loop (10–45 min)** — Build the interesting thing. Commit every meaningful chunk.
4. **Polish Pass (45–55 min)** — Colors, output, a README, a banner. Make it look good on camera.
5. **Deploy & Share (55–60 min)** — Push to Railway or GitHub. Tag it. Show the NialoLand URL.

## What to Build

Ideal stream projects are:
- **Completable in 60–90 min** — viewers see a full arc
- **Visually interesting** — terminal UIs, web dashboards, chat integrations
- **Teachable** — one or two real concepts that stick
- **Deployable** — ends with something live on the internet

Good archetypes: chat bots, stream overlays, timers/countdowns, API mashups, Discord bots, mini-games, data visualizers, AI wrappers.

## Code Comments for Viewers

Since NialoLand is public, add comments that teach:

```csharp
// Why we use a CancellationToken here: long-running tasks should always
// respect cancellation so the app shuts down cleanly without hanging.
var cts = new CancellationTokenSource();
```

```python
# We store the API key in an env var rather than the code itself.
# If this were in the source, anyone who sees your repo gets your key.
api_key = os.environ["OPENAI_API_KEY"]
```

```typescript
// Fastify uses async route handlers — we always await I/O inside them
// so errors bubble up to Fastify's error handler automatically.
app.get('/data', async (request, reply) => {
```

Comments should answer: **"What would confuse a new developer here?"**

## Projects Folder — Beginner-First Audience

Code under `projects/` is different from session code: it's designed to be handed to
complete beginners — people who don't know Python, don't know what Railway is, and
have never seen a terminal. Apply a higher standard:

- **README = self-contained landing page.** Never assume the reader has context. Explain
  what Railway is. Explain what a `.env` file is. Explain what the cloud server does and
  why it exists. Write it like a YouTube tutorial, not a developer README.
- **Comments explain what things *are*, not just what they do.** Don't just say
  `# load config` — say `# Read config.toml (your preferences file) if it exists`.
- **Every non-obvious pattern needs a why.** Why are credentials in .env and not the code?
  Why does the server hold no PiShock credentials? Why use threading for the keypress
  listener? A beginner asking "why is it done this way?" should find the answer in the
  comments, not by Googling.
- **Errors should be self-diagnosing.** When a credential is missing, say which file it
  should be in and what it should look like. Don't just say `RAILWAY_URL not set`.
- **Position Copilot as the customisation assistant.** In README and comments, tell
  readers they can use "GitHub Copilot" or "AI chat" to help them adjust the code.
  They don't need to understand every line — they need to know where to look and who to ask.

## Stream-Friendly Coding Practices

- **Narrate before you type** — say what you're about to do, then do it
- **Use `AnsiConsole` / `rich` / `chalk`** — colored output reads on camera; `Console.WriteLine` is invisible
- **Commit loudly** — `git add . && git commit -m "feat: first working version"` is a satisfying stream moment
- **Make errors visible** — a red error panel is engaging; a silent crash is confusing
- **Keep functions short** — code should fit in one screen without scrolling

## Starting a New Stream Session

```powershell
# In the NialoLand workspace root
git checkout main
git pull
$date = Get-Date -Format "yyyy-MM-dd"
git checkout -b live/$date-your-app-name
New-Item -ItemType Directory -Path "sessions/$date-your-app-name"
cd sessions/$date-your-app-name
```

Or use the `/new-stream-app` prompt — it handles all of this.

## Ending a Stream Session

1. Final commit: `git commit -m "chore: end of stream cleanup"`
2. Run `/session-wrap` to do the educational comment pass and push
3. If deploying: run `/deploy-railway` first
4. Merge `live/` branch to `main`

## Energy Levels

Keep the pacing tight. If you're stuck for more than 3–4 minutes:
- Say "let me check the docs real quick" — don't go silent
- Use Copilot visibly — it's part of the show
- Simplify — cut a feature to keep momentum

The stream dies when nothing is happening. Ship a smaller version rather than stalling on the ideal one.
