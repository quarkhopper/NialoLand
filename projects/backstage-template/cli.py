"""
Backstage CLI — stream control tools.

This is the file you run to control your stream shock sessions.
A CLI (command-line interface) just means you interact with it by typing commands
in a terminal instead of clicking buttons in a window.

All commands follow this pattern:
    python cli.py <command> [options]

Common commands:
    python cli.py shock-button                          # the main one — arm a viewer shock button
    python cli.py shock-button --max-intensity 50       # override intensity cap for this session
    python cli.py get-url                               # print the current viewer URL
    python cli.py rotate-token                          # generate a new viewer URL

Less common (PiShock direct links instead of the Railway relay):
    python cli.py link create --activations 1 --duration 2000 --intensity 30
    python cli.py link watch --code 3fa85f64-5717-4562-b3fc-2c963f66afa6
    python cli.py link create-and-watch --activations 1 --duration 2000 --intensity 30
"""

import os
from pathlib import Path

import click


def _load_config() -> dict:
    """
    Load config.toml if it exists, return empty dict otherwise.

    config.toml is your personal preferences file (shock limits, OBS settings, etc.).
    If it doesn't exist yet, that's fine — the app uses built-in defaults and you can
    override everything on the command line. Copy config.toml.example to config.toml
    to get started.
    """
    path = Path(__file__).parent / "config.toml"
    if not path.exists():
        return {}
    try:
        import tomllib          # Python 3.11+
    except ImportError:
        import tomli as tomllib  # type: ignore[no-redef]
    with open(path, "rb") as f:
        return tomllib.load(f)


_config = _load_config()


# ---------------------------------------------------------------------------
# CLI root
# ---------------------------------------------------------------------------

@click.group()
def cli():
    """Backstage — stream control tools."""


# ---------------------------------------------------------------------------
# link group
# ---------------------------------------------------------------------------

@cli.group("link")
def link():
    """Manage and watch PiShock viewer-triggered links."""


@link.command("create")
@click.option("--name",           default="Viewer Shock",  show_default=True, help="Display name for the link.")
@click.option("--intensity",      default=30,              show_default=True, help="Max intensity percent (1–100).")
@click.option("--duration",       default=1000,            show_default=True, help="Max duration in milliseconds.")
@click.option("--activations",    default=None,            type=int,          help="Remaining activations limit (omit for unlimited).")
@click.option("--expiry-minutes", default=None,            type=int,          help="Minutes until link expires (omit for no expiry).")
@click.option("--shock/--no-shock",   default=True,  show_default=True, help="Allow shock operation.")
@click.option("--vibrate/--no-vibrate", default=False, show_default=True, help="Allow vibrate operation.")
@click.option("--beep/--no-beep",     default=False, show_default=True, help="Allow beep operation.")
def link_create(name, intensity, duration, activations, expiry_minutes, shock, vibrate, beep):
    """Create a one-time (or limited) PiShock viewer link and print the URL."""
    import requests as req
    from pishock_client import get_shocker

    shocker = get_shocker()

    body = {
        "Name": name,
        "ShockerId": shocker.shocker_id,
        "Intensity": intensity,
        "Duration": duration,
        "ShockEnabled": shock,
        "VibrateEnabled": vibrate,
        "BeepEnabled": beep,
        "ForceWarning": True,
        "ForceLogin": False,
        "ShowCountdown": True,
        "ShowUsages": True,
        "Editable": False,
        "ActivateOnLoad": False,
        "SingleUser": True,
    }
    if activations is not None:
        body["RemainingActivations"] = activations
    if expiry_minutes is not None:
        body["ExpiryMinutes"] = expiry_minutes

    r = req.post("https://api.pishock.com/Links", json=body, headers=shocker.headers)
    if not r.ok:
        click.echo(f"Error {r.status_code} creating link: {r.text}", err=True)
        r.raise_for_status()

    # Retrieve the newly created link to get its code/URL
    links_r = req.get("https://api.pishock.com/Links", headers=shocker.headers)
    if not links_r.ok:
        click.echo(f"Error {links_r.status_code} listing links: {links_r.text}", err=True)
        links_r.raise_for_status()
    links = links_r.json()

    # Find the link we just created by name (most recent match)
    match = next((lnk for lnk in reversed(links) if lnk.get("Name") == name), None)
    if match:
        code = match.get("Code", "")
        click.echo(f"Link created: https://pishock.com/go/{code}")
        click.echo(f"  Name:        {name}")
        click.echo(f"  ShockerId:   {shocker.shocker_id}")
        click.echo(f"  Intensity:   {intensity}%")
        click.echo(f"  Duration:    {duration}ms")
        click.echo(f"  Activations: {'unlimited' if activations is None else activations}")
        click.echo(f"  Code (GUID): {code}")
        click.echo(f"\nTo watch: python cli.py link watch --code {code}")
    else:
        click.echo("Link created (could not retrieve code — check PiShock dashboard).")


@link.command("watch")
@click.option("--code",          required=True, help="Link GUID code from `link create`.")
@click.option("--poll-interval", default=2.0,   show_default=True, type=float, help="Seconds between polls.")
@click.option("--no-obs",        is_flag=True,  default=False, help="Disable OBS overlay even if configured.")
def link_watch(code, poll_interval, no_obs):
    """Watch a PiShock link for viewer activations and show OBS overlays."""
    from pishock_client import get_shocker
    import link_watcher

    shocker = get_shocker()

    obs = None
    if not no_obs and _config.get("obs"):
        from obs_client import obs_from_config
        obs = obs_from_config(_config)

    obs_cfg = _config.get("obs", {})
    link_watcher.watch_link(
        link_code=code,
        headers=shocker.headers,
        obs=obs,
        obs_shock_source=obs_cfg.get("shock_source", "Shock Alert"),
        poll_interval=poll_interval,
    )


@link.command("create-and-watch")
@click.option("--name",           default="Viewer Shock",  show_default=True, help="Display name for the link.")
@click.option("--intensity",      default=30,              show_default=True, help="Max intensity percent (1–100).")
@click.option("--duration",       default=1000,            show_default=True, help="Max duration in milliseconds.")
@click.option("--activations",    default=None,            type=int,          help="Remaining activations limit (omit for unlimited).")
@click.option("--expiry-minutes", default=None,            type=int,          help="Minutes until link expires (omit for no expiry).")
@click.option("--shock/--no-shock",     default=True,  show_default=True, help="Allow shock operation.")
@click.option("--vibrate/--no-vibrate", default=False, show_default=True, help="Allow vibrate operation.")
@click.option("--beep/--no-beep",       default=False, show_default=True, help="Allow beep operation.")
@click.option("--poll-interval",  default=2.0, show_default=True, type=float, help="Seconds between polls.")
@click.option("--no-obs",         is_flag=True, default=False, help="Disable OBS overlay even if configured.")
def link_create_and_watch(name, intensity, duration, activations, expiry_minutes,
                          shock, vibrate, beep, poll_interval, no_obs):
    """Create a viewer link, print the URL, then immediately start watching it."""
    import requests as req
    from pishock_client import get_shocker
    import link_watcher

    shocker = get_shocker()

    body = {
        "Name": name,
        "ShockerId": shocker.shocker_id,
        "Intensity": intensity,
        "Duration": duration,
        "ShockEnabled": shock,
        "VibrateEnabled": vibrate,
        "BeepEnabled": beep,
        "ForceWarning": True,
        "ForceLogin": False,
        "ShowCountdown": True,
        "ShowUsages": True,
        "Editable": False,
        "ActivateOnLoad": False,
        "SingleUser": True,
    }
    if activations is not None:
        body["RemainingActivations"] = activations
    if expiry_minutes is not None:
        body["ExpiryMinutes"] = expiry_minutes

    r = req.post("https://api.pishock.com/Links", json=body, headers=shocker.headers)
    if not r.ok:
        click.echo(f"Error {r.status_code} creating link: {r.text}", err=True)
        r.raise_for_status()

    links_r = req.get("https://api.pishock.com/Links", headers=shocker.headers)
    if not links_r.ok:
        click.echo(f"Error {links_r.status_code} listing links: {links_r.text}", err=True)
        links_r.raise_for_status()
    links = links_r.json()

    match = next((lnk for lnk in reversed(links) if lnk.get("Name") == name), None)
    if not match:
        click.echo("Link created but could not retrieve code — check PiShock dashboard.")
        return

    code = match.get("Code", "")
    url = f"https://pishock.com/go/{code}"

    click.echo(f"\n  URL (paste to chat): {url}")
    click.echo(f"  Intensity:           {intensity}%")
    click.echo(f"  Duration:            {duration}ms")
    click.echo(f"  Activations:         {'unlimited' if activations is None else activations}")
    click.echo()

    obs = None
    if not no_obs and _config.get("obs"):
        from obs_client import obs_from_config
        obs = obs_from_config(_config)

    obs_cfg = _config.get("obs", {})
    link_watcher.watch_link(
        link_code=code,
        headers=shocker.headers,
        obs=obs,
        obs_shock_source=obs_cfg.get("shock_source", "Shock Alert"),
        poll_interval=poll_interval,
    )


# ---------------------------------------------------------------------------
# shock-button command (Railway server)
# ---------------------------------------------------------------------------

_sb_cfg = _config.get("shock_button", {})

@cli.command("shock-button")
@click.option("--max-intensity",    default=_sb_cfg.get("max_intensity", 100), show_default=True, type=int,   help="Maximum intensity the viewer can choose (1–100).")
@click.option("--duration",         default=_sb_cfg.get("duration", 2000),     show_default=True, type=int,   help="Shock duration in milliseconds.")
@click.option("--timeout",          default=_sb_cfg.get("timeout", 120),       show_default=True, type=int,   help="Seconds before the slot auto-expires (0 = no timeout).")
@click.option("--label",            default=_sb_cfg.get("label", "Shock Button"), show_default=True,           help="Label shown on the viewer page.")
@click.option("--image-url",        default=_sb_cfg.get("image_url", ""),         show_default=True,           help="Image URL shown on the viewer page (empty = no image).")
@click.option("--poll-interval",    default=2.0,  show_default=True, type=float, help="Seconds between status polls.")
@click.option("--no-obs",           is_flag=True, default=False,                 help="Disable OBS overlay even if configured.")
def shock_button(max_intensity, duration, timeout, label, image_url, poll_interval, no_obs):
    """Arm a one-time shock button on Railway. Viewer picks intensity (1–max); you control duration and timeout."""
    import sys
    import time
    import threading
    import requests as req
    from dotenv import load_dotenv
    from pishock_client import get_shocker
    load_dotenv(dotenv_path=Path(__file__).parent / ".env")

    if not (1 <= max_intensity <= 100):
        raise click.BadParameter("Must be 1–100.", param_hint="--max-intensity")
    if timeout < 0:
        raise click.BadParameter("Must be >= 0 (0 = no timeout).", param_hint="--timeout")

    # These must match what's in your .env
    # os.environ.get() reads environment variables — values that come from your .env file.
    # The app uses these instead of hardcoding them in the source because:
    #   1. Your .env is private (gitignored) — hardcoded values would be public on GitHub
    #   2. Different streamers can have different credentials without changing the code
    railway_url = os.environ.get("RAILWAY_URL", "").rstrip("/")
    arm_secret  = os.environ.get("ARM_SECRET", "")
    if not railway_url:
        raise click.ClickException("RAILWAY_URL not set in .env")
    if not arm_secret:
        raise click.ClickException("ARM_SECRET not set in .env")

    shocker = get_shocker()

    obs = None
    if not no_obs and _config.get("obs"):
        from obs_client import obs_from_config
        obs = obs_from_config(_config)
    obs_cfg = _config.get("obs", {})
    armed_source   = obs_cfg.get("armed_source",   "Shock Armed")
    claimed_source = obs_cfg.get("claimed_source", "Shock Claimed")
    shock_source   = obs_cfg.get("shock_source",   "Shock Alert")

    # Arm slot on Railway — server returns the permanent viewer URL.
    # We POST a JSON body to the /arm endpoint on our Railway server.
    # The server creates a "slot" (a waiting session) and starts accepting viewer connections.
    # It returns the URL that viewers visit — this is the link you paste in chat.
    body: dict = {
        "secret":           arm_secret,
        "min_intensity":    1,
        "max_intensity":    max_intensity,
        "duration":         duration,
        "label":            label,
    }
    if timeout > 0:
        body["timeout_seconds"] = timeout
    if image_url:
        body["image_url"] = image_url
    r = req.post(f"{railway_url}/arm", json=body)
    if not r.ok:
        raise click.ClickException(f"Failed to arm slot: {r.status_code} {r.text}")

    url = r.json()["url"]

    if obs:
        obs.show_source(armed_source)

    timeout_str = f"{timeout}s" if timeout > 0 else "none"
    click.echo(f"\n  *** Permanent URL (post this once, reuse every time): {url} ***\n")
    click.echo(f"  Max intensity: {max_intensity}%  Duration: {duration}ms  Timeout: {timeout_str}  Label: {label}")
    click.echo("  Waiting for a viewer to claim it... (press any key to cancel)")

    stop_event = threading.Event()

    def _wait_key():
        try:
            if sys.platform == "win32":
                import msvcrt
                msvcrt.getch()
            else:
                import tty, termios
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

    threading.Thread(target=_wait_key, daemon=True).start()

    status_url = f"{railway_url}/arm/status"
    claimed = False
    timed_out = False
    slot_allocated = False
    while not stop_event.is_set():
        try:
            s = req.get(status_url, params={"secret": arm_secret}, timeout=5)
            if s.ok:
                state = s.json().get("state")
                if state in ("grabbed", "claimed") and not slot_allocated:
                    slot_allocated = True
                    click.echo("  Viewer grabbed the control!")
                    if obs:
                        obs.hide_source(armed_source)
                        obs.show_source(claimed_source)
                if state == "claimed":
                    claimed = True
                    stop_event.set()
                elif state in ("expired", "cancelled"):
                    timed_out = True
                    stop_event.set()
        except Exception as e:
            click.echo(f"  Poll error: {e}", err=True)
        if not stop_event.is_set():
            stop_event.wait(poll_interval)

    if claimed:
        # A viewer claimed the slot! Read back the intensity they chose.
        # The viewer picked a number between 1 and max_intensity on their slider.
        # We now read that value from the server and fire the shock at exactly that intensity.
        s = req.get(status_url, params={"secret": arm_secret}, timeout=5)
        viewer_intensity = s.json().get("claimed_intensity", max_intensity) if s.ok else max_intensity
        duration_s = duration / 1000

        click.echo(f"  Claimed! Intensity={viewer_intensity}%  Duration={duration_s:.1f}s")

        if obs:
            obs.hide_source(claimed_source)
            obs.show_source(shock_source)

        shocker.shock(duration=round(duration_s), intensity=viewer_intensity)
        import time
        time.sleep(duration_s)

        if obs:
            obs.hide_source(shock_source)
    elif timed_out:
        click.echo("  Slot timed out — no viewer claimed it.")
        if obs:
            obs.hide_source(armed_source)
            obs.hide_source(claimed_source)
    else:
        click.echo("  Cancelled.")
        # Cancel the slot on Railway so the viewer page shows 'expired'
        try:
            req.delete(f"{railway_url}/arm", params={"secret": arm_secret}, timeout=5)
        except Exception:
            pass
        if obs:
            obs.hide_source(armed_source)
            obs.hide_source(claimed_source)

    click.echo("  Done.")


# ---------------------------------------------------------------------------
# Token management commands
# ---------------------------------------------------------------------------

@cli.command("rotate-token")
def rotate_token():
    """Rotate the channel token on Railway — invalidates the old viewer URL and prints the new one."""
    import requests as req
    from dotenv import load_dotenv
    load_dotenv(dotenv_path=Path(__file__).parent / ".env")

    railway_url = os.environ.get("RAILWAY_URL", "").rstrip("/")
    arm_secret  = os.environ.get("ARM_SECRET", "")
    if not railway_url:
        raise click.ClickException("RAILWAY_URL not set in .env")
    if not arm_secret:
        raise click.ClickException("ARM_SECRET not set in .env")

    r = req.post(f"{railway_url}/channel-token/rotate", json={"secret": arm_secret}, timeout=10)
    if not r.ok:
        raise click.ClickException(f"Failed to rotate token: {r.status_code} {r.text}")

    data = r.json()
    click.echo(f"\n  *** New viewer URL: {data['url']} ***")
    click.echo(f"  {data.get('reminder', '')}")


@cli.command("get-url")
def get_url():
    """Print the current viewer URL (useful after a Railway restart)."""
    import requests as req
    from dotenv import load_dotenv
    load_dotenv(dotenv_path=Path(__file__).parent / ".env")

    railway_url = os.environ.get("RAILWAY_URL", "").rstrip("/")
    arm_secret  = os.environ.get("ARM_SECRET", "")
    if not railway_url:
        raise click.ClickException("RAILWAY_URL not set in .env")
    if not arm_secret:
        raise click.ClickException("ARM_SECRET not set in .env")

    r = req.get(f"{railway_url}/channel-token/current", params={"secret": arm_secret}, timeout=10)
    if not r.ok:
        raise click.ClickException(f"Failed to get URL: {r.status_code} {r.text}")

    click.echo(r.json()["url"])


if __name__ == "__main__":
    cli()
