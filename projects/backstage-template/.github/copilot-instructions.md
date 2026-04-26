# Backstage Template — Copilot Instructions
#
# This is a template for setting up a Backstage PiShock automation instance
# for a new streamer. The live, maintained source of truth is in ../Backstage/.
#
# HOW TO USE THIS TEMPLATE
# ========================
# When a new streamer needs a Backstage setup, Copilot should:
#   1. Ask for their customization values (see CUSTOMIZATION VARIABLES below)
#   2. Generate their `.env` from `.env.example` with real values filled in
#   3. Generate their `config.toml` from `config.toml.example` with their values
#   4. Walk them through Railway deployment (see docs/deployment.md)
#
# That's it. The Python source files do NOT need to change per user.
#
# CUSTOMIZATION VARIABLES (collect these before generating files)
# ===============================================================
# From PiShock account/app:
#   PISHOCK_USERNAME    — their PiShock login username
#   PISHOCK_API_KEY     — their PiShock API key (account settings page)
#   PISHOCK_SHARE_CODE  — share code for their specific shocker (tap shocker → SHARE in app)
#
# From Railway deployment:
#   RAILWAY_URL         — the public URL of their deployed server (e.g. https://myapp.railway.app)
#   ARM_SECRET          — a random secret they generate once:
#                         python -c "import secrets; print(secrets.token_urlsafe(32))"
#
# From OBS (optional — skip if they don't use OBS overlays):
#   obs.password        — OBS WebSocket password (Tools → WebSocket Server Settings)
#   obs.scene           — scene name (default: "Main")
#   obs source names    — they need sources named "Shock Armed", "Shock Claimed", "Shock Alert"
#                         (or whatever they set in config.toml)
#
# Shock preferences (streamer's choice):
#   max_intensity       — max % the viewer can choose (suggest starting at 30)
#   duration            — shock duration in milliseconds (suggest 3000–5000)
#   label               — text on the viewer's button page
#
# ARCHITECTURE REMINDER
# =====================
# The Railway server (server/) holds NO PiShock credentials.
# It is a stateless relay — it brokers the viewer ↔ CLI handshake.
# Credentials only exist in .env on the streamer's machine.
# Each streamer gets their own Railway deployment (separate ARM_SECRET + CHANNEL_TOKEN).
#
# SYNC WITH UPSTREAM
# ==================
# If the live Backstage repo (../Backstage/) has been updated since this template
# was last synced, Copilot should copy changed .py files from there to here.
# Config templates (.env.example, config.toml.example) are maintained here.
