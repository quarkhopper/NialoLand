"""
Backstage shock server — deployed on Railway.

This file runs in the cloud (on Railway), not on your machine.
You don't need to edit it to use Backstage. Read this if you're curious about
how it works, or if you want to customise the viewer page.

--- WHAT THIS SERVER DOES ---

This is a relay — a middleman between your viewers and your local machine.

Here's the flow:
  1. You run `python cli.py shock-button` on your computer
  2. Your CLI sends a POST /arm request to this server: "I'm ready, here are the limits"
  3. This server creates a "slot" (a session) and starts waiting
  4. Viewers visit the permanent URL (e.g. https://yourapp.up.railway.app/shock/abc123)
  5. The first viewer to click "Claim control!" grabs the slot
  6. They set their intensity and click "ZAP THE STREAMER!"
  7. This server marks the slot as "claimed" and stores their chosen intensity
  8. Your local CLI is polling /arm/status every 2 seconds — it sees "claimed"
  9. CLI reads the viewer's intensity, fires PiShock locally, and it's over

--- WHY THE SERVER HAS NO PISHOCK CREDENTIALS ---

This server runs on Railway's computers, which means its code is accessible to Railway.
If we put PiShock credentials here, Railway (and anyone who compromises Railway) could
fire shocks at any time. By keeping credentials only on your local machine, you stay in
control. The server can only tell your machine "someone wants to shock you" — it can
never do it directly.

--- ONE SLOT AT A TIME ---

Only one shock session is active at a time. When you run shock-button again, the
previous slot is replaced. State is in-memory, so a Railway restart clears everything.
"""

import os
import uuid
import asyncio
import json
from pathlib import Path
from datetime import datetime, timezone, timedelta
from fastapi import FastAPI, HTTPException
from fastapi.responses import HTMLResponse, StreamingResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel
from dotenv import load_dotenv

load_dotenv()

ARM_SECRET = os.environ["ARM_SECRET"]

app = FastAPI(title="Backstage Shock Server")

_static_dir = Path(__file__).parent / "static"
_static_dir.mkdir(exist_ok=True)
app.mount("/static", StaticFiles(directory=_static_dir), name="static")

# Single-slot state — only one armed session at a time
_slot: dict | None = None
_lock = asyncio.Lock()

# SSE broadcast queues — one per connected viewer
_sse_queues: list[asyncio.Queue] = []

# Channel token — controls the permanent viewer URL: /shock/<channel_token>
#
# This is the last part of the URL you paste in your stream chat.
# If CHANNEL_TOKEN is set as a Railway environment variable, the URL stays the same
# across server restarts (Railway redeploys, etc.).
# If it's not set, Railway generates a random one on every startup — which means
# your viewer URL changes every time, which is annoying.
# Solution: set CHANNEL_TOKEN in Railway env vars once, then forget about it.
_channel_token: str = os.environ.get("CHANNEL_TOKEN") or str(uuid.uuid4())
if not os.environ.get("CHANNEL_TOKEN"):
    print(
        "WARNING: CHANNEL_TOKEN not set — generated ephemeral token. "
        "Run `python cli.py get-url` to retrieve it, or set CHANNEL_TOKEN in Railway.",
        flush=True,
    )


# ---------------------------------------------------------------------------
# Request/response models
# ---------------------------------------------------------------------------

class ArmRequest(BaseModel):
    secret: str
    min_intensity: int = 1          # viewer slider lower bound
    max_intensity: int = 100        # viewer slider upper bound
    duration: int = 2000            # milliseconds — set by streamer, not shown to viewer
    label: str = "Shock Button"
    timeout_seconds: int | None = None  # optional: slot auto-expires after this many seconds
    image_url: str | None = None        # optional: image shown on the viewer session page


class RotateTokenRequest(BaseModel):
    secret: str


class ClaimRequest(BaseModel):
    intensity: int


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _public_base() -> str:
    domain = os.environ.get("RAILWAY_PUBLIC_DOMAIN", "")
    if domain:
        return f"https://{domain}"
    return os.environ.get("PUBLIC_URL", "http://localhost:8000").rstrip("/")


def _check_expiry(slot: dict) -> bool:
    """Return True if the slot has expired. Mutates slot state in-place.
    Must be called while holding _lock. Does NOT broadcast — caller must do so."""
    if slot.get("expires_at") is None:
        return False
    if datetime.now(timezone.utc) > slot["expires_at"]:
        slot["state"] = "expired"
        return True
    return False


async def _broadcast(event: str, data: dict) -> None:
    """Fan out a SSE event to all connected viewer queues."""
    payload = json.dumps({"event": event, **data})
    for q in list(_sse_queues):
        q.put_nowait(payload)


def _slot_initial_event(slot: dict | None) -> str:
    """Return the JSON string for the first SSE event sent to a new viewer."""
    if slot is None:
        return json.dumps({"event": "idle"})
    state = slot["state"]
    if state == "armed":
        return json.dumps({
            "event": "armed",
            "label": slot["label"],
            "min_intensity": slot["min_intensity"],
            "max_intensity": slot["max_intensity"],
            "image_url": slot.get("image_url"),
        })
    if state == "grabbed":
        return json.dumps({"event": "grabbed"})
    if state == "claimed":
        return json.dumps({"event": "claimed"})
    if state == "cancelled":
        return json.dumps({"event": "cancelled"})
    if state == "expired":
        return json.dumps({"event": "expired"})
    return json.dumps({"event": "idle"})


# ---------------------------------------------------------------------------
# Endpoints
# ---------------------------------------------------------------------------

@app.get("/health")
def health():
    return {"status": "ok"}


@app.post("/arm")
async def arm(req: ArmRequest):
    """Arm the shock slot. Creates/replaces the current slot. Called by the local CLI."""
    global _slot
    if req.secret != ARM_SECRET:
        raise HTTPException(403, "Invalid secret")
    if not (1 <= req.min_intensity <= req.max_intensity <= 100):
        raise HTTPException(400, "min_intensity/max_intensity must be 1-100 with min <= max")
    token = str(uuid.uuid4())
    expires_at = None
    if req.timeout_seconds and req.timeout_seconds > 0:
        expires_at = datetime.now(timezone.utc) + timedelta(seconds=req.timeout_seconds)
    async with _lock:
        _slot = {
            "token": token,
            "min_intensity": req.min_intensity,
            "max_intensity": req.max_intensity,
            "duration": req.duration,
            "label": req.label,
            "state": "armed",
            "created_at": datetime.now(timezone.utc).isoformat(),
            "expires_at": expires_at,
            "image_url": req.image_url or None,
            "claimed_at": None,
            "claimed_intensity": None,
        }
        entry_token = _channel_token
    await _broadcast("armed", {
        "label": req.label,
        "min_intensity": req.min_intensity,
        "max_intensity": req.max_intensity,
        "image_url": req.image_url or None,
    })
    return {"url": f"{_public_base()}/shock/{entry_token}"}


@app.get("/arm/status")
async def arm_status(secret: str):
    """Poll endpoint for the local CLI. Requires ARM_SECRET as query param."""
    global _slot
    if secret != ARM_SECRET:
        raise HTTPException(403, "Invalid secret")
    should_broadcast_expired = False
    async with _lock:
        if _slot is None:
            raise HTTPException(404, "No active slot")
        slot = _slot
        if _check_expiry(slot):
            should_broadcast_expired = True
    if should_broadcast_expired:
        await _broadcast("expired", {})
    async with _lock:
        slot = _slot
        if slot is None:
            raise HTTPException(404, "No active slot")
        return {
            "state":             slot["state"],
            "claimed_intensity": slot["claimed_intensity"],
            "duration":          slot["duration"],
            "claimed_at":        slot["claimed_at"],
        }


@app.delete("/arm")
async def cancel_arm(secret: str):
    """Cancel the current armed slot. Called by local CLI on keypress/stop."""
    global _slot
    if secret != ARM_SECRET:
        raise HTTPException(403, "Invalid secret")
    cancelled = False
    async with _lock:
        if _slot and _slot["state"] in ("armed", "grabbed"):
            _slot["state"] = "cancelled"
            cancelled = True
    if cancelled:
        await _broadcast("cancelled", {})
    return {"ok": True}


@app.post("/channel-token/rotate")
async def rotate_channel_token(req: RotateTokenRequest):
    """Rotate the channel token. Old viewer entry URLs immediately stop working."""
    global _channel_token
    if req.secret != ARM_SECRET:
        raise HTTPException(403, "Invalid secret")
    new_token = str(uuid.uuid4())
    async with _lock:
        _channel_token = new_token
    url = f"{_public_base()}/shock/{new_token}"
    return {
        "url": url,
        "reminder": f"Set CHANNEL_TOKEN={new_token} in Railway env vars to persist across restarts.",
    }


@app.get("/channel-token/current")
async def current_channel_token(secret: str):
    """Return the current viewer entry URL. Useful after a server restart."""
    if secret != ARM_SECRET:
        raise HTTPException(403, "Invalid secret")
    async with _lock:
        token = _channel_token
    return {"url": f"{_public_base()}/shock/{token}"}


@app.get("/shock/{channel_token}/events")
async def shock_events(channel_token: str):
    """
    SSE stream for the viewer page. One queue per connected client.
    Immediately emits current slot state so late-joiners are in sync.
    """
    should_broadcast_expired = False
    async with _lock:
        if channel_token != _channel_token:
            raise HTTPException(404, "Unknown channel")
        slot = _slot
        if slot is not None and _check_expiry(slot):
            should_broadcast_expired = True
    if should_broadcast_expired:
        await _broadcast("expired", {})
        async with _lock:
            slot = _slot

    initial = _slot_initial_event(slot)
    queue: asyncio.Queue = asyncio.Queue()
    _sse_queues.append(queue)

    async def event_stream():
        try:
            yield f"data: {initial}\n\n"
            while True:
                payload = await queue.get()
                yield f"data: {payload}\n\n"
        except asyncio.CancelledError:
            pass
        finally:
            try:
                _sse_queues.remove(queue)
            except ValueError:
                pass

    return StreamingResponse(
        event_stream(),
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "X-Accel-Buffering": "no",
            "Connection": "keep-alive",
        },
    )


@app.post("/shock/{channel_token}/grab")
async def grab(channel_token: str):
    """
    Viewer claims first-mover rights. Atomically transitions armed -> grabbed and
    broadcasts to all other viewers so they see controls disabled immediately.
    Only the grabber may then call /claim.
    """
    global _slot
    async with _lock:
        if channel_token != _channel_token:
            raise HTTPException(404, "Unknown channel")
        slot = _slot
        if slot is None:
            return {"won": False, "message": "No control is active right now."}
        _check_expiry(slot)
        state = slot["state"]
        if state != "armed":
            if state == "grabbed":
                return {"won": False, "message": "Too slow! someone else grabbed it! \u26a1"}
            if state == "claimed":
                return {"won": False, "message": "Too slow! someone else got it! \u26a1"}
            if state == "cancelled":
                return {"won": False, "message": "The streamer cancelled. Stay tuned!"}
            if state == "expired":
                return {"won": False, "message": "This control has expired."}
            return {"won": False, "message": "No control is active right now."}
        slot["state"] = "grabbed"

    await _broadcast("grabbed", {})
    return {"won": True}


@app.get("/shock/{channel_token}", response_class=HTMLResponse)
async def shock_page(channel_token: str):
    """
    Permanent viewer page. Serves the unified page for all states.
    State is driven by SSE after load.
    """
    async with _lock:
        if channel_token != _channel_token:
            return HTMLResponse(_page_gone("That link is no longer valid."), status_code=404)
        slot = _slot
        image_url = slot.get("image_url") if slot else None
    return HTMLResponse(_page_unified(channel_token, image_url))


@app.post("/shock/{channel_token}/claim")
async def claim(channel_token: str, body: ClaimRequest):
    """
    Viewer submits their chosen intensity and attempts to claim the slot.
    Race-safe — only the first caller wins. Actual shock is fired by the local CLI.
    """
    global _slot
    won = False
    duration_s = 0.0
    intensity = body.intensity
    async with _lock:
        if channel_token != _channel_token:
            raise HTTPException(404, "Unknown channel")
        slot = _slot
        if slot is None:
            return {"won": False, "message": "No control is active right now."}
        _check_expiry(slot)
        state = slot["state"]
        if state != "grabbed":
            if state == "armed":
                return {"won": False, "message": "Press 'Try to get control!' first."}
            if state == "claimed":
                return {"won": False, "message": "Too slow! someone else got it! \u26a1"}
            if state == "cancelled":
                return {"won": False, "message": "The streamer cancelled. Stay tuned!"}
            if state == "expired":
                return {"won": False, "message": "This control has expired."}
            return {"won": False, "message": "No control is active right now."}
        intensity = max(slot["min_intensity"], min(slot["max_intensity"], body.intensity))
        slot["state"] = "claimed"
        slot["claimed_intensity"] = intensity
        slot["claimed_at"] = datetime.now(timezone.utc).isoformat()
        duration_s = slot["duration"] / 1000
        won = True

    await _broadcast("claimed", {})
    return {
        "won": True,
        "message": f"\U0001f4a5 Get ready! The streamer is about to be shocked for {duration_s:.1f}s at {intensity}% intensity.",
    }


# ---------------------------------------------------------------------------
# HTML helpers
# ---------------------------------------------------------------------------

def _page_unified(channel_token: str, image_url: str | None) -> str:
    img_html = f'<img class="streamer-img" src="{image_url}" alt="Streamer">' if image_url else ""
    channel_token_js = json.dumps(channel_token)
    return f"""<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Backstage</title>
  <style>
    *, *::before, *::after {{ box-sizing: border-box; margin: 0; padding: 0; }}
    body {{
      font-family: 'Segoe UI', sans-serif;
      background: #0d0d0d;
      color: #f0f0f0;
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
      padding: 2rem;
    }}
    h1 {{ font-size: clamp(1.8rem, 5vw, 3rem); }}
    .meta {{ color: #aaa; font-size: 1rem; }}
    .page-layout {{
      display: flex;
      flex-direction: row;
      align-items: stretch;
      border-radius: 20px;
      overflow: hidden;
      box-shadow: 0 8px 48px #0009;
      flex-wrap: wrap;
      max-width: 700px;
      width: 100%;
    }}
    .streamer-img {{
      width: 200px;
      min-height: 220px;
      object-fit: cover;
      display: block;
      flex-shrink: 0;
    }}
    .controls-col {{
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 1.5rem;
      padding: 2.5rem 2rem;
      text-align: center;
      background: #111;
      flex: 1;
      min-width: 260px;
    }}
    .slider-row {{
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
      width: min(320px, 90vw);
    }}
    .slider-label {{ font-size: 1.1rem; }}
    .slider-label span {{ color: #e63946; font-weight: bold; font-size: 1.3rem; }}
    input[type=range] {{
      width: 100%;
      accent-color: #e63946;
      cursor: pointer;
    }}
    input[type=range]:disabled {{ cursor: default; opacity: 0.4; }}
    button {{
      font-size: clamp(1.2rem, 4vw, 2rem);
      padding: 1rem 3.5rem;
      border: none;
      border-radius: 14px;
      background: #e63946;
      color: #fff;
      cursor: pointer;
      transition: background 0.15s, transform 0.1s;
      box-shadow: 0 4px 24px #e6394644;
    }}
    button:hover:not(:disabled) {{ background: #c1121f; transform: scale(1.04); }}
    button:active:not(:disabled) {{ transform: scale(0.97); }}
    button:disabled {{ background: #444; cursor: default; box-shadow: none; }}
    #msg {{ font-size: 1.2rem; min-height: 1.5rem; color: #ffd166; }}
  </style>
</head>
<body>
  <div class="page-layout">
    {img_html}
    <div class="controls-col">
      <h1 id="label">&#x26a1; Backstage</h1>
      <p class="meta" id="meta">Get ready for the control to arm</p>
      <div class="slider-row" id="slider-row" style="display:none">
        <p class="slider-label">Intensity: <span id="val">50</span>%</p>
        <input type="range" id="slider" min="1" max="100" value="50"
               oninput="document.getElementById('val').textContent = this.value">
        <p id="range-hint" style="color:#666;font-size:0.85rem">1% &mdash; 100%</p>
      </div>
      <button id="btn" disabled onclick="grab()">Get ready&hellip;</button>
      <p id="msg"></p>
    </div>
  </div>
  <script>
    const CHANNEL = {channel_token_js};
    let _armed = false;
    let _grabbed = false;
    let _sliderMin = 1, _sliderMax = 100;

    function setArmed(data) {{
      _armed = true;
      _sliderMin = data.min_intensity != null ? data.min_intensity : 1;
      _sliderMax = data.max_intensity != null ? data.max_intensity : 100;
      document.getElementById('label').textContent = '\u26a1 ' + (data.label || 'Backstage');
      document.getElementById('meta').textContent = 'First to click wins \u2014 be ready!';
      document.getElementById('slider-row').style.display = 'none';
      const btn = document.getElementById('btn');
      btn.textContent = 'Claim control!';
      btn.onclick = grab;
      btn.disabled = false;
      _grabbed = false;
      document.getElementById('msg').textContent = '';
    }}

    function setDisabled(msg, meta) {{
      _armed = false;
      _grabbed = false;
      document.getElementById('slider-row').style.display = 'none';
      const btn = document.getElementById('btn');
      btn.disabled = true;
      btn.onclick = grab;
      btn.textContent = 'Get ready\u2026';
      if (meta) document.getElementById('meta').textContent = meta;
      document.getElementById('msg').textContent = msg;
    }}

    let _es = null;
    function connect() {{
      if (_es) _es.close();
      _es = new EventSource('/shock/' + CHANNEL + '/events');
      _es.onmessage = function(e) {{
        let d;
        try {{ d = JSON.parse(e.data); }} catch(_) {{ return; }}
        const ev = d.event;
        if (ev === 'armed') {{
          setArmed(d);
        }} else if (ev === 'grabbed') {{
          if (_armed) setDisabled('Too slow! someone else grabbed it! \u26a1', 'The control was grabbed');
        }} else if (ev === 'claimed') {{
          if (_armed || _grabbed) setDisabled('Too slow! someone else got it! \u26a1', 'The control was claimed');
        }} else if (ev === 'cancelled') {{
          setDisabled('The streamer cancelled. Stay tuned!', 'Get ready for the control to arm');
        }} else if (ev === 'expired') {{
          setDisabled('This control has expired.', 'Get ready for the control to arm');
        }} else {{
          setDisabled('Get ready for the control to arm', 'Get ready for the control to arm');
        }}
      }};
      _es.onerror = function() {{
        document.getElementById('msg').textContent = 'Reconnecting\u2026';
      }};
    }}
    connect();

    async function grab() {{
      if (!_armed || _grabbed) return;
      _armed = false;
      _grabbed = true;
      const btn = document.getElementById('btn');
      btn.disabled = true;
      btn.textContent = 'Grabbing\u2026';
      try {{
        const resp = await fetch('/shock/' + CHANNEL + '/grab', {{
          method: 'POST',
          headers: {{'Content-Type': 'application/json'}},
        }});
        const data = await resp.json();
        if (data.won) {{
          const mid = Math.floor((_sliderMin + _sliderMax) / 2);
          const slider = document.getElementById('slider');
          slider.min = _sliderMin;
          slider.max = _sliderMax;
          slider.value = mid;
          document.getElementById('val').textContent = mid;
          document.getElementById('range-hint').textContent = _sliderMin + '% \u2014 ' + _sliderMax + '%';
          document.getElementById('slider-row').style.display = '';
          btn.textContent = 'ZAP THE STREAMER!';
          btn.onclick = claim;
          btn.disabled = false;
          document.getElementById('meta').textContent = 'You\u2019ve got it \u2014 pick your intensity and confirm!';
        }} else {{
          setDisabled(data.message || 'Too slow!', 'The control was grabbed');
        }}
      }} catch (e) {{
        setDisabled('Network error \u2014 try again.', 'Get ready for the control to arm');
      }}
    }}

    async function claim() {{
      if (!_grabbed) return;
      _grabbed = false;
      const btn = document.getElementById('btn');
      const msg = document.getElementById('msg');
      const intensity = parseInt(document.getElementById('slider').value);
      btn.disabled = true;
      btn.textContent = 'Get ready\u2026';
      msg.textContent = 'Sending\u2026';
      try {{
        const resp = await fetch('/shock/' + CHANNEL + '/claim', {{
          method: 'POST',
          headers: {{'Content-Type': 'application/json'}},
          body: JSON.stringify({{intensity}}),
        }});
        const data = await resp.json();
        msg.textContent = data.message || 'Done!';
        if (data.won) {{
          document.getElementById('slider-row').style.display = 'none';
          document.getElementById('meta').textContent = 'You got it!';
        }}
      }} catch (e) {{
        msg.textContent = 'Network error \u2014 try again.';
      }}
    }}
  </script>
</body>
</html>"""



def _page_gone(message: str) -> str:
    return f"""<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Backstage</title>
  <style>
    body {{
      font-family: 'Segoe UI', sans-serif;
      background: #0d0d0d;
      color: #f0f0f0;
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
      font-size: 1.5rem;
      text-align: center;
      padding: 2rem;
    }}
  </style>
</head>
<body><p>{message}</p></body>
</html>"""
