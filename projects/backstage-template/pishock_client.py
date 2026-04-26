"""
PiShock API client.

This module handles the connection to PiShock's servers and fires shocks.
You don't need to edit this file unless you want to change how shocks work.

How it works:
  1. Reads your PiShock credentials from the .env file
  2. Authenticates with the PiShock API
  3. Resolves your share code to a shocker device ID
  4. Provides a Shocker object that cli.py uses to fire shocks

Credentials come from .env (never from the code itself) so they stay private.
"""

import os
import requests
from pathlib import Path
from dotenv import load_dotenv

# dotenv reads your .env file and loads each line as an environment variable.
# This is how secrets get into the app without being written in the source code.
load_dotenv(dotenv_path=Path(__file__).parent / ".env")

_API_BASE = "https://api.pishock.com"  # PiShock's REST API base URL


class Shocker:
    def __init__(self, shocker_id: int, headers: dict):
        self._shocker_id = shocker_id
        self._headers = headers

    @property
    def shocker_id(self) -> int:
        return self._shocker_id

    @property
    def headers(self) -> dict:
        """Auth headers for direct API calls (e.g. Links API)."""
        return self._headers

    def shock(self, *, duration: int, intensity: int):
        """
        Fire the shock.

        duration  : how long the shock lasts, in seconds (1–15)
        intensity : how strong the shock is, as a percentage (1–100)

        Note: PiShock's API expects duration in milliseconds, so we multiply by 1000 here.
        The rest of the codebase uses milliseconds everywhere for consistency.
        """
        body = {
            "AgentName": "Backstage",
            "Operation": 0,  # 0=Shock, 1=Vibrate, 2=Beep
            "Duration": duration * 1000,  # API expects milliseconds
            "Intensity": intensity,
            "IntensityAsPercentage": False,
        }
        r = requests.post(
            f"{_API_BASE}/Shockers/{self._shocker_id}",
            json=body,
            headers=self._headers,
        )
        r.raise_for_status()


def get_shocker() -> Shocker:
    """
    Load credentials from .env and return a ready-to-use Shocker object.

    This is called at the start of every shock command. It:
      1. Reads credentials from environment variables (set by .env)
      2. Authenticates with the PiShock API
      3. Claims your share code (safe to run multiple times)
      4. Resolves the share code to an integer shocker ID
      5. Returns a Shocker object you can call .shock() on
    """
    username = os.environ.get("PISHOCK_USERNAME")
    api_key = os.environ.get("PISHOCK_API_KEY")
    share_code = os.environ.get("PISHOCK_SHARE_CODE", "").strip()

    # Fail loudly if credentials are missing — a clear error message is much better
    # than a confusing "401 Unauthorized" from the PiShock API.
    if not all([username, api_key, share_code]):
        missing = [k for k, v in {
            "PISHOCK_USERNAME": username,
            "PISHOCK_API_KEY": api_key,
            "PISHOCK_SHARE_CODE": share_code,
        }.items() if not v]
        raise EnvironmentError(
            f"Missing required environment variables: {', '.join(missing)}\n"
            "Copy .env.example to .env and fill in your credentials."
        )

    masked_key = api_key[:4] + "*" * (len(api_key) - 8) + api_key[-4:]
    print(f"  Username:   {username}")
    print(f"  API key:    {masked_key}")
    print(f"  Share code: {share_code}")

    headers = {
        "X-PiShock-Username": username,
        "X-PiShock-Api-Key": api_key,
        "Content-Type": "application/json",
    }

    # Fetch UserId for endpoints that require X-PiShock-UserId
    print("  Fetching account info...", end="", flush=True)
    r = requests.get(f"{_API_BASE}/Account", headers=headers)
    if r.ok:
        user_id = r.json().get("UserId")
        if user_id is not None:
            headers["X-PiShock-UserId"] = str(user_id)
            print(f" UserId={user_id}")
        else:
            print(" (no UserId in response)")
    else:
        print(f" skipped ({r.status_code})")

    # Claim the share code (safe to call repeatedly; 410 = already claimed)
    print("  Claiming share code...", end="", flush=True)
    r = requests.put(
        f"{_API_BASE}/Share",
        json={"Shares": [share_code]},
        headers=headers,
    )
    if r.status_code == 410:
        print(" already claimed")
    elif r.status_code == 204:
        print(" claimed")
    else:
        r.raise_for_status()

    # Resolve share code -> integer ShockerId
    print("  Looking up shocker...", end="", flush=True)
    r = requests.get(f"{_API_BASE}/Share/GetShared", headers=headers)
    r.raise_for_status()
    shared = r.json()

    match = next((s for s in shared if s.get("ShareCode") == share_code), None)
    if match is None:
        available = [s.get("ShareCode") for s in shared]
        raise ValueError(
            f"Share code {share_code!r} not found after claiming.\n"
            f"Claimed shares on this account: {available}"
        )

    shocker_id = match["Id"]
    share_id = match.get("ShareId")
    name = match.get("Name") or "unnamed"
    can_create_links = match.get("CanCreateLinks", False)
    print(f" found '{name}' (ID {shocker_id}, ShareId={share_id}, CanCreateLinks={can_create_links})")

    if not can_create_links and share_id is not None:
        print("  Enabling link creation on share...", end="", flush=True)
        r = requests.patch(
            f"{_API_BASE}/Share/Link",
            json={"ShareId": share_id, "LinkCreationState": True},
            headers=headers,
        )
        if r.ok:
            print(" done")
        else:
            print(f" failed ({r.status_code}: {r.text})")

    return Shocker(shocker_id=shocker_id, headers=headers)
