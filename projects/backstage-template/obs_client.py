"""
OBS WebSocket client for Backstage stream overlays.

This module is OPTIONAL. If you don't use OBS, nothing in here is ever called
and you can completely ignore this file.

What it does:
  OBS (Open Broadcaster Software) is the streaming software many streamers use.
  It has a feature called WebSocket that lets other programs control it remotely.
  This module uses that to automatically show/hide overlay sources in your OBS scene
  at the right moments — when the button arms, when a viewer grabs it, and when the
  shock fires.

How it degrades gracefully:
  If OBS isn't running, or you haven't configured it, the ObsClient just prints a
  warning once and then becomes a no-op. Every obs.show_source() / obs.hide_source()
  call silently does nothing instead of crashing. This means OBS being off never
  breaks a shock session.
"""

from __future__ import annotations


class ObsClient:
    """Show/hide OBS scene item sources by name via WebSocket v5."""

    def __init__(self, host: str, port: int, password: str, scene: str):
        self._scene = scene
        self._item_id_cache: dict[str, int] = {}
        self._connected = False

        try:
            import obsws_python as obs  # type: ignore[import]
            self._ws = obs.ReqClient(host=host, port=port, password=password, timeout=3)
            self._connected = True
            print(f"  OBS connected ({host}:{port})", flush=True)
        except Exception as e:
            print(f"  OBS not available ({e}) — overlay disabled.", flush=True)
            self._ws = None

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def show_source(self, source_name: str) -> None:
        self._set_enabled(source_name, True)

    def hide_source(self, source_name: str) -> None:
        self._set_enabled(source_name, False)

    def disconnect(self) -> None:
        if self._connected and self._ws is not None:
            try:
                self._ws.disconnect()
            except Exception:
                pass

    # ------------------------------------------------------------------
    # Internals
    # ------------------------------------------------------------------

    def _get_item_id(self, source_name: str) -> int | None:
        if source_name in self._item_id_cache:
            return self._item_id_cache[source_name]
        try:
            resp = self._ws.get_scene_item_id(scene_name=self._scene, source_name=source_name)
            item_id = resp.scene_item_id
            self._item_id_cache[source_name] = item_id
            return item_id
        except Exception as e:
            print(f"  OBS: could not find source '{source_name}' in scene '{self._scene}': {e}", flush=True)
            return None

    def _set_enabled(self, source_name: str, enabled: bool) -> None:
        if not self._connected or self._ws is None:
            return
        item_id = self._get_item_id(source_name)
        if item_id is None:
            return
        try:
            self._ws.set_scene_item_enabled(
                scene_name=self._scene,
                item_id=item_id,
                enabled=enabled,
            )
        except Exception as e:
            print(f"  OBS set_enabled error: {e}", flush=True)


def obs_from_config(config: dict) -> ObsClient:
    """Build an ObsClient from the [obs] section of config.toml."""
    cfg = config.get("obs", {})
    return ObsClient(
        host=cfg.get("host", "localhost"),
        port=cfg.get("port", 4455),
        password=cfg.get("password", ""),
        scene=cfg.get("scene", "Stream"),
    )
