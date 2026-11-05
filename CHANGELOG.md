# Changelog

## 0.4.0

- **Per-tinder speed override.** Select a single tinder in the *Tinders* tab and give it its own
  speed; others keep following the global value. Overrides are dropped automatically once the
  tinder reaches its destination, so a recycled pool slot never inherits a stale speed.
- **Localization for all shipped game languages:** 简体中文 / English / Français / Deutsch / 日本語.
  The panel follows `Localization.CurrentLanguageLCID` and updates live when you change the
  language in game; the font is rebuilt per language so CJK renders correctly.
- Log messages are now in English so bug reports are readable by anyone.

## 0.3.1

- **LAUNCH now really launches.** When the hive had no idle tinder, the mod used to fall back to a
  queued order. It now builds a tinder on the spot through the game's own `AddTinderDeferred` →
  `ExecuteDeferredEnemyChange` path and dispatches it immediately.
- Added a diagnostic log line when even that fails (hive realized state, dock count, pool cursor).

## 0.3.0

- Target list now uses the game's own `StarData.maxHiveCount` — normal systems allow 3 hives,
  black holes / neutron stars allow 6.
- **Fuel auto-fill**: the tinder's builder (`matter` / `energy` / `sp`) is topped up right before
  dispatch, matching the vanilla "topped up from the parent hive" end state.
- New *Tinders* tab listing every tinder in the cluster, with a global flight-speed override.
- Flight speed is now understood properly: vanilla uses a closed-loop controller
  (`240 u/s²` accel, `360 u/s²` decel, hard cap `1200 u/s`), so the override is re-pinned every tick.

## 0.2.0

- In-game IMGUI panel, toggled with `\`.
- Source hive / target system pickers, LAUNCH button, queued-order fallback.

## 0.1.0

- First working version: passive redirection of Dark Fog expansion through a text rule file
  (`DarkFogDirector.orders.txt`), plus an optional forced-launch flag.
