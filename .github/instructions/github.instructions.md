---
description: "Use when working with GitHub repos, git commits, GitHub Actions CI, pushing code, or tagging releases. NialoLand is both the dev workspace and the public educational showcase."
---

# GitHub Integration Guidelines

## Repository

[`quarkhopper/NialoLand`](https://github.com/quarkhopper/NialoLand) is the single public repo — both the live coding workspace and the educational showcase for stream viewers.

All stream projects live under `sessions/`:
```
NialoLand/
  sessions/
    2026-04-25-chat-overlay/   ← YYYY-MM-DD-project-name
      src/
      README.md
```

Code here should be **commented for new developers** — explain the *why*, not just the *what*.

## Commit Conventions

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: add chat overlay widget
fix: correct timer reset on stream end
chore: update dependencies
docs: add educational comments to session
refactor: extract config loading
```

Stream commit messages can be casual but type-prefixed — shows a clean graph on camera.

## Branching Strategy

```
main              ← stable, always clean
live/session-name ← active stream work branch
```

Create a `live/` branch at the start of each stream:
```powershell
git checkout -b live/$(Get-Date -Format "yyyy-MM-dd")-session-name
```

Merge to `main` after the stream. Tag if releasing a deployable app.

## Tagging Releases

```bash
git tag -a v1.0.0 -m "First release of chat-overlay"
git push origin v1.0.0
```

## GitHub Actions CI

Use for non-trivial projects. Minimal workflow template:

```yaml
# .github/workflows/ci.yml
name: CI

on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.x'
      - run: dotnet build sessions/your-app/
```

For Python:
```yaml
      - uses: actions/setup-python@v5
        with:
          python-version: '3.12'
      - run: pip install -r sessions/your-app/requirements.txt
```

## .gitignore Essentials

Always include:
```gitignore
# Secrets
.env
*.env.local

# Build output
bin/
obj/
dist/
__pycache__/
*.pyc
.venv/
node_modules/

# IDE
.vscode/settings.json
.idea/
```

Do **not** ignore `railway.toml`, `.github/`, or `README.md`.
