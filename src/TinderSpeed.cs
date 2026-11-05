using System;
using System.Collections.Generic;
using HarmonyLib;

namespace DarkFogDirector
{
    internal struct TinderRow
    {
        public int HiveAstroId;
        public int TinderId;
        public string From;
        public string Target;
        public string Stage;
        public float Speed;
        public bool Overridden;
    }

    /// <summary>
    /// 火种飞行速度接管，支持**单颗火种**单独设定。
    ///
    /// 原版是一个闭环调速器（DFTinderComponent.TinderSailLogic，stage==0 分支）：
    ///   目标速度 = uSpeed * 剩余距离 / (uSpeed + 0.1) * 0.382 + 50，上限 1200
    ///   每 tick 只能朝目标速度靠 240*dt（加速）或 360*dt（减速）
    ///   然后 vel = 方向 * uSpeed，再积分位置
    /// 因为每 tick 的变化量被限幅，只要我们在方法末尾把 uSpeed 钉成玩家设定值，
    /// 下一 tick 它最多也只会漂移一个步长，实际就稳定在设定值上。
    ///
    /// 单位：u/s，游戏里 1 个宇宙单位 ≈ 1 米，所以数值上等同 m/s。
    /// </summary>
    internal static class TinderSpeed
    {
        internal const float VanillaMaxSpeed = 1200f;

        /// <summary>全局兜底速度；0 = 不接管。</summary>
        internal static float GlobalSpeed;

        /// <summary>单颗火种的覆盖速度，key = hiveAstroId &lt;&lt; 32 | tinderId。</summary>
        private static readonly Dictionary<long, float> _perTinder = new Dictionary<long, float>();

        internal static long Key(int hiveAstroId, int tinderId)
        {
            return ((long)hiveAstroId << 32) | (uint)tinderId;
        }

        internal static void SetTinder(int hiveAstroId, int tinderId, float speed)
        {
            _perTinder[Key(hiveAstroId, tinderId)] = speed;
        }

        internal static bool ClearTinder(int hiveAstroId, int tinderId)
        {
            return _perTinder.Remove(Key(hiveAstroId, tinderId));
        }

        internal static bool HasTinder(int hiveAstroId, int tinderId)
        {
            return _perTinder.ContainsKey(Key(hiveAstroId, tinderId));
        }

        internal static void ClearAll()
        {
            GlobalSpeed = 0f;
            _perTinder.Clear();
        }

        internal static int OverrideCount { get { return _perTinder.Count; } }

        /// <summary>单颗火种的实际生效速度；0 表示不接管。</summary>
        internal static float Effective(int hiveAstroId, int tinderId)
        {
            float v;
            if (_perTinder.TryGetValue(Key(hiveAstroId, tinderId), out v) && v > 0f) return v;
            return GlobalSpeed > 0f ? GlobalSpeed : 0f;
        }

        /// <summary>立即把速度刷到所有正在飞行的火种上，返回受影响数量。</summary>
        internal static int ApplyNow(SpaceSector sector)
        {
            int n = 0;
            if (sector == null || sector.dfHives == null) return 0;
            for (int s = 0; s < sector.dfHives.Length; s++)
            {
                for (var hive = sector.dfHives[s]; hive != null; hive = hive.nextSibling)
                {
                    var pool = hive.tinders;
                    if (pool == null || pool.buffer == null) continue;
                    int cur = pool.cursor;
                    if (cur > pool.buffer.Length) cur = pool.buffer.Length;
                    for (int i = 1; i < cur; i++)
                    {
                        if (pool.buffer[i].id != i) continue;
                        if (pool.buffer[i].stage != 0) continue;
                        if (pool.buffer[i].targetHiveAstroId == 0) continue;
                        float v = Effective(hive.hiveAstroId, i);
                        if (v <= 0f) continue;
                        pool.buffer[i].uSpeed = v;
                        n++;
                    }
                }
            }
            return n;
        }

        /// <summary>每个 tick 的 TinderSailLogic 之后调用。必须写回真实数组元素，不能写副本。</summary>
        internal static void OnSailTick(ref DFTinderComponent tinder, SpaceSector sector)
        {
            if (sector == null) return;
            if (tinder.originHiveAstroId <= 0) return;

            // 抵达阶段（stage 既不是 254 在港、也不是 -1 起飞、也不是 0 飞行）→ 覆盖作废，
            // 免得火种回收后同一个池下标被新火种继承旧速度。
            if (tinder.stage != 0 && tinder.stage != 254 && tinder.stage != -1)
            {
                _perTinder.Remove(Key(tinder.originHiveAstroId, tinder.id));
                return;
            }

            if (tinder.stage != 0) return;
            if (tinder.targetHiveAstroId == 0) return;

            float v = Effective(tinder.originHiveAstroId, tinder.id);
            if (v <= 0f) return;

            var hive = sector.GetHiveByAstroId(tinder.originHiveAstroId);
            if (hive == null) return;

            var pool = hive.tinders;
            if (pool == null || pool.buffer == null) return;
            int cur = pool.cursor;
            if (cur > pool.buffer.Length) cur = pool.buffer.Length;
            for (int i = 1; i < cur; i++)
            {
                if (pool.buffer[i].id != tinder.id) continue;
                pool.buffer[i].uSpeed = v;
                return;
            }
        }

        /// <summary>列出全图所有火种，供界面显示。</summary>
        internal static void Collect(SpaceSector sector, List<TinderRow> into)
        {
            into.Clear();
            if (sector == null || sector.dfHives == null) return;

            for (int s = 0; s < sector.dfHives.Length; s++)
            {
                for (var hive = sector.dfHives[s]; hive != null; hive = hive.nextSibling)
                {
                    var pool = hive.tinders;
                    if (pool == null || pool.buffer == null) continue;
                    int cur = pool.cursor;
                    if (cur > pool.buffer.Length) cur = pool.buffer.Length;
                    for (int i = 1; i < cur; i++)
                    {
                        DFTinderComponent t = pool.buffer[i];
                        if (t.id != i) continue;

                        var row = new TinderRow();
                        row.HiveAstroId = hive.hiveAstroId;
                        row.TinderId = i;
                        row.From = LaunchOrders.DescribeHive(hive);
                        row.Stage = LaunchOrders.StageName(t);
                        row.Speed = t.uSpeed;
                        row.Overridden = _perTinder.ContainsKey(Key(hive.hiveAstroId, i));

                        if (t.targetHiveAstroId > 0)
                        {
                            var target = sector.GetHiveByAstroId(t.targetHiveAstroId);
                            row.Target = target != null ? LaunchOrders.DescribeHive(target) : ("astroId " + t.targetHiveAstroId);
                        }
                        else
                        {
                            row.Target = "—";
                        }

                        into.Add(row);
                    }
                }
            }
        }
    }

    /// <summary>每 tick 在 TinderSailLogic 之后把 uSpeed 钉回玩家设定值。</summary>
    [HarmonyPatch(typeof(DFTinderComponent), "TinderSailLogic")]
    internal static class Patch_TinderSailLogic
    {
        [HarmonyPostfix]
        internal static void Postfix(ref DFTinderComponent __instance, SpaceSector sector)
        {
            try
            {
                TinderSpeed.OnSailTick(ref __instance, sector);
            }
            catch (Exception e)
            {
                DarkFogDirectorPlugin.LogError("speed override failed (ignored this tick): " + e.Message);
            }
        }
    }
}
