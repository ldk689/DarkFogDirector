# Dark Fog Director

Take command of the Dark Fog's interstellar expansion. Pick a hive, pick a target system, and
launch the Tinder yourself — with the fuel topped up and the flight speed under your control.

[![DSP](https://img.shields.io/badge/Dyson%20Sphere%20Program-0.10.35-blue)](https://store.steampowered.com/app/1366540/)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.4.x-orange)](https://github.com/BepInEx/BepInEx)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

> 中文说明见下方 [中文](#中文说明)。

---

## English

### What it does

In vanilla DSP, the Dark Fog decides on its own when and where to send a *Tinder* (火种 / the
seed that crosses star systems to found a new hive). This mod puts that decision in your hands.

Press **`\`** in game to open the panel. It follows your in-game language
(English / 简体中文 / Français / Deutsch / 日本語).

| Feature | Description |
|---|---|
| **Directed launch** | Choose a source hive and a target system, press LAUNCH. The Tinder flies there immediately. |
| **Fuel auto-fill** | The tinder's builder is topped up (`matter` / `energy` / `sp`) before launch, so you never wait for production. |
| **On-demand tinder** | If the hive has no idle tinder, one is built on the spot through the game's own `AddTinderDeferred` path. |
| **Per-tinder speed** | Override the flight speed of one specific tinder, or all of them. Unit is m/s (vanilla cap is 1200). |
| **Safe targeting** | The destination is always a **newly created** hive; the orbit is chosen by the game itself. No hand-picked orbits, no save corruption. |

### Requirements

- Dyson Sphere Program **0.10.35.29151** (Unity 2022.3, Mono)
- **BepInEx 5.4.x** (Mono build) — works with the `xiaoye97-BepInEx` package on Thunderstore

### Installation

**With r2modman / Thunderstore Mod Manager** (recommended)

1. Install `BepInEx` (by xiaoye97) into your DSP profile first.
2. Install this mod, or use *Install mod from file* with the release zip.
3. **Launch the game through r2modman**, not through Steam directly.

**Manual**

Drop `DarkFogDirector.dll` into

```
<profile>\BepInEx\plugins\DarkFogDirector\
```

### Usage

Press **`\`** (the key between Backspace and Enter) to open or close the panel.

**Launch tab**

- Left: **source hives** — every existing hive, with its tinder count and how many are idle.
- Right: **target systems** — only systems below the hive cap. Normal stars allow **3** hives,
  black holes / neutron stars allow **6** (this is the game's own `StarData.maxHiveCount`).
- Press **LAUNCH**. If the hive has no idle tinder, one is built immediately.

**Tinders tab**

- Lists every tinder in the cluster: `source hive → target hive [state] speed`.
- Click a tinder to select it, type a speed in m/s, then **Apply to selected** —
  or **Apply to all** to override every tinder.
- **Restore vanilla** clears all overrides and hands control back to the game's own
  closed-loop speed controller.

### How it works (short version)

The mod patches three methods of `DFTinderComponent`:

| Method | Patch | Purpose |
|---|---|---|
| `GenerateSortedStarIndices` | Postfix | Rewrites the star scoring array so the chosen system wins the weighted draw. |
| `PrepareDispatchLogic` | Prefix | Consumes queued orders: fills resources and dispatches immediately. |
| `TinderSailLogic` | Postfix | Pins `uSpeed` every tick, because vanilla recomputes it from a distance-based controller. |

The target always goes through `SpaceSector.TryCreateNewHive`, which is exactly what vanilla does.
See `黑雾火种机制.md` (Chinese) for the full reverse-engineered mechanism.

### Known limitations

- The mod only affects **where the tinder goes, how fast it flies, and when it launches**.
  Whether a system can be colonised still obeys `maxHiveCount` and free orbits.
- Fuel auto-fill is deliberately cheaty: it means "the hive already paid for it", not a new economy.
- The panel is IMGUI, so it looks like a plain tool window rather than native DSP UI.
- `uSpeed` is part of the save file; restore vanilla speeds before saving if you used extreme values.
- Version-locked to **0.10.35.29151**. Game updates may break the patches (check `LogOutput.log`).

### License

MIT — see [LICENSE](LICENSE).

---

## 中文说明

接管黑雾的跨星系扩张：**你来决定哪个巢穴、向哪个星系、发射火种**，燃料自动补齐，
飞行速度也能单独调。

### 功能

在原版里，黑雾自己决定什么时候、向哪里发射**火种**。本 MOD 把这个决定权交给你。

游戏内按 **`\`** 呼出面板。界面语言跟随游戏设置（中/英/法/德/日）。

| 功能 | 说明 |
|---|---|
| **指定发射** | 选一个起点巢穴和一个目标星系，点「发射」，火种立刻起飞。 |
| **资源自动补满** | 发射前把火种的建造器补满（`matter` / `energy` / `sp`），不用等生产。 |
| **火种按需现造** | 巢穴没有空闲火种时，走游戏自己的 `AddTinderDeferred` 通道当场造一个。 |
| **单颗火种调速** | 可以只改某颗火种的速度，也可以全部一起改。单位 m/s（原版上限 1200）。 |
| **安全选点** | 目标一律是**新建**巢穴，轨道由游戏自己分配，绝不人工指定，不会坏档。 |

### 环境要求

- 戴森球计划 **0.10.35.29151**（Unity 2022.3 / Mono）
- **BepInEx 5.4.x**（Mono 版），即 Thunderstore 上的 `xiaoye97-BepInEx`

### 安装

**r2modman / Thunderstore Mod Manager（推荐）**

1. 先在配置档里装好 `BepInEx`（作者 xiaoye97）。
2. 安装本 MOD，或用「Install mod from file」导入压缩包。
3. **必须从 r2modman 启动游戏**，不要直接点 Steam 的开始按钮。

**手动**

把 `DarkFogDirector.dll` 放进

```
<配置档>\BepInEx\plugins\DarkFogDirector\
```

### 使用

游戏内按 **`\`**（Backspace 与 Enter 之间那个键）开关面板。

**发射指挥页**

- 左栏：**起点巢穴**，列出所有已有巢穴，以及「火种总数 / 空闲待发数」。
- 右栏：**目标星系**，只列未达上限的星系。普通星系上限 **3**，黑洞/中子星上限 **6**
  （取自游戏自己的 `StarData.maxHiveCount`）。
- 点 **发射**。如果该巢穴没有空闲火种，会当场造一个再发射。

**火种管理页**

- 列出全图所有火种：`起点巢穴 → 目标巢穴 [状态] 速度`。
- 点选一颗火种，输入速度（m/s），点 **应用到选中**；或点 **应用到全部** 统一改所有火种。
- **恢复原版** 清除全部覆盖，把速度交回游戏自己的闭环调速器。

### 原理简述

MOD 打了三个 `DFTinderComponent` 的补丁：

| 方法 | 补丁 | 作用 |
|---|---|---|
| `GenerateSortedStarIndices` | Postfix | 改写星系评分数组，让指定星系必然被加权随机选中。 |
| `PrepareDispatchLogic` | Prefix | 消费待发指令：补满资源并立即发射。 |
| `TinderSailLogic` | Postfix | 每 tick 把 `uSpeed` 钉住——原版是按距离闭环计算的，不钉会被拉回去。 |

目标一律走 `SpaceSector.TryCreateNewHive`，与原版行为完全一致。
完整反汇编机制见 `黑雾火种机制.md`。

### 已知限制

- 只影响**火种去哪、飞多快、什么时候发**；能否开巢仍受 `maxHiveCount` 与空轨道限制。
- 资源自动补满是刻意的作弊向设计：等价于「母巢已经付过账」，不额外产生经济。
- 面板用 IMGUI 绘制，外观是简朴的工具窗口，不是原版风格。
- `uSpeed` 是存档字段；用过夸张数值的话，请先「恢复原版」再存档。
- 版本锁定 **0.10.35.29151**，游戏更新可能导致补丁失效（看 `LogOutput.log`）。

### 许可证

MIT，见 [LICENSE](LICENSE)。
