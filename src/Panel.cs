using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DarkFogDirector
{
    /// <summary>
    /// 游戏内操作面板（IMGUI）。两个页签：
    ///   「发射指挥」：选起点巢穴 + 目标星系，一键发射
    ///   「火种管理」：列出全图火种，可对单颗火种或全体接管飞行速度
    ///
    /// 用 IMGUI 而不是原生 uGUI/UIWindow：不依赖游戏自研的 UILayoutParser，
    /// 也能用系统字体直接渲染中/日文（Unity 内置 Arial 没有 CJK 字形）。
    /// Language: follows Localization.CurrentLanguageLCID and updates live.
    /// </summary>
    internal static class DirectorPanel
    {
        internal static bool Visible;

        private const float PanelWidth = 920f;
        private const float PanelHeight = 620f;
        private const float ColumnWidth = 440f;
        private const float ListHeight = 400f;
        private const float RowHeight = 24f;

        private struct HiveRow
        {
            public int HiveAstroId;
            public int StarIndex;
            public string StarName;
            public int Orbit;
            public int Tinders;
            public int Ready;
            public bool Pending;
        }

        private struct StarRow
        {
            public int StarIndex;
            public string Name;
            public int Hives;
            public int Capacity;
        }

        private static readonly List<HiveRow> _hives = new List<HiveRow>();
        private static readonly List<StarRow> _stars = new List<StarRow>();
        private static readonly List<TinderRow> _tinders = new List<TinderRow>();

        private static GUIStyle _title, _label, _row, _rowSel, _button, _tabOn, _tabOff, _statusStyle, _small, _field;
        private static Font _font;
        private static int _fontLang = -1;
        private static Texture2D _panelTex, _rowTex, _selTex, _btnTex;
        private static bool _stylesReady;

        private static Vector2 _srcScroll, _dstScroll, _tinderScroll;
        private static float _nextRefresh;
        private static string _status;
        private static int _selHive = -1;
        private static int _selStar = -1;
        private static long _selTinder = 0L;
        private static int _tab;
        private static string _speedText = "1200";

        internal static void Toggle()
        {
            Visible = !Visible;
            if (Visible)
            {
                L.Refresh();
                Refresh();
                _nextRefresh = Time.realtimeSinceStartup + 2f;
            }
        }

        // ── 样式 ───────────────────────────────────────────────────────────

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        private static Font BuildFont(int lang)
        {
            string[] names;
            if (lang == L.LangJa) names = new string[] { "Yu Gothic UI", "Meiryo", "MS Gothic", "Microsoft YaHei", "Arial" };
            else if (lang == L.LangZh) names = new string[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial" };
            else names = new string[] { "Segoe UI", "Microsoft YaHei", "Arial" };

            try { return Font.CreateDynamicFontFromOSFont(names, 15); }
            catch (Exception) { return null; }
        }

        private static void EnsureStyles()
        {
            int lang = L.Index;
            if (_stylesReady && _fontLang == lang) return;

            if (_font == null || _fontLang != lang)
            {
                _font = BuildFont(lang);
                _fontLang = lang;
            }

            if (!_stylesReady)
            {
                _panelTex = Solid(new Color(0.07f, 0.08f, 0.11f, 0.96f));
                _rowTex = Solid(new Color(0.16f, 0.18f, 0.24f, 0.95f));
                _selTex = Solid(new Color(0.95f, 0.55f, 0.16f, 0.95f));
                _btnTex = Solid(new Color(0.24f, 0.28f, 0.38f, 1f));

                _title = new GUIStyle();
                _title.fontSize = 20;
                _title.fontStyle = FontStyle.Bold;
                _title.normal.textColor = new Color(0.95f, 0.96f, 1f);
                _title.padding = new RectOffset(12, 12, 8, 8);

                _label = new GUIStyle();
                _label.fontSize = 14;
                _label.normal.textColor = new Color(0.75f, 0.79f, 0.88f);
                _label.padding = new RectOffset(10, 6, 4, 4);

                _small = new GUIStyle(_label);
                _small.fontSize = 12;
                _small.normal.textColor = new Color(0.62f, 0.66f, 0.76f);

                _row = new GUIStyle();
                _row.fontSize = 14;
                _row.alignment = TextAnchor.MiddleLeft;
                _row.padding = new RectOffset(10, 6, 3, 3);
                _row.normal.background = _rowTex;
                _row.hover.background = _btnTex;
                _row.active.background = _selTex;
                _row.normal.textColor = new Color(0.88f, 0.91f, 0.97f);
                _row.hover.textColor = Color.white;
                _row.active.textColor = Color.white;

                _rowSel = new GUIStyle(_row);
                _rowSel.normal.background = _selTex;
                _rowSel.normal.textColor = new Color(0.10f, 0.08f, 0.05f);

                _button = new GUIStyle();
                _button.fontSize = 15;
                _button.fontStyle = FontStyle.Bold;
                _button.alignment = TextAnchor.MiddleCenter;
                _button.normal.background = _btnTex;
                _button.hover.background = _selTex;
                _button.active.background = _selTex;
                _button.normal.textColor = Color.white;
                _button.hover.textColor = new Color(0.10f, 0.08f, 0.05f);
                _button.active.textColor = new Color(0.10f, 0.08f, 0.05f);

                _tabOn = new GUIStyle(_button);
                _tabOn.normal.background = _selTex;
                _tabOn.normal.textColor = new Color(0.10f, 0.08f, 0.05f);

                _tabOff = new GUIStyle(_button);
                _tabOff.fontStyle = FontStyle.Normal;
                _tabOff.normal.textColor = new Color(0.72f, 0.76f, 0.86f);

                _field = new GUIStyle(GUI.skin.textField);
                _field.fontSize = 14;
                _field.alignment = TextAnchor.MiddleLeft;

                _statusStyle = new GUIStyle(_label);
                _statusStyle.fontSize = 13;
                _statusStyle.wordWrap = false;
                _statusStyle.normal.textColor = new Color(0.98f, 0.78f, 0.42f);

                _stylesReady = true;
            }

            // 字体随语言切换
            _title.font = _font;
            _label.font = _font;
            _small.font = _font;
            _row.font = _font;
            _rowSel.font = _font;
            _button.font = _font;
            _tabOn.font = _font;
            _tabOff.font = _font;
            _field.font = _font;
            _statusStyle.font = _font;

            if (_status == null) _status = L.T("st_default");
        }

        // ── 数据 ───────────────────────────────────────────────────────────

        private static string StarName(StarData s)
        {
            if (s == null) return "?";
            if (!string.IsNullOrEmpty(s.displayName)) return s.displayName;
            return s.name;
        }

        internal static void Refresh()
        {
            _hives.Clear();
            _stars.Clear();

            var data = GameMain.data;
            var sector = data != null ? data.spaceSector : null;
            if (sector == null || sector.galaxy == null)
            {
                _status = L.T("st_not_in_game");
                return;
            }

            var stars = sector.galaxy.stars;
            for (int i = 0; i < stars.Length; i++)
            {
                int count = 0;
                for (var h = sector.dfHives[i]; h != null; h = h.nextSibling)
                {
                    count++;
                    var row = new HiveRow();
                    row.HiveAstroId = h.hiveAstroId;
                    row.StarIndex = i;
                    row.StarName = StarName(stars[i]);
                    row.Orbit = h.hiveOrbitIndex;
                    row.Tinders = LaunchOrders.CountTinders(h);
                    row.Ready = LaunchOrders.CountReadyTinders(h);
                    row.Pending = LaunchOrders.HasPending(h.hiveAstroId);
                    _hives.Add(row);
                }

                int cap = LaunchOrders.OrbitCapacity(stars[i]);
                if (cap > 0 && count < cap)
                {
                    var sr = new StarRow();
                    sr.StarIndex = i;
                    sr.Name = StarName(stars[i]);
                    sr.Hives = count;
                    sr.Capacity = cap;
                    _stars.Add(sr);
                }
            }

            TinderSpeed.Collect(sector, _tinders);

            bool hiveOk = false;
            foreach (var h in _hives) { if (h.HiveAstroId == _selHive) { hiveOk = true; break; } }
            if (!hiveOk) _selHive = -1;

            bool starOk = false;
            foreach (var s in _stars) { if (s.StarIndex == _selStar) { starOk = true; break; } }
            if (!starOk) _selStar = -1;

            bool tinderOk = false;
            foreach (var t in _tinders)
            {
                if (TinderSpeed.Key(t.HiveAstroId, t.TinderId) == _selTinder) { tinderOk = true; break; }
            }
            if (!tinderOk) _selTinder = 0L;
        }

        private static SpaceSector Sector()
        {
            var data = GameMain.data;
            return data != null ? data.spaceSector : null;
        }

        private static TinderRow SelectedTinder()
        {
            foreach (var t in _tinders)
            {
                if (TinderSpeed.Key(t.HiveAstroId, t.TinderId) == _selTinder) return t;
            }
            return new TinderRow();
        }

        private static bool ParseSpeed(out float value)
        {
            return float.TryParse(_speedText.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static void DoLaunch()
        {
            var sector = Sector();
            if (sector == null) { _status = L.T("st_not_in_game"); return; }
            if (_selHive < 0) { _status = L.T("st_pick_hive"); return; }
            if (_selStar < 0) { _status = L.T("st_pick_star"); return; }

            _status = LaunchOrders.RequestLaunch(sector, _selHive, _selStar);
            Refresh();
        }

        private static void ApplySpeedToSelected()
        {
            float v;
            if (!ParseSpeed(out v)) { _status = L.T("st_speed_bad"); return; }
            if (v <= 0f) { _status = L.T("st_speed_positive"); return; }
            if (_selTinder == 0L) { _status = L.T("st_pick_tinder"); return; }

            var t = SelectedTinder();
            TinderSpeed.SetTinder(t.HiveAstroId, t.TinderId, v);
            var sector = Sector();
            int n = sector != null ? TinderSpeed.ApplyNow(sector) : 0;
            _status = L.T("st_speed_one", v, n);
            Refresh();
        }

        private static void ApplySpeedToAll()
        {
            float v;
            if (!ParseSpeed(out v)) { _status = L.T("st_speed_bad"); return; }
            if (v <= 0f) { _status = L.T("st_speed_positive"); return; }

            TinderSpeed.GlobalSpeed = v;
            var sector = Sector();
            int n = sector != null ? TinderSpeed.ApplyNow(sector) : 0;
            _status = L.T("st_speed_applied", v, n);
            Refresh();
        }

        // ── 绘制 ───────────────────────────────────────────────────────────

        internal static void OnGUI()
        {
            if (!Visible) return;

            try
            {
                EnsureStyles();

                if (Time.realtimeSinceStartup >= _nextRefresh)
                {
                    Refresh();
                    _nextRefresh = Time.realtimeSinceStartup + 2f;
                }

                float w = PanelWidth;
                float h = PanelHeight;
                var area = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

                GUILayout.BeginArea(area);
                GUI.DrawTexture(new Rect(0f, 0f, w, h), _panelTex);

                GUILayout.BeginVertical();

                GUILayout.Label(L.T("title"), _title);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(L.T("tab_launch"), _tab == 0 ? _tabOn : _tabOff, GUILayout.Width(110f), GUILayout.Height(30f))) _tab = 0;
                if (GUILayout.Button(L.T("tab_tinders"), _tab == 1 ? _tabOn : _tabOff, GUILayout.Width(110f), GUILayout.Height(30f))) _tab = 1;
                GUILayout.Label(L.T("header", "\\", _hives.Count, _stars.Count, _tinders.Count, LaunchOrders.PendingCount), _small);
                GUILayout.EndHorizontal();

                GUILayout.Space(4f);
                if (_tab == 0) DrawLaunchTab();
                else DrawTinderTab();

                GUILayout.Space(6f);
                GUILayout.Label(_status, _statusStyle);

                GUILayout.EndVertical();
                GUILayout.EndArea();
            }
            catch (Exception e)
            {
                DarkFogDirectorPlugin.LogError("panel draw failed: " + e.Message);
                Visible = false;
            }
        }

        private static void DrawLaunchTab()
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(ColumnWidth));
            GUILayout.Label(L.T("src_header"), _label);
            _srcScroll = GUILayout.BeginScrollView(_srcScroll, GUILayout.Width(ColumnWidth - 10f), GUILayout.Height(ListHeight));
            if (_hives.Count == 0)
            {
                GUILayout.Label(L.T("src_empty"), _small);
            }
            for (int i = 0; i < _hives.Count; i++)
            {
                var row = _hives[i];
                string text = L.T("hive_row", row.StarName, row.StarIndex, row.Orbit, row.Tinders, row.Ready);
                if (row.Pending) text += "   [" + L.T("pending_tag") + "]";
                if (GUILayout.Button(text, row.HiveAstroId == _selHive ? _rowSel : _row, GUILayout.Height(RowHeight)))
                {
                    _selHive = row.HiveAstroId;
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(10f);

            GUILayout.BeginVertical(GUILayout.Width(ColumnWidth));
            GUILayout.Label(L.T("dst_header"), _label);
            _dstScroll = GUILayout.BeginScrollView(_dstScroll, GUILayout.Width(ColumnWidth - 10f), GUILayout.Height(ListHeight));
            if (_stars.Count == 0)
            {
                GUILayout.Label(L.T("dst_empty"), _small);
            }
            for (int i = 0; i < _stars.Count; i++)
            {
                var row = _stars[i];
                string text = L.T("star_row", row.Name, row.StarIndex, row.Hives, row.Capacity);
                if (GUILayout.Button(text, row.StarIndex == _selStar ? _rowSel : _row, GUILayout.Height(RowHeight)))
                {
                    _selStar = row.StarIndex;
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(L.T("btn_launch"), _button, GUILayout.Width(130f), GUILayout.Height(38f)))
            {
                DoLaunch();
            }
            if (GUILayout.Button(L.T("btn_refresh"), _button, GUILayout.Width(95f), GUILayout.Height(38f)))
            {
                Refresh();
                _status = L.T("st_refreshed");
            }
            if (_selHive >= 0 && LaunchOrders.HasPending(_selHive))
            {
                if (GUILayout.Button(L.T("btn_cancel"), _button, GUILayout.Width(120f), GUILayout.Height(38f)))
                {
                    _status = LaunchOrders.CancelPending(_selHive);
                    Refresh();
                }
            }
            if (GUILayout.Button(L.T("btn_close"), _button, GUILayout.Width(95f), GUILayout.Height(38f)))
            {
                Visible = false;
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawTinderTab()
        {
            GUILayout.Label(L.T("tinder_header"), _label);
            _tinderScroll = GUILayout.BeginScrollView(_tinderScroll, GUILayout.Width(PanelWidth - 40f), GUILayout.Height(ListHeight));
            if (_tinders.Count == 0)
            {
                GUILayout.Label(L.T("tinder_empty"), _small);
            }
            for (int i = 0; i < _tinders.Count; i++)
            {
                var row = _tinders[i];
                long key = TinderSpeed.Key(row.HiveAstroId, row.TinderId);
                string text = L.T("tinder_row", row.From, row.Target, row.Stage, row.Speed);
                if (row.Overridden) text += "  *";
                if (GUILayout.Button(text, key == _selTinder ? _rowSel : _row, GUILayout.Height(RowHeight)))
                {
                    _selTinder = key;
                    var t = SelectedTinder();
                    _speedText = ((int)t.Speed).ToString(CultureInfo.InvariantCulture);
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(L.T("speed_label"), _label, GUILayout.Width(120f));
            _speedText = GUILayout.TextField(_speedText, _field, GUILayout.Width(110f), GUILayout.Height(28f));
            if (GUILayout.Button(L.T("btn_apply_one"), _button, GUILayout.Width(110f), GUILayout.Height(30f)))
            {
                ApplySpeedToSelected();
            }
            if (GUILayout.Button(L.T("btn_apply_all"), _button, GUILayout.Width(110f), GUILayout.Height(30f)))
            {
                ApplySpeedToAll();
            }
            if (GUILayout.Button(L.T("btn_restore"), _button, GUILayout.Width(120f), GUILayout.Height(30f)))
            {
                TinderSpeed.ClearAll();
                _status = L.T("st_restored");
                Refresh();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label(string.Format(L.T("speed_state"), TinderSpeed.VanillaMaxSpeed,
                TinderSpeed.GlobalSpeed > 0f ? L.T("speed_override", TinderSpeed.GlobalSpeed) : L.T("speed_off")), _small);

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(L.T("btn_refresh"), _button, GUILayout.Width(95f), GUILayout.Height(34f)))
            {
                Refresh();
                _status = L.T("st_refreshed");
            }
            if (GUILayout.Button(L.T("btn_close"), _button, GUILayout.Width(95f), GUILayout.Height(34f)))
            {
                Visible = false;
            }
            GUILayout.EndHorizontal();
        }
    }
}
