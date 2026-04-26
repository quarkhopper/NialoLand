"""
PiShock link watcher.

This is an alternative to the Railway-based shock button.
Instead of your viewers visiting a Railway server, they visit a PiShock-hosted link
directly (pishock.com/go/<code>). When they activate it, this watcher detects the
activation by polling the PiShock API and shows OBS overlays.

When would you use this instead of shock-button?
  - You don't want to deploy to Railway
  - You want to use PiShock's built-in viewer experience
  - You want viewers to be able to set their own intensity on the PiShock page

The main `shock-button` command (Railway-based) gives you more control —
you set the intensity ceiling and duration. This watcher just reacts to whatever
PiShock's page allows.

Polling means: we ask the API every few seconds "did anyone activate the link?"
We can't receive a push notification, so we check repeatedly until something happens.
"""

from __future__ import annotations

import sys
import time
import threading
from datetime import datetime

_API_BASE = "https://api.pishock.com"


def _print(msg: str):
    print(f"[{datetime.now().strftime('%H:%M:%S')}] {msg}", flush=True)


def _wait_for_keypress(stop_event: threading.Event):
    try:
        if sys.platform == "win32":
            import msvcrt
            msvcrt.getch()
        else:
            import tty
            import termios
            fd = sys.stdin.fileno()
            old = termios.tcgetattr(fd)
            try:
                tty.setraw(fd)
                sys.stdin.read(1)
            finally:
                termios.tcsetattr(fd, termios.TCSADRAIN, old)
    except Exception:
        pass
    stop_event.set()


def watch_link(
    link_code: str,
    headers: dict,
    obs=None,
    obs_shock_source: str = "Shock Alert",
    poll_interval: float = 2.0,
):
    """
    Poll a PiShock link and show an OBS overlay whenever it is activated.

    link_code   : the GUID code from `link create` (e.g. "3fa85f64-...")
    headers     : PiShock auth headers (reuse from pishock_client.Shocker.headers)
    obs         : optional ObsClient; None = overlays disabled
    poll_interval: seconds between polls (default 2)
    """
    import requests

    _print(f"Fetching link {link_code!r}...")
    try:
        r = requests.get(f"{_API_BASE}/Links/{link_code}", headers=headers)
        r.raise_for_status()
        link_data = r.json()
    except Exception as e:
        _print(f"Failed to fetch link: {e}")
        sys.exit(1)

    link_name = link_data.get("Name", link_code)
    max_duration_ms = link_data.get("MaxDuration", 2000)
    last_used = link_data.get("LastUsed")
    remaining = link_data.get("RemainingActivations")

    _print(
        f"Watching link '{link_name}' — overlay={obs_shock_source!r}  "
        f"activations={'unlimited' if remaining is None else remaining}  "
        f"poll_interval={poll_interval}s"
    )
    _print("Press any key to stop.")

    stop_event = threading.Event()
    listener = threading.Thread(target=_wait_for_keypress, args=(stop_event,), daemon=True)
    listener.start()

    overlay_active = False

    try:
        while not stop_event.is_set():
            time.sleep(poll_interval)
            if stop_event.is_set():
                break

            try:
                r = requests.get(f"{_API_BASE}/Links/{link_code}", headers=headers, timeout=5)
                r.raise_for_status()
                new_data = r.json()
            except Exception as e:
                _print(f"Poll error: {e}")
                continue

            new_last_used = new_data.get("LastUsed")
            new_remaining = new_data.get("RemainingActivations")

            activated = False
            if new_last_used != last_used and new_last_used is not None:
                activated = True
            elif remaining is not None and new_remaining is not None and new_remaining < remaining:
                activated = True

            last_used = new_last_used
            remaining = new_remaining

            if activated:
                _print(f"Link activated! ({link_name})")
                if obs is not None and not overlay_active:
                    obs.show_source(obs_shock_source)
                    overlay_active = True
                    # Schedule hide after max_duration_ms
                    def _hide_after(delay_s: float):
                        time.sleep(delay_s)
                        try:
                            obs.hide_source(obs_shock_source)
                        except Exception:
                            pass
                    t = threading.Thread(
                        target=_hide_after,
                        args=(max_duration_ms / 1000,),
                        daemon=True,
                    )
                    t.start()
                    # Reset flag after duration so rapid re-activations work
                    def _reset_flag(delay_s: float):
                        time.sleep(delay_s)
                        nonlocal overlay_active
                        overlay_active = False
                    threading.Thread(
                        target=_reset_flag,
                        args=(max_duration_ms / 1000,),
                        daemon=True,
                    ).start()

    except KeyboardInterrupt:
        pass

    if obs is not None:
        obs.hide_source(obs_shock_source)
        obs.disconnect()

    _print("Stopped.")
