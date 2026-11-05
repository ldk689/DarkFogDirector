using System;
using System.Collections.Generic;

namespace DarkFogDirector
{
    /// <summary>一次「立即发射」指令。</summary>
    internal sealed class LaunchOrder
    {
        public int SourceHiveAstroId;
        public int TargetStar;
    }

    /// <summary>
    /// UI 下发的显式指令：立刻让某个巢穴的火种飞向某个星系的**新建**巢穴。
    ///
    /// 三条硬性规则：
    ///  1. 目标一律新建（SpaceSector.TryCreateNewHive），已存在的巢穴永远不作为终点。
    ///     新巢穴落在哪个轨道由游戏自己挑空位，绝不人工指定（避免坏档）。
    ///  2. 发射前把火种的建造器资源补满（matter/energy/sp），等价于原版
    ///     "从母巢调拨到满"的结果，省掉等待生产的时间。
    ///  3. 目标星系的容量用游戏自己的 StarData.maxHiveCount（普通星 3、黑洞/中子星 6）。
    /// </summary>
    internal static class LaunchOrders
    {
        internal const int OrbitsPerStar = 8;
        internal const int TinderIdleStage = 254;
        internal const int TinderSailingStage = 0;

        private static readonly Dictionary<int, LaunchOrder> _pending = new Dictionary<int, LaunchOrder>();

        internal static int PendingCount { get { return _pending.Count; } }

        internal static bool HasPending(int hiveAstroId)
        {
            return _pending.ContainsKey(hiveAstroId);
        }

        internal static string CancelPending(int hiveAstroId)
        {
            return _pending.Remove(hiveAstroId) ? L.T("cancel_ok") : L.T("cancel_none");
        }

        // ── 容量 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 每星系允许的巢穴数。游戏里 StarData.maxHiveCount 就是这个值
        /// （普通星系 3，黑洞/中子星 6），为 0 时按星型兜底。
        /// </summary>
        internal static int OrbitCapacity(StarData star)
        {
            if (star == null) return 0;
            int cap = star.maxHiveCount;
            if (cap <= 0)
            {
                cap = (star.type == EStarType.BlackHole || star.type == EStarType.NeutronStar) ? 6 : 3;
            }
            if (cap > OrbitsPerStar) cap = OrbitsPerStar;
            return cap;
        }

        internal static int HiveCountInStar(SpaceSector sector, int starIndex)
        {
            if (sector == null || sector.dfHives == null) return 0;
            if (starIndex < 0 || starIndex >= sector.dfHives.Length) return 0;
            int count = 0;
            for (var h = sector.dfHives[starIndex]; h != null; h = h.nextSibling) count++;
            return count;
        }

        internal static int FreeOrbits(SpaceSector sector, int starIndex)
        {
            if (sector == null || sector.galaxy == null) return 0;
            var stars = sector.galaxy.stars;
            if (starIndex < 0 || starIndex >= stars.Length) return 0;
            int free = OrbitCapacity(stars[starIndex]) - HiveCountInStar(sector, starIndex);
            return free < 0 ? 0 : free;
        }

        // ── 火种查询 ───────────────────────────────────────────────────────

        internal static int CountTinders(EnemyDFHiveSystem hive)
        {
            int n = 0;
            EachTinder(hive, delegate(int slot, DFTinderComponent t) { n++; return true; });
            return n;
        }

        internal static int CountReadyTinders(EnemyDFHiveSystem hive)
        {
            int n = 0;
            EachTinder(hive, delegate(int slot, DFTinderComponent t)
            {
                if (t.stage == TinderIdleStage && t.targetHiveAstroId == 0 && t.direction == 0) n++;
                return true;
            });
            return n;
        }

        private delegate bool TinderVisitor(int slot, DFTinderComponent tinder);

        private static void EachTinder(EnemyDFHiveSystem hive, TinderVisitor visit)
        {
            if (hive == null || hive.tinders == null || hive.tinders.buffer == null) return;
            var buffer = hive.tinders.buffer;
            int cur = hive.tinders.cursor;
            if (cur > buffer.Length) cur = buffer.Length;
            for (int i = 1; i < cur; i++)
            {
                DFTinderComponent t = buffer[i];
                if (t.id != i) continue;
                if (!visit(i, t)) return;
            }
        }

        private static int FindIdleSlot(EnemyDFHiveSystem hive)
        {
            if (hive == null || hive.tinders == null || hive.tinders.buffer == null) return -1;
            var buffer = hive.tinders.buffer;
            int cur = hive.tinders.cursor;
            if (cur > buffer.Length) cur = buffer.Length;
            for (int i = 1; i < cur; i++)
            {
                DFTinderComponent t = buffer[i];
                if (t.id != i) continue;
                if (t.stage != TinderIdleStage) continue;
                if (t.targetHiveAstroId != 0) continue;
                if (t.direction != 0) continue;
                return i;
            }
            return -1;
        }

        private static int FindSlot(EnemyDFHiveSystem hive, int tinderId)
        {
            if (hive == null || hive.tinders == null || hive.tinders.buffer == null) return -1;
            var buffer = hive.tinders.buffer;
            int cur = hive.tinders.cursor;
            if (cur > buffer.Length) cur = buffer.Length;
            for (int i = 1; i < cur; i++)
            {
                if (buffer[i].id == tinderId) return i;
            }
            return -1;
        }

        // ── 资源补齐 ───────────────────────────────────────────────────────

        /// <summary>
        /// 把某颗火种的建造器补到满。原版 PrepareDispatchLogic 会从母巢的建造器
        /// 一点一点调拨，直到 matter/energy 都到上限才允许发射；这里直接写满，
        /// 效果等同于"资源已经准备好了"，不额外创造新的经济来源。
        /// </summary>
        internal static bool FillResources(EnemyDFHiveSystem hive, int builderId)
        {
            var pool = hive != null ? hive.builders : null;
            if (pool == null || pool.buffer == null) return false;
            if (builderId <= 0 || builderId >= pool.cursor || builderId >= pool.buffer.Length) return false;

            EnemyBuilderComponent b = pool.buffer[builderId];
            if (b.maxMatter > 0) b.matter = b.maxMatter;
            if (b.maxEnergy > 0) b.energy = b.maxEnergy;
            if (b.spMax > 0) b.sp = b.spMax;
            pool.buffer[builderId] = b;
            return true;
        }

        // ── 下达指令 ───────────────────────────────────────────────────────

        internal static string RequestLaunch(SpaceSector sector, int hiveAstroId, int targetStar)
        {
            if (sector == null || sector.galaxy == null) return L.T("lg_not_in_game");

            var hive = sector.GetHiveByAstroId(hiveAstroId);
            if (hive == null) return L.T("lg_hive_gone");

            if (FreeOrbits(sector, targetStar) <= 0)
            {
                return L.T("lg_cap_full",
                    Director.Describe(sector, targetStar));
            }

            // 1) 池里有空闲火种 → 补满资源直接发射
            int slot = FindIdleSlot(hive);
            if (slot >= 0) return DispatchSlot(sector, hive, slot, targetStar, L.T("how_filled"));

            // 2) 池里没有空闲火种 → 走游戏自己的建造通道当场造一个，再造完立刻发射。
            //    这条路径和原版 DFSCoreComponent 造火种完全一样（AddTinderDeferred → ExecuteDeferredEnemyChange
            //    → CreateEnemyFinal(hive, PROTO_TINDER, hiveAstroId, dock.pos, dock.rot) → AddIdleTinder），
            //    不是手工拼一个实体出来。
            int spawned = SpawnTinderNow(hive);
            if (spawned >= 0) return DispatchSlot(sector, hive, spawned, targetStar, L.T("how_spawned"));

            // 3) 还是不行（例如没有火种停靠位），退回待发，等核心自己造
            DarkFogDirectorPlugin.LogWarn(string.Format(
                "hive {0}: cannot launch now (realized={1} tinderDocks={2} tinders.cursor={3} proto={4})",
                hive.hiveAstroId, hive.realized,
                hive.tinderDocks != null ? hive.tinderDocks.Length : -1,
                hive.tinders != null ? hive.tinders.cursor : -1,
                EnemyDFHiveSystem.PROTO_TINDER));

            var order = new LaunchOrder();
            order.SourceHiveAstroId = hiveAstroId;
            order.TargetStar = targetStar;
            _pending[hiveAstroId] = order;
            return L.T("lg_pending",
                DescribeHive(hive), Director.Describe(sector, targetStar));
        }

        /// <summary>每次原版准备派火种时先看有没有待发指令；有就补满资源立刻发掉。</summary>
        internal static bool TryConsume(ref DFTinderComponent tinder, EnemyDFHiveSystem hive)
        {
            if (_pending.Count == 0) return false;
            if (hive == null || hive.sector == null) return false;

            LaunchOrder order;
            if (!_pending.TryGetValue(hive.hiveAstroId, out order)) return false;

            int slot = FindSlot(hive, tinder.id);
            if (slot < 0) return false;

            int astroId = CreateHiveInStar(hive.sector, order.TargetStar);
            if (astroId <= 0)
            {
                _pending.Remove(hive.hiveAstroId);
                DarkFogDirectorPlugin.LogWarn(string.Format(
                    "queued order dropped: target star {0} is at its hive cap.",
                    Director.Describe(hive.sector, order.TargetStar)));
                return false;
            }

            FillResources(hive, hive.tinders.buffer[slot].builderId);
            hive.tinders.buffer[slot].DispatchFromHive(hive.sector, astroId);
            _pending.Remove(hive.hiveAstroId);

            DarkFogDirectorPlugin.LogInfo(string.Format(
                "queued order executed: hive {0} -> {1} (astroId {2})",
                hive.hiveAstroId, Director.Describe(hive.sector, order.TargetStar), astroId));
            return true;
        }

        /// <summary>把某个槽位的火种补满资源并发射出去。失败返回原因文本。</summary>
        private static string DispatchSlot(SpaceSector sector, EnemyDFHiveSystem hive, int slot, int targetStar, string how)
        {
            if (slot < 0) return L.T("lg_none");

            int astroId = CreateHiveInStar(sector, targetStar);
            if (astroId <= 0) return L.T("lg_create_fail");

            FillResources(hive, hive.tinders.buffer[slot].builderId);
            hive.tinders.buffer[slot].DispatchFromHive(sector, astroId);

            return L.T("lg_launched",
                how, DescribeHive(hive), Director.Describe(sector, targetStar));
        }

        /// <summary>
        /// 立刻在巢穴里造一个火种并返回它的池下标。
        /// 用的是游戏自己的延迟建造通道：AddTinderDeferred 只是把 dock 下标塞进 HashSet，
        /// 真正创建在 ExecuteDeferredEnemyChange 里；这里手动结算一次，就不必等下一个 tick。
        /// </summary>
        internal static int SpawnTinderNow(EnemyDFHiveSystem hive)
        {
            if (hive == null) return -1;
            var docks = hive.tinderDocks;
            if (docks == null || docks.Length == 0) return -1;

            // 原版 DFSCoreComponent.LogicTick 的闸门：
            //     if (hive.idleTinderCount >= hive.tinderDocks.Length) return;
            // 空闲火种数达到停靠位数就不再造。我们必须沿用这条，否则会把
            // idleTinderIds（EnemyDFHiveSystem.Init 里固定 new int[16]）顶爆。
            if (hive.idleTinderCount >= docks.Length) return -1;

            // 二道保险：AddIdleTinder 没有任何越界检查，17 个空闲火种就会
            // IndexOutOfRangeException，而且是在游戏自己的 tick 里抛，会一直刷。
            if (hive.idleTinderIds != null && hive.idleTinderCount >= hive.idleTinderIds.Length)
            {
                DarkFogDirectorPlugin.LogWarn(string.Format(
                    "hive {0}: idle tinder list is full ({1}/{2}), refusing to spawn more.",
                    hive.hiveAstroId, hive.idleTinderCount, hive.idleTinderIds.Length));
                return -1;
            }

            try
            {
                hive.AddTinderDeferred(0);            // 0 is taken modulo tinderDocks.Length inside
                hive.ExecuteDeferredEnemyChange();    // resolve immediately so the tinder really exists
            }
            catch (Exception e)
            {
                DarkFogDirectorPlugin.LogWarn("spawning tinder on demand failed: " + e.Message);
                return -1;
            }

            return FindIdleSlot(hive);
        }

        // ── 内部 ───────────────────────────────────────────────────────────

        private static int CreateHiveInStar(SpaceSector sector, int starIndex)
        {
            if (sector == null || sector.galaxy == null) return -1;
            var stars = sector.galaxy.stars;
            if (starIndex < 0 || starIndex >= stars.Length) return -1;

            // 让游戏自己挑空轨道，不人工指定，避免与存档里的轨道数据不一致。
            var created = sector.TryCreateNewHive(stars[starIndex]);
            if (created == null) return -1;
            return created.hiveAstroId;
        }

        internal static string DescribeHive(EnemyDFHiveSystem hive)
        {
            if (hive == null) return "?";
            string star = hive.starData != null
                ? (string.IsNullOrEmpty(hive.starData.displayName) ? hive.starData.name : hive.starData.displayName)
                : "?";
            return L.T("hive_desc", star, hive.hiveOrbitIndex);
        }

        /// <summary>把火种的 stage 翻译成人能看的状态。</summary>
        internal static string StageName(DFTinderComponent t)
        {
            if (t.stage == TinderIdleStage) return L.T("stage_docked");
            if (t.stage == -1) return L.T("stage_takeoff");
            if (t.stage == TinderSailingStage) return L.T("stage_sailing");
            return L.T("stage_other", t.stage);
        }
    }
}
