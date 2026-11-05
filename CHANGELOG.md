# Changelog / 更新日志

<!-- 最近三个版本；更早的版本在下方。 -->

## 0.4.3 — 2026-11-05 · 修掉连续发射后的报错

### 现象

在面板里连续点「发射」，游戏报错并中断，退出时自动存档写成 `<auto save errored>`：

```
ArgumentException: Getting control 161's position in a group with only 161 controls when doing repaint
Aborting
  at UnityEngine.GUILayoutGroup.GetNext ()
  ...
  at DarkFogDirector.DirectorPanel.DrawLaunchTab ()
  at DarkFogDirector.DirectorPanel.DrawBody ()
  at DarkFogDirector.DirectorPanel.DrawWindow ()
GUI Error: You are pushing more GUIClips than you are popping.
```

### 根因

- `DoLaunch()` 在按钮回调里直接调用 `Refresh()` 重算巢穴 / 星系 / 火种列表。
- IMGUI 一帧内会走 `Layout → 各种事件 → Repaint` 多趟；每次发射都会当场新建一个巢穴，
  列表因此多一行，Repaint 阶段要画的控件比 Layout 阶段登记的多一个。
- Unity 抛 `ArgumentException` 后 `Aborting`，GUIClip 失衡，连带退出时的自动存档失败。

### 修复

- 数据只在 `Update` 里刷新（新增 `DirectorPanel.Tick()`），按钮回调一律只调 `QueueRefresh()`，
  保证同一帧内列表长度恒定。
- 「取消待发」按钮改为**常驻占位**、不可用时置灰，不再按条件增删控件。

### 顺带加固

- 现造火种（按需 `SpawnTinderNow`）补上原版闸门 `idleTinderCount < tinderDocks.Length`。
- 额外按 `EnemyDFHiveSystem.idleTinderIds` 的固定 16 格做第二道保险：原版 `AddIdleTinder`
  没有任何越界检查，第 17 个空闲火种会让游戏每 tick 抛 `IndexOutOfRangeException`。

### English

- Fixed `ArgumentException: Getting control N's position ... when doing repaint`. The panel rebuilt
  its lists inside a button handler, in the middle of an IMGUI frame; the Repaint pass then saw more
  buttons than the Layout pass, Unity aborted the GUI and the quit-time autosave failed with it.
  Data is now rebuilt in `Update` only.
- The "cancel queued order" button keeps its slot (disabled instead of hidden).
- On-demand tinder spawning now honours vanilla's `idleTinderCount < tinderDocks.Length` gate and
  the fixed 16-slot `idleTinderIds` array.

## 0.4.2 — 2026-11-05 · 面板可拖动

- **IMGUI 面板支持拖动。** 按住标题栏即可把面板拖到任意位置，双击标题栏回到屏幕中央。
- 位置写入 BepInEx 配置 `UI.PanelX` / `UI.PanelY`，下次呼出沿用；分辨率变化或被拖出屏幕时
  会自动拉回可见区域。
- 标题栏右侧增加了一行多语言提示（中 / 英 / 法 / 德 / 日）。
- English: the IMGUI panel is now draggable by its title bar, double-click recentres it, and the
  position is persisted in the BepInEx config (`UI.PanelX` / `UI.PanelY`).

## 0.4.1 — 2026-11-05 · 新增图标

- 新增 MOD 图标（`icon.png`），随 Thunderstore 包一起发布。
- 清单命名空间对齐已发布的页面（`ManifestDK`）。
- 无玩法改动。
- English: new package icon; manifest namespace aligned with the published page; no gameplay changes.

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
