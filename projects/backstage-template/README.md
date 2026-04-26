# Backstage — Viewer Shock Button for Streamers

> **You do not need to know how to code to use this.** The code is already written. You just need to fill in a few settings and follow the steps below. GitHub Copilot can help you with every step if you get stuck — just open this repo in VS Code and ask it questions.

---

## What does this do?

You run one command on your computer. It gives you a link.

You paste that link in your stream chat. Viewers click it and compete to get control of your shock collar. The first viewer to click gets a slider — they pick the intensity, you've already set the duration, they hit the button. You get shocked. On stream.

That's it.

---

## What you need

- A **PiShock account** and a shocker (the collar, wristband, or whatever you're using)
- **Python 3.11 or newer** installed on your computer ([download here](https://www.python.org/downloads/))
- A **GitHub account** (you're probably already here, so you have one)
- A **Railway account** — Railway is the free cloud server that hosts the viewer page. Sign up at [railway.app](https://railway.app) — the free tier is enough.
- **OBS** if you want overlays that show when a shock is happening (optional — everything works without it)

---

## How it works (plain English)

There are two pieces:

**1. Your computer (the CLI)**
A command-line app you run locally. It connects to PiShock and actually fires the shock. Your PiShock credentials stay on your machine and never go anywhere else.

**2. The cloud server (Railway)**
A tiny website that runs on Railway's servers. It hosts the page your viewers see. When a viewer claims the button, it tells your local app, which then fires the shock. The cloud server never connects to PiShock directly — it's just a middleman.

```
Viewer clicks button → Railway server → your machine → PiShock → shock ⚡
```

---

## Setup

### Step 1 — Fork or copy this repo

Click **Fork** at the top right of this GitHub page, or use the template button if you see one. You want your own copy so you can customise it and deploy it.

### Step 2 — Install Python dependencies

Open a terminal in the folder where you put the code and run:

```bash
pip install -r requirements.txt
```

This downloads the Python libraries the code needs. You only do this once.

### Step 3 — Fill in your settings

There are two files you need to create from the examples in this repo:

**`.env` — your private credentials**

This file holds your PiShock API key and other secrets. It never gets uploaded to GitHub (it's listed in `.gitignore` for that reason).

```bash
# Windows
copy .env.example .env

# Mac/Linux
cp .env.example .env
```

Open `.env` and fill in your values. The comments in the file explain each one.

**`config.toml` — your preferences**

This file controls your shock limits — how strong, how long, what label to show viewers, etc.

```bash
# Windows
copy config.toml.example config.toml

# Mac/Linux
cp config.toml.example config.toml
```

Open `config.toml` and adjust the settings. The comments explain every option.

### Step 4 — Deploy the server to Railway

This is the one slightly technical step. See **[docs/deployment.md](docs/deployment.md)** for a full walkthrough with screenshots described.

Short version: you connect Railway to your GitHub repo, it builds and deploys the server automatically, you give it two secret values, and it gives you a public URL.

After deployment you'll have a URL like `https://your-app.up.railway.app`.

### Step 5 — Add your Railway URL to `.env`

Open your `.env` file and fill in:

```
RAILWAY_URL=https://your-app.up.railway.app
```

### Step 6 — Get your viewer URL

```bash
python cli.py get-url
```

This prints the permanent URL your viewers use. **Paste it in your stream chat once — you never need to change it.** It stays the same across stream sessions.

### Step 7 — Run the shock button

Before each shock session:

```bash
python cli.py shock-button
```

The terminal will print the viewer URL again (as a reminder) and then wait. When a viewer claims it, you'll see a message and the shock fires automatically.

Press any key to cancel if no one claims it.

---

## Adjusting shock strength mid-stream

All the defaults are in `config.toml`. You can also override them on the command line without editing the file:

```bash
python cli.py shock-button --max-intensity 50 --duration 3000 --label "SHOCK ME"
```

- `--max-intensity` — the maximum % a viewer can set (you control the ceiling, they pick within it)
- `--duration` — how many milliseconds the shock lasts (3000 = 3 seconds)
- `--label` — the text shown on the viewer's page

---

## OBS overlays (optional)

If you use OBS, you can set it up so overlays automatically appear when the button is armed, claimed, and fired.

1. In OBS: **Tools → WebSocket Server Settings** → enable it and set a password
2. Add your password and OBS scene name to `config.toml` under `[obs]`
3. Create sources in your OBS scene named exactly:
   - **Shock Armed** — shown while viewers can compete for control
   - **Shock Claimed** — shown when a viewer has grabbed it
   - **Shock Alert** — shown during the actual shock
4. Set all three sources to **hidden by default** — the app will show and hide them automatically

You can rename these — just update the names in `config.toml`.

---

## Troubleshooting

**"RAILWAY_URL not set in .env"** — Open your `.env` file and make sure `RAILWAY_URL=https://...` is filled in.

**"Missing required environment variables: PISHOCK_USERNAME..."** — Open your `.env` file and fill in your PiShock credentials.

**Shock fires but OBS doesn't react** — Check that OBS WebSocket is enabled, the password matches `config.toml`, and the source names match exactly (case-sensitive).

**Railway says "Build failed"** — Make sure the Root Directory in Railway is set to `server` (see deployment.md step 3b).

---

## File overview

You only need to care about these two files:

| File | What it is |
|---|---|
| `.env` | Your private credentials — PiShock login, API key, etc. |
| `config.toml` | Your preferences — shock limits, label, OBS settings |

The rest of the files are the app itself — you don't need to edit them unless you want to customise behaviour.

| File | What it does |
|---|---|
| `cli.py` | The commands you run (`shock-button`, `get-url`, etc.) |
| `pishock_client.py` | Talks to the PiShock API to fire shocks |
| `obs_client.py` | Talks to OBS to show/hide overlay sources |
| `link_watcher.py` | Watches for viewer activations on PiShock links |
| `server/main.py` | The cloud relay — runs on Railway, no credentials in it |

---

## Using GitHub Copilot to customise this

This repo is designed to be modified with Copilot's help. Open it in VS Code and ask things like:

- *"Help me fill in my .env file"*
- *"What should I set max_intensity to for a beginner-friendly stream?"*
- *"Add a --vibrate flag to the shock-button command"*
- *"Why does the server not store my PiShock credentials?"*

The code has comments throughout that explain the why behind decisions, specifically to help you (and Copilot) understand what to change safely.

---

## Quick Start (for the streamer)

### 1. Prerequisites

- Python 3.11+
- A [PiShock](https://pishock.com) account with a shocker
- A [Railway](https://railway.app) account (free tier works)
- OBS with WebSocket v5 enabled (optional — for overlays)

### 2. Get your credentials

| What | Where to find it |
|---|---|
| PiShock username | Your PiShock account login name |
| PiShock API key | pishock.com → Account Settings |
| Share code | PiShock app → tap shocker → SHARE |
| ARM_SECRET | Generate once: `python -c "import secrets; print(secrets.token_urlsafe(32))"` |

### 3. Install dependencies

```bash
pip install -r requirements.txt
```

### 4. Create your config files

```bash
# Credentials — stays on your machine
copy .env.example .env
# Edit .env with your real values

# Preferences — stays on your machine  
copy config.toml.example config.toml
# Edit config.toml with your shock settings and OBS connection
```

### 5. Deploy the server to Railway

See [docs/deployment.md](docs/deployment.md) for step-by-step instructions.

Once deployed, add these environment variables in Railway:
- `ARM_SECRET` — same value you put in `.env`
- `CHANNEL_TOKEN` — run `python -c "import secrets; print(secrets.token_urlsafe(32))"` for a stable viewer URL

### 6. Run the shock button

```bash
python cli.py shock-button
```

This arms the button, prints the viewer URL (paste it in chat once), and waits.

---

## Commands

```bash
python cli.py shock-button              # Arm the shock button (main command)
python cli.py shock-button --max-intensity 50 --duration 3000

python cli.py rotate-token              # Get a new permanent viewer URL
python cli.py get-url                   # Print the current viewer URL

python cli.py link create               # Create a one-time PiShock link
python cli.py link watch --code <guid>  # Watch a link for activations
python cli.py link create-and-watch     # Create + watch in one step
```

---

## OBS Setup (optional)

Enable WebSocket: OBS → Tools → WebSocket Server Settings → Enable

Create these sources in your scene (hidden by default):
- **Shock Armed** — shown while the button is live and waiting
- **Shock Claimed** — shown while viewer has grabbed control
- **Shock Alert** — shown during the actual shock

You can rename these — just match them in `config.toml` under `[obs]`.

---

## File Overview

| File | Purpose |
|---|---|
| `cli.py` | All CLI commands — run this |
| `pishock_client.py` | PiShock REST API wrapper |
| `obs_client.py` | OBS WebSocket wrapper (degrades gracefully if OBS is off) |
| `link_watcher.py` | Polling loop for PiShock links |
| `worker.py` | Reserved for future background workers |
| `server/main.py` | Railway FastAPI server — the relay |
| `.env` | **Your secrets** — never commit this |
| `config.toml` | **Your preferences** — never commit this |
| `.env.example` | Template for `.env` |
| `config.toml.example` | Template for `config.toml` |
