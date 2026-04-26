---
description: "Wrap up a stream session. Does an educational comment pass for new developers, writes the session README, commits, and merges the live branch to main."
argument-hint: "Session folder name, e.g. '2026-04-25-chat-overlay', or leave blank for the current session"
agent: "agent"
tools: [read, edit, execute, search]
---

Wrap up the NialoLand stream session: ${input}

## Overview

This prompt finalizes a session so it's clean and educational for viewers browsing the public repo. Since NialoLand IS the workspace, there's no cross-repo copying — just a comment pass, README update, and final push.

## Steps

### 1. Identify the session
- Parse `${input}` for the session folder name
- If blank, look for the current `live/` branch to determine the session: `git branch --show-current`
- Confirm `sessions/{session-name}/` exists

### 2. Educational comment pass

Read through source files and add comments that teach new developers. Focus on:
- **Why** a design decision was made (not just what the code does)
- **Security patterns** — env vars for secrets, input validation
- **Language-specific patterns** new devs might not know
- **Library choices** — why Spectre.Console, why Fastify, why rich

Example style:
```csharp
// CancellationTokenSource lets us signal the app to stop gracefully.
// Without this, pressing Ctrl+C would kill the process mid-operation.
var cts = new CancellationTokenSource();
```

```python
# track() wraps any iterable and shows a progress bar automatically.
# It's from the 'rich' library — much nicer than a manual print loop.
for item in track(items, description="Processing..."):
```

### 3. Write or update the session README

The `sessions/{session-name}/README.md` should have:
- **What we built** — one paragraph, casual tone matching the stream
- **Key concepts** — bullet list of things a new dev should take away
- **How to run it** — minimal setup instructions (install, configure env, run)
- **Stack** — languages and key packages used

### 4. Final commit and push

```powershell
git add .
git commit -m "docs: session wrap — add educational comments and README"
git push origin {current-branch}
```

### 5. Merge to main

```powershell
git checkout main
git merge {live-branch} --no-ff -m "feat: merge {session-name} from stream"
git push origin main
```

### 6. Confirm

Report:
- The GitHub URL for the session: `https://github.com/quarkhopper/NialoLand/tree/main/sessions/{session-name}`
- What key concepts are documented for viewers
