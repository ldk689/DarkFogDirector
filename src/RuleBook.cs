using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DarkFogDirector
{
    /// <summary>一条扩张指令：源巢穴 -> 目标星系。</summary>
    internal sealed class Rule
    {
        public string RawSource;
        public string RawTarget;

        /// <summary>true 表示允许「强制起飞」（跳过原版的节奏/概率闸门，只要求火种装填完成）。</summary>
        public bool Force;

        public int StarIndex = -1;
        public int Orbit = -1;
        public int TargetStar = -1;

        public override string ToString()
        {
            return string.Format("{0} => {1}{2}", RawSource, RawTarget, Force ? " !" : string.Empty);
        }
    }

    /// <summary>
    /// 指令表。语法（UTF-8，一行一条，# 或 // 开头为注释）：
    ///
    ///   源 => 目标
    ///   源   = 星系索引 | 星系名 | 星系索引:轨道 | 星系名:轨道
    ///   目标 = 星系索引 | 星系名 | 巢穴 astroId
    ///   目标后加 ! 表示允许强制起飞
    ///
    /// 例：
    ///   5 => 12               星系 5 的所有巢穴，扩张目标锁定星系 12
    ///   Teldrar:0 => 12 !     星系 Teldrar 的 0 号轨道巢穴，立即向星系 12 强制发射
    ///   *#  => 3              注释
    /// </summary>
    internal sealed class RuleBook
    {
        private readonly List<Rule> _rules = new List<Rule>();
        private GalaxyData _resolvedFor;
        private string _path;

        internal string Path { get { return _path; } }

        internal IList<Rule> All { get { return _rules; } }

        internal void Load(string path)
        {
            _path = path;
            _rules.Clear();
            _resolvedFor = null;

            if (!File.Exists(path))
            {
                DarkFogDirectorPlugin.LogWarn("指令文件不存在，已按「不干预」运行：" + path);
                return;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(path, Encoding.UTF8);
            }
            catch (Exception e)
            {
                DarkFogDirectorPlugin.LogWarn("读取指令文件失败：" + e.Message);
                return;
            }

            int lineNo = 0;
            foreach (var raw in lines)
            {
                lineNo++;
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#") || line.StartsWith("//")) continue;

                int arrow = line.IndexOf("=>", StringComparison.Ordinal);
                if (arrow < 0)
                {
                    DarkFogDirectorPlugin.LogWarn(string.Format("第 {0} 行缺少 =>，已跳过：{1}", lineNo, line));
                    continue;
                }

                var rule = new Rule();
                rule.RawSource = line.Substring(0, arrow).Trim();
                string target = line.Substring(arrow + 2).Trim();

                if (target.EndsWith("!"))
                {
                    rule.Force = true;
                    target = target.Substring(0, target.Length - 1).Trim();
                }
                rule.RawTarget = target;

                if (rule.RawSource.Length == 0 || rule.RawTarget.Length == 0)
                {
                    DarkFogDirectorPlugin.LogWarn(string.Format("第 {0} 行源或目标为空，已跳过。", lineNo));
                    continue;
                }

                _rules.Add(rule);
            }

            DarkFogDirectorPlugin.LogInfo(string.Format("已载入 {0} 条扩张指令：{1}", _rules.Count, path));
        }

        /// <summary>名字 -> 索引：需要一个已加载的星系数据。</summary>
        internal void EnsureResolved(GalaxyData galaxy)
        {
            if (galaxy == null) return;
            if (ReferenceEquals(_resolvedFor, galaxy)) return;
            _resolvedFor = galaxy;

            foreach (var rule in _rules)
            {
                rule.StarIndex = -1;
                rule.Orbit = -1;
                rule.TargetStar = -1;

                string src = rule.RawSource;
                int colon = src.LastIndexOf(':');
                if (colon > 0)
                {
                    string orbitText = src.Substring(colon + 1).Trim();
                    src = src.Substring(0, colon).Trim();
                    int orbit;
                    if (int.TryParse(orbitText, NumberStyles.Integer, CultureInfo.InvariantCulture, out orbit))
                    {
                        rule.Orbit = orbit;
                    }
                }

                rule.StarIndex = ResolveStarOrAstro(src, galaxy);
                rule.TargetStar = ResolveStarOrAstro(rule.RawTarget, galaxy);

                if (rule.StarIndex < 0)
                {
                    DarkFogDirectorPlugin.LogWarn("无法识别源巢穴：" + rule.RawSource);
                }
                else if (rule.TargetStar < 0)
                {
                    DarkFogDirectorPlugin.LogWarn("无法识别目标星系：" + rule.RawTarget);
                }
            }
        }

        private static int ResolveStarOrAstro(string token, GalaxyData galaxy)
        {
            token = token.Trim();
            if (token.Length == 0) return -1;

            int number;
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            {
                if (number >= 0 && number < galaxy.stars.Length) return number;      // 星系索引
                if (number >= Director.AstroBase)                                    // 巢穴 astroId
                {
                    int idx = Director.ToStarIndex(number);
                    if (idx >= 0 && idx < galaxy.stars.Length) return idx;
                }
                return -1;
            }

            for (int i = 0; i < galaxy.stars.Length; i++)
            {
                var s = galaxy.stars[i];
                if (s == null) continue;
                if (string.Equals(s.name, token, StringComparison.OrdinalIgnoreCase)) return i;
                if (string.Equals(s.overrideName, token, StringComparison.OrdinalIgnoreCase)) return i;
                if (string.Equals(s.displayName, token, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        /// <summary>精确匹配（带轨道）优先于通配匹配。</summary>
        internal Rule Match(int starIndex, int orbit)
        {
            Rule exact = null;
            Rule wildcard = null;
            foreach (var rule in _rules)
            {
                if (rule.StarIndex != starIndex || rule.TargetStar < 0) continue;
                if (rule.Orbit >= 0 && rule.Orbit == orbit) exact = rule;
                else if (rule.Orbit < 0) wildcard = rule;
            }
            return exact ?? wildcard;
        }

        /// <summary>没有任何可用规则时，模组完全透明。</summary>
        internal bool HasResolvableRule()
        {
            foreach (var rule in _rules)
            {
                if (rule.StarIndex >= 0 && rule.TargetStar >= 0) return true;
            }
            return false;
        }
    }
}
