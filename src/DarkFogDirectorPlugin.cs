using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace DarkFogDirector
{
    /// <summary>
    /// 让黑雾按玩家指定的方向扩张：把「巢穴 -> 火种发射目标星系」写进指令文件即可。
    ///
    /// 已验证的目标版本：Dyson Sphere Program 0.10.35.29151（Unity 2022.3.62f3c1 / Mono）。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class DarkFogDirectorPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "dsp.darkfog.director";
        public const string PluginName = "Dark Fog Expansion Director";
        public const string PluginVersion = "0.4.2";

        internal static DarkFogDirectorPlugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<KeyboardShortcut> ReloadKey;
        internal static ConfigEntry<KeyboardShortcut> DumpKey;
        internal static ConfigEntry<int> ForceCooldownTicks;
        internal static ConfigEntry<bool> Verbose;

        /// <summary>面板左上角位置（屏幕像素）。-1 表示「按屏幕居中」。</summary>
        internal static ConfigEntry<float> PanelX;
        internal static ConfigEntry<float> PanelY;

        internal static string OrdersPath;
        internal static string DumpPath;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            ReloadKey = Config.Bind("Hotkeys", "Reload", new KeyboardShortcut(KeyCode.F10),
                "重新载入指令文件。");
            DumpKey = Config.Bind("Hotkeys", "Dump", new KeyboardShortcut(KeyCode.F9),
                "把当前星系/巢穴清单写入日志与文件，方便编写指令。");
            ForceCooldownTicks = Config.Bind("Behaviour", "ForceLaunchCooldownTicks", 1800,
                "同一巢穴两次「强制起飞」之间的最小间隔（游戏 tick，60 tick ≈ 1 秒）。");
            Verbose = Config.Bind("Behaviour", "VerboseLog", false,
                "每次改写目标时输出详细日志。");

            PanelX = Config.Bind("UI", "PanelX", -1f,
                "面板左上角的屏幕 X 坐标（-1 = 每次按屏幕居中）。拖动标题栏后自动记忆。");
            PanelY = Config.Bind("UI", "PanelY", -1f,
                "面板左上角的屏幕 Y 坐标（-1 = 每次按屏幕居中）。拖动标题栏后自动记忆。");

            OrdersPath = Path.Combine(Paths.ConfigPath, "DarkFogDirector.orders.txt");
            DumpPath = Path.Combine(Paths.ConfigPath, "DarkFogDirector.dump.txt");

            Director.Rules.Load(OrdersPath);

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll(typeof(DarkFogDirectorPlugin).Assembly);
                LogInfo("Harmony 补丁已应用：" + PluginName + " " + PluginVersion);
            }
            catch (Exception e)
            {
                LogError("Harmony 补丁失败：" + e);
            }
        }

        private void OnDestroy()
        {
            DirectorPanel.SavePosition();

            if (_harmony != null)
            {
                try { _harmony.UnpatchSelf(); } catch { }
            }
        }

        private void Update()
        {
            try
            {
                // \ 键（Backspace 与 Enter 之间那个）呼出/关闭操作界面
                if (UnityEngine.Input.GetKeyDown(KeyCode.Backslash))
                {
                    DirectorPanel.Toggle();
                }
                if (ReloadKey.Value.IsDown())
                {
                    Director.Rules.Load(OrdersPath);
                    LogInfo("指令已重新载入。");
                }
                if (DumpKey.Value.IsDown())
                {
                    DumpNow();
                }
            }
            catch (Exception e)
            {
                LogError("热键处理失败：" + e.Message);
            }
        }

        private void OnGUI()
        {
            DirectorPanel.OnGUI();
        }

        private static void DumpNow()
        {
            string text;
            try
            {
                var data = GameMain.data;   // GameMain.data 是静态字段
                var sector = data != null ? data.spaceSector : null;
                text = Director.BuildDump(sector);
            }
            catch (Exception e)
            {
                text = "读取现场失败：" + e.Message;
            }

            LogInfo("\n" + text);
            try
            {
                File.WriteAllText(DumpPath, text, System.Text.Encoding.UTF8);
                LogInfo("清单已写入：" + DumpPath);
            }
            catch (Exception e)
            {
                LogWarn("写出清单失败：" + e.Message);
            }
        }

        internal static void LogInfo(string msg)
        {
            if (Log != null) Log.LogInfo(msg);
        }

        internal static void LogWarn(string msg)
        {
            if (Log != null) Log.LogWarning(msg);
        }

        internal static void LogError(string msg)
        {
            if (Log != null) Log.LogError(msg);
        }

        internal static void LogDebug(string msg)
        {
            if (Log != null && Verbose != null && Verbose.Value) Log.LogInfo(msg);
        }
    }

    /// <summary>
    /// 核心补丁：原版给所有星系打分之后，把玩家指定的目标星系顶到首位并给压倒性权重，
    /// 使随后的加权抽取必然命中它。其余逻辑（巢穴创建、轨道选择、物质装载）原样保留。
    /// </summary>
    [HarmonyPatch(typeof(DFTinderComponent), "GenerateSortedStarIndices")]
    internal static class Patch_GenerateSortedStarIndices
    {
        [HarmonyPostfix]
        internal static void Postfix(ref DFTinderComponent __instance, SpaceSector sector)
        {
            try
            {
                Director.OnStarIndicesGenerated(ref __instance, sector);
            }
            catch (Exception e)
            {
                DarkFogDirectorPlugin.LogError("定向补丁异常（已忽略本次）：" + e);
            }
        }
    }

    /// <summary>
    /// 可选补丁：指令带 ! 时，跨过原版的节奏与概率闸门，直接发射已装填完成的火种。
    /// 返回 false 表示完全接管，原版方法不再执行。
    /// </summary>
    [HarmonyPatch(typeof(DFTinderComponent), "PrepareDispatchLogic")]
    internal static class Patch_PrepareDispatchLogic
    {
        [HarmonyPrefix]
        internal static bool Prefix(ref DFTinderComponent __instance, EnemyDFHiveSystem hive)
        {
            try
            {
                // 优先处理 UI 下发的「立即发射」指令
                if (LaunchOrders.TryConsume(ref __instance, hive)) return false;

                int cooldown = DarkFogDirectorPlugin.ForceCooldownTicks != null
                    ? DarkFogDirectorPlugin.ForceCooldownTicks.Value
                    : 1800;
                return !Director.TryForceLaunch(ref __instance, hive, cooldown);
            }
            catch (Exception e)
            {
                DarkFogDirectorPlugin.LogError("强制起飞补丁异常（已交还原版）：" + e);
                return true;
            }
        }
    }
}
