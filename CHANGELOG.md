# Changelog

## 0.4.3

- **Fixed the `ArgumentException: Getting control N's position ... when doing repaint` crash that
  showed up when launching repeatedly.** The panel refreshed its hive/star/tinder lists from inside
  the click handler, i.e. in the middle of an IMGUI frame. The Repaint pass then saw one more button
  than the Layout pass, Unity aborted the GUI and the quit-time autosave failed with it. Data is now
  rebuilt in `Update` only, and the "cancel queued order" button always occupies its slot (disabled
  instead of disappearing).
- **Tinder spawning now respects the game's own cap.** `EnemyDFHiveSystem.AddIdleTinder` writes into
  a fixed `int[16]` with no bounds check, and vanilla only keeps
  `idleTinderCount < tinderDocks.Length`. Bypassing that gate (as the on-demand spawn used to) could
  push a hive past 16 idle tinders and make the game throw `IndexOutOfRangeException` every tick.
- 修掉了「反复发射后游戏报错」：面板原先在按钮回调里直接重算列表，导致 IMGUI 的 Layout 与
  Repaint 控件数不一致、抛 `ArgumentException`，并连带退出时的自动存档失败。现在只在 Update
  里刷新，待发按钮也改为常驻（不可用时置灰）。
- 现造火种现在遵守原版上限：空闲火种数受 `tinderDocks.Length` 与固定 16 格 `idleTinderIds`
  双重限制，不会再触发游戏侧 `IndexOutOfRangeException`。

## 0.4.2

- **The panel can be moved now.** Drag the title bar to put it wherever you like; double-click the
  title bar to snap it back to the centre of the screen. The position is remembered in the BepInEx
  config (`UI.PanelX` / `UI.PanelY`) and restored next time. The window is also clamped back on
  screen when the resolution changes.
- 面板可以拖动了：按住标题栏拖到任意位置，双击标题栏回到屏幕中央；位置写进 BepInEx 配置
  （`UI.PanelX` / `UI.PanelY`），下次打开沿用。分辨率变化时自动拉回可见区域。

## 0.4.1

- New package icon.
- Manifest namespace aligned with the published page (ManifestDK).
- No gameplay changes.

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
