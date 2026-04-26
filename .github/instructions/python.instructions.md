---
description: "Use when writing Python scripts, AI integrations, data tools, or quick prototypes. Covers Python 3.12+, rich library, virtual envs, and stream-friendly patterns."
applyTo: ["**/*.py", "**/requirements.txt", "**/pyproject.toml"]
---

# Python Guidelines for NialoLand

## Backstage Project (../Backstage/)

Backstage uses **Click** for its CLI (not `rich`) and splits config into two files:

| File | Contains | Gitignored? |
|---|---|---|
| `.env` | Secrets: PiShock credentials, ARM_SECRET, Railway URL | Yes |
| `config.toml` | Non-secret settings: shock limits, OBS connection, label text | Yes |

**Config access pattern in Backstage:**
```python
# Secrets — always from environment (loaded via dotenv)
username = os.environ["PISHOCK_USERNAME"]   # raises if missing — intentional

# Non-secret config — from config.toml with sensible defaults
cfg = _config.get("shock_button", {})
max_intensity = cfg.get("max_intensity", 100)
```

**Adding a new user setup:**
- Copy `.env.example` → `.env`, fill in their PiShock credentials + ARM_SECRET
- Copy `config.toml` defaults, adjust their shock limits and OBS source names
- Deploy `server/` to Railway with their own `CHANNEL_TOKEN` + `ARM_SECRET` env vars
- Each user is completely isolated — no shared state

**When modifying Backstage:** preserve the `cli.py` ↔ `server/` split. Server holds no credentials. CLI fires PiShock directly after polling the server for a claim.

## Environment Setup

Always use a virtual environment per project:

```bash
python -m venv .venv
.venv\Scripts\activate        # Windows
source .venv/bin/activate     # Mac/Linux
pip install -r requirements.txt
```

For Railway deployments, include `runtime.txt`:
```
python-3.12.0
```

## Terminal Output

Always use **rich** for output — it makes streams look great:

```bash
pip install rich
```

Patterns to use:
```python
from rich.console import Console
from rich.progress import track
from rich.table import Table
from rich import print as rprint

console = Console()
console.print("[green]Done![/green]")
console.print("[bold red]Error:[/bold red] something went wrong")

# Progress bars
for item in track(items, description="Processing..."):
    process(item)

# Live updates
from rich.live import Live
from rich.spinner import Spinner
```

## Project Structure

Keep it flat and obvious — no need for packages unless the app is large:

```
my-app/
  main.py          ← entry point
  requirements.txt
  .env.example     ← document env vars, never commit .env
  README.md
  railway.toml
```

## Configuration & Secrets

```python
import os
from dotenv import load_dotenv  # pip install python-dotenv

load_dotenv()  # loads .env locally; Railway injects env vars directly

API_KEY = os.environ["MY_API_KEY"]  # raises KeyError if missing — good
```

Never hardcode credentials. Use `.env` locally, Railway env vars in production.

## Error Handling

Surface errors visibly:

```python
from rich.console import Console
console = Console()

try:
    result = call_api()
except requests.HTTPError as e:
    console.print(f"[red]API error {e.response.status_code}:[/red] {e}")
    raise SystemExit(1)
```

## Async Code

Use `asyncio` for I/O-heavy work:

```python
import asyncio
import httpx  # pip install httpx — async-native, prefer over requests for async

async def fetch(url: str) -> dict:
    async with httpx.AsyncClient() as client:
        response = await client.get(url)
        response.raise_for_status()
        return response.json()

asyncio.run(main())
```

## Key Packages

| Package | Purpose |
|---------|---------|
| `rich` | Terminal output / UI |
| `httpx` | HTTP client (async-native) |
| `python-dotenv` | Load `.env` files locally |
| `typer` | CLI argument parsing |
| `openai` | OpenAI / Azure OpenAI |
| `anthropic` | Claude API |
| `fastapi` + `uvicorn` | Web APIs |

## Style

- Type hints on function signatures — helps Copilot suggest correct code
- f-strings over `.format()` or `%`
- `if __name__ == "__main__":` guard in every entry point
- Keep functions short — readable on camera
