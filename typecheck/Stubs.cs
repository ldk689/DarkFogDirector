// 仅用于离线编译校验：把 BepInEx / HarmonyX 的最小表面打桩，
// 这样可以在没装 BepInEx 的机器上验证对 Assembly-CSharp 的 API 调用是否正确。
// 正式编译时不要把它包含进项目（csproj 已排除 typecheck 目录）。
using System;
using System.Reflection;

namespace BepInEx
{
    public static class Paths
    {
        public static string ConfigPath = ".";
        public static string PluginPath = ".";
        public static string GameRootPath = ".";
    }

    public abstract class BaseUnityPlugin : UnityEngine.MonoBehaviour
    {
        public Configuration.ConfigFile Config;
        public Logging.ManualLogSource Logger;
    }
}

namespace BepInEx.Logging
{
    public class ManualLogSource
    {
        public void LogInfo(object data) { }
        public void LogWarning(object data) { }
        public void LogError(object data) { }
        public void LogDebug(object data) { }
    }
}

namespace BepInEx.Configuration
{
    public class ConfigDefinition
    {
        public ConfigDefinition(string section, string key) { }
    }

    public class ConfigEntry<T>
    {
        public T Value;
    }

    public class ConfigFile
    {
        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
        {
            return new ConfigEntry<T> { Value = defaultValue };
        }
    }

    public struct KeyboardShortcut
    {
        public KeyboardShortcut(UnityEngine.KeyCode key) { }
        public bool IsDown() { return false; }
    }
}

namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BepInPlugin : Attribute
    {
        public BepInPlugin(string guid, string name, string version) { }
    }
}

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string methodName) { }
        public HarmonyPatch(Type type, string methodName, params Type[] argumentTypes) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class HarmonyPrefix : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class HarmonyPostfix : Attribute { }

    public class Harmony
    {
        public Harmony(string id) { }
        public void PatchAll(Assembly assembly) { }
        public void UnpatchSelf() { }
    }
}
