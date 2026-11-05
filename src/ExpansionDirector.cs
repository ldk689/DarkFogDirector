using System;
using System.Collections.Generic;
using System.Text;

namespace DarkFogDirector
{
    /// <summary>
    /// 黑雾（Dark Fog）跨星系扩张的「目标星系」定向器。
    ///
    /// 原理（已对 Assembly-CSharp 0.10.35.29151 反汇编确认）：
    ///   黑雾巢穴在 EnemyDFHiveSystem.KeyTickLogic 里，对每个空闲火种
    ///   （stage==254 且 targetHiveAstroId==0 且 direction==0）调用
    ///   DFTinderComponent.PrepareDispatchLogic(hive)。
    ///   该方法选定目标时先调用 GenerateSortedStarIndices(sector) 给所有星系打分排序，
    ///   再用 RandomTable 做加权随机抽出目标星系，最后 DispatchFromHive(sector, targetHiveAstroId)。
    ///
    /// 本类在 GenerateSortedStarIndices 之后改写权重数组，使玩家指定的星系必然被抽中；
    /// 其余原版逻辑（新巢创建、轨道选择、物质装载、节奏与概率闸门）全部保留。
    ///
    /// 注意：DFTinderComponent 是 struct，所有入口都用 ref 传递，避免改动副本。
    /// </summary>
    internal static class Director
    {
        internal const int AstroBase = 1000000;
        internal const int OrbitsPerStar = 8;

        /// <summary>hiveAstroId = 1000000 + starIndex * 8 + orbit + 1（由 SpaceSector.TryCreateNewHive 反推）</summary>
        internal static int ToStarIndex(int hiveAstroId)
        {
            return (hiveAstroId - AstroBase - 1) / OrbitsPerStar;
        }

        internal static int ToOrbit(int hiveAstroId)
        {
            return (hiveAstroId - AstroBase - 1) % OrbitsPerStar;
        }

        internal static int ToAstroId(int starIndex, int orbit)
        {
            return AstroBase + starIndex * OrbitsPerStar + orbit + 1;
        }

        internal static readonly RuleBook Rules = new RuleBook();

        /// <summary>由 GenerateSortedStarIndices 的 Postfix 调用。</summary>
        internal static void OnStarIndicesGenerated(ref DFTinderComponent tinder, SpaceSector sector)
        {
            if (sector == null || sector.galaxy == null) return;
            if (tinder.originHiveAstroId <= 0) return;

            var origin = sector.GetHiveByAstroId(tinder.originHiveAstroId);
            if (origin == null || origin.starData == null) return;

            Rules.EnsureResolved(sector.galaxy);

            Rule rule = Rules.Match(origin.starData.index, origin.hiveOrbitIndex);
            if (rule == null) return;

            ApplyTarget(ref tinder, sector, origin, rule);
        }

        private static void ApplyTarget(ref DFTinderComponent tinder, SpaceSector sector, EnemyDFHiveSystem origin, Rule rule)
        {
            int target = rule.TargetStar;
            var stars = sector.galaxy.stars;
            if (target < 0 || target >= stars.Length)
            {
                DarkFogDirectorPlugin.LogWarn("规则目标星系越界，已忽略：" + rule);
                return;
            }

            var blank = tinder.starBlankOrbitCount;
            var indices = tinder.sortedStarIndices;
            var values = tinder.starValues;
            if (blank == null || indices == null || values == null) return;
            if (indices.Length != values.Length || target >= blank.Length) return;
            int n = indices.Length;
            if (n == 0) return;

            // 目标星系巢穴已满（原版每星系最多 8 个轨道），无法再接收火种，交还原版随机。
            if (blank[target] <= 0)
            {
                DarkFogDirectorPlugin.LogInfo(string.Format(
                    "目标星系 {0} 已无空余巢穴轨道，本次沿用原版目标。", Describe(sector, target)));
                return;
            }

            // 原版抽取公式：r = rtseed.Integer(总权重 / 2); r = r * r / (总权重 / 2);
            // 再取第一个满足 r < 累积权重 的下标。把目标星系放到首位并给压倒性权重即可 100% 命中。
            const int Dominate = 200000;
            indices[0] = target;
            values[0] = Dominate;
            for (int i = 1; i < n; i++)
            {
                values[i] = values[i - 1] + 1;
            }

            DarkFogDirectorPlugin.LogDebug(string.Format(
                "火种 {0}：源巢穴 {1}（星系 {2} 轨道 {3}）-> 目标 {4}",
                tinder.id, origin.hiveAstroId, origin.starData.index, origin.hiveOrbitIndex, Describe(sector, target)));
        }

        /// <summary>
        /// 强制起飞入口：规则带 ! 且冷却已过时，直接发射一个已装填完成的火种。
        /// 返回 true 表示已接管，原版 PrepareDispatchLogic 不应再执行。
        /// </summary>
        internal static bool TryForceLaunch(ref DFTinderComponent tinder, EnemyDFHiveSystem hive, int cooldownTicks)
        {
            if (hive == null || hive.sector == null || hive.sector.galaxy == null) return false;
            if (hive.starData == null) return false;

            Rules.EnsureResolved(hive.sector.galaxy);
            Rule rule = Rules.Match(hive.starData.index, hive.hiveOrbitIndex);
            if (rule == null || !rule.Force) return false;

            // 关键：不依赖 Harmony 传进来的 ref 副本，直接在巢穴的火种池数组元素上操作，
            // 保证 DispatchFromHive 改的是真正在用的那个结构体。
            var pool = hive.tinders;
            if (pool == null || pool.buffer == null) return false;
            int slot = -1;
            for (int i = 1; i < pool.cursor; i++)
            {
                if (pool.buffer[i].id == tinder.id) { slot = i; break; }
            }
            if (slot < 0) return false;

            // 火种必须已装填完成（与原版判定一致），否则飞过去也不携带物质。
            if (!IsTinderLoaded(hive, slot)) return false;

            int last;
            if (ForceClock.TryGetValue(hive.hiveAstroId, out last))
            {
                if (hive.ticks - last < cooldownTicks) return false;
            }

            int astroId = EnsureHiveInStar(hive.sector, rule.TargetStar);
            if (astroId <= 0) return false;

            pool.buffer[slot].DispatchFromHive(hive.sector, astroId);
            ForceClock[hive.hiveAstroId] = hive.ticks;

            DarkFogDirectorPlugin.LogInfo(string.Format(
                "强制发射：巢穴 {0} -> {1}（目标 astroId {2}）",
                hive.hiveAstroId, Describe(hive.sector, rule.TargetStar), astroId));
            return true;
        }

        private static readonly Dictionary<int, int> ForceClock = new Dictionary<int, int>();

        private static bool IsTinderLoaded(EnemyDFHiveSystem hive, int slot)
        {
            int id = hive.tinders.buffer[slot].builderId;
            if (id <= 0) return false;
            var pool = hive.builders;
            if (pool == null || pool.buffer == null) return false;
            if (id >= pool.cursor) return false;
            EnemyBuilderComponent b = pool.buffer[id];
            return b.matter >= b.maxMatter && b.energy >= b.maxEnergy;
        }

        /// <summary>确保目标星系至少存在一个巢穴，返回其 astroId。</summary>
        internal static int EnsureHiveInStar(SpaceSector sector, int starIndex)
        {
            if (sector == null || sector.galaxy == null) return -1;
            if (starIndex < 0 || starIndex >= sector.galaxy.stars.Length) return -1;

            var head = sector.dfHives[starIndex];
            if (head != null)
            {
                // 取链尾（该星系最新建立的巢穴）作为接收方
                var cur = head;
                var last = head;
                while (cur != null)
                {
                    last = cur;
                    cur = cur.nextSibling;
                }
                return last.hiveAstroId;
            }

            var created = sector.TryCreateNewHive(sector.galaxy.stars[starIndex]);
            if (created == null) return -1;
            return created.hiveAstroId;
        }

        internal static string Describe(SpaceSector sector, int starIndex)
        {
            if (sector == null || sector.galaxy == null) return "#" + starIndex;
            var stars = sector.galaxy.stars;
            if (starIndex < 0 || starIndex >= stars.Length) return "#" + starIndex;
            var s = stars[starIndex];
            string name = string.IsNullOrEmpty(s.displayName) ? s.name : s.displayName;
            return string.Format("{0}(#{1})", name, starIndex);
        }

        /// <summary>把当前星图与黑雾巢穴清单导出，方便玩家编写规则。</summary>
        internal static string BuildDump(SpaceSector sector)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DarkFogDirector 现场快照");
            if (sector == null || sector.galaxy == null)
            {
                sb.AppendLine("(未进入游戏，无法读取星图)");
                return sb.ToString();
            }

            var stars = sector.galaxy.stars;
            sb.AppendLine("# 星系清单：索引 | 名称 | 类型 | 已建巢穴数");
            for (int i = 0; i < stars.Length; i++)
            {
                int count = 0;
                for (var h = sector.dfHives[i]; h != null; h = h.nextSibling) count++;
                sb.AppendLine(string.Format("{0} | {1} | {2} | {3}",
                    i, stars[i].displayName, stars[i].type, count));
            }

            sb.AppendLine();
            sb.AppendLine("# 巢穴清单：astroId | 星系索引 | 轨道 | 已实体化");
            for (int i = 0; i < sector.dfHives.Length; i++)
            {
                for (var h = sector.dfHives[i]; h != null; h = h.nextSibling)
                {
                    sb.AppendLine(string.Format("{0} | {1} | {2} | {3}",
                        h.hiveAstroId, i, h.hiveOrbitIndex, h.realized));
                }
            }

            sb.AppendLine();
            sb.AppendLine("# 规则写法：  源 => 目标     源可为 星系索引 / 星系名 / 星系索引:轨道");
            sb.AppendLine("#             目标后加 ! 表示允许强制起飞");
            return sb.ToString();
        }
    }
}
