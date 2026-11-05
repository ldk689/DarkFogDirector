using System;
using System.Collections.Generic;

namespace DarkFogDirector
{
    /// <summary>
    /// 界面多语言。跟随游戏内语言设置（Localization.CurrentLanguageLCID）。
    ///
    /// 游戏当前发行 5 种语言：简体中文 2052 / English 1033 / Français 1036 / Deutsch 1031 / 日本語 1041。
    /// 其它 LCID（西/韩/俄）目前没有语言文件，一律回落到英文。
    /// </summary>
    internal static class L
    {
        internal const int LangZh = 0;
        internal const int LangEn = 1;
        internal const int LangFr = 2;
        internal const int LangDe = 3;
        internal const int LangJa = 4;

        private static int _index = LangEn;
        private static int _detectedFor = int.MinValue;

        private static Dictionary<string, string[]> _table;

        /// <summary>当前语言下标（0 中 / 1 英 / 2 法 / 3 德 / 4 日）。</summary>
        internal static int Index
        {
            get
            {
                Refresh();
                return _index;
            }
        }

        /// <summary>每次开界面/每次绘制前调用，切换语言后能立刻生效。</summary>
        internal static void Refresh()
        {
            int lcid = 0;
            try { lcid = Localization.CurrentLanguageLCID; }
            catch (Exception) { lcid = 0; }

            if (lcid == _detectedFor) return;
            _detectedFor = lcid;

            try
            {
                if (lcid == Localization.LCID_ZHCN) _index = LangZh;
                else if (lcid == Localization.LCID_FRFR) _index = LangFr;
                else if (lcid == Localization.LCID_DEDE) _index = LangDe;
                else if (lcid == Localization.LCID_JAJA) _index = LangJa;
                else _index = LangEn;   // 1033 及其它一律英文
            }
            catch (Exception)
            {
                _index = LangEn;
            }
        }

        /// <summary>取词条；缺词条时返回 key 本身（方便在界面上直接看出漏翻）。</summary>
        internal static string T(string key)
        {
            Refresh();
            if (_table == null) BuildTable();
            string[] row;
            if (_table.TryGetValue(key, out row))
            {
                if (_index >= 0 && _index < row.Length) return row[_index];
                if (row.Length > LangEn) return row[LangEn];
            }
            return key;
        }

        internal static string T(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        private static void Add(string key, string zh, string en, string fr, string de, string ja)
        {
            _table[key] = new string[] { zh, en, fr, de, ja };
        }

        private static void BuildTable()
        {
            _table = new Dictionary<string, string[]>(StringComparer.Ordinal);

            // ── 面板框架 ───────────────────────────────────────────────────
            Add("title", "黑雾扩张指挥", "Dark Fog Director", "Directeur du Brouillard Sombre",
                "Dunkelnebel-Direktor", "ダークフォグ拡張指令");
            Add("drag_hint", "拖动标题栏移动窗口 · 双击复位",
                "drag title bar to move · double-click to reset",
                "glisser le titre pour déplacer · double-clic pour recentrer",
                "Titelleiste ziehen zum Verschieben · Doppelklick zum Zurücksetzen",
                "タイトルバーをドラッグで移動 · ダブルクリックで中央へ");
            Add("tab_launch", "发射指挥", "Launch", "Lancer", "Abschuss", "発射");
            Add("tab_tinders", "火种管理", "Tinders", "Embrands", "Zündkörper", "種火");
            Add("header", "按 {0} 键开关    巢穴 {1}    可扩张星系 {2}    火种 {3}    待发 {4}",
                "Press {0} to toggle    Hives {1}    Targets {2}    Tinders {3}    Queued {4}",
                "Touche {0} : ouvrir/fermer    Ruches {1}    Cibles {2}    Embrands {3}    En attente {4}",
                "Taste {0} : ein/aus    Nester {1}    Ziele {2}    Zündkörper {3}    Wartend {4}",
                "{0} キーで開閉    巣 {1}    拡張先 {2}    種火 {3}    待機 {4}");

            // ── 发射页 ─────────────────────────────────────────────────────
            Add("src_header", "起点巢穴（已有巢穴）", "Source hive (existing)",
                "Ruche d'origine (existante)", "Quellnest (vorhanden)", "発射元の巣（既存）");
            Add("src_empty", "（星图上还没有黑雾巢穴）", "(No Dark Fog hives in this cluster yet)",
                "(Aucune ruche dans cet amas)", "(Noch keine Nester in diesem Haufen)", "（この星団に巣はまだない）");
            Add("hive_row", "{0}  #{1} · 轨道 {2}   火种 {3} / 空闲 {4}",
                "{0}  #{1} · orbit {2}   tinders {3} / idle {4}",
                "{0}  #{1} · orbite {2}   embrands {3} / libres {4}",
                "{0}  #{1} · Bahn {2}   Zündkörper {3} / frei {4}",
                "{0}  #{1} · 軌道 {2}   種火 {3} / 待機 {4}");
            Add("pending_tag", "待发", "queued", "en attente", "wartend", "待機");
            Add("dst_header", "目标星系（未达巢穴上限；巢穴由游戏自动选轨道）",
                "Target system (below hive cap; the game picks the orbit)",
                "Système cible (sous la limite ; le jeu choisit l'orbite)",
                "Zielsystem (unter dem Nestlimit; Spiel wählt die Bahn)",
                "目標星系（上限未満・軌道はゲームが選択）");
            Add("dst_empty", "（没有可扩张的星系）", "(No system can be expanded to)",
                "(Aucun système disponible)", "(Kein System verfügbar)", "（拡張可能な星系がない）");
            Add("star_row", "{0}  #{1}   巢穴 {2}/{3}", "{0}  #{1}   hives {2}/{3}",
                "{0}  #{1}   ruches {2}/{3}", "{0}  #{1}   Nester {2}/{3}", "{0}  #{1}   巣 {2}/{3}");
            Add("btn_launch", "发 射", "LAUNCH", "LANCER", "START", "発射");
            Add("btn_refresh", "刷新", "Refresh", "Rafraîchir", "Aktualisieren", "更新");
            Add("btn_cancel", "取消待发", "Cancel queue", "Annuler l'attente", "Wartend abbrechen", "待機解除");
            Add("btn_close", "关闭", "Close", "Fermer", "Schließen", "閉じる");

            // ── 火种页 ─────────────────────────────────────────────────────
            Add("tinder_header", "全图火种", "All tinders in this cluster", "Tous les embrands",
                "Alle Zündkörper", "星団内の種火");
            Add("tinder_empty", "（当前没有任何火种）", "(No tinders at the moment)",
                "(Aucun embrand)", "(Keine Zündkörper)", "（種火はない）");
            Add("tinder_row", "{0}   →   {1}    [{2}]    速度 {3:0} m/s",
                "{0}   →   {1}    [{2}]    speed {3:0} m/s",
                "{0}   →   {1}    [{2}]    vitesse {3:0} m/s",
                "{0}   →   {1}    [{2}]    Tempo {3:0} m/s",
                "{0}   →   {1}    [{2}]    速度 {3:0} m/s");
            Add("speed_label", "飞行速度 (m/s)：", "Flight speed (m/s):", "Vitesse (m/s) :",
                "Flugtempo (m/s):", "飛行速度 (m/s)：");
            Add("btn_apply", "应用", "Apply", "Appliquer", "Anwenden", "適用");
            Add("btn_restore", "恢复原版", "Restore vanilla", "Rétablir d'origine",
                "Original wiederherstellen", "元に戻す");
            Add("speed_state", "原版上限约 {0:0} m/s；当前{1}",
                "vanilla cap ~{0:0} m/s; currently {1}",
                "plafond d'origine ~{0:0} m/s ; actuellement {1}",
                "Original-Limit ~{0:0} m/s; aktuell {1}",
                "元の上限 約 {0:0} m/s；現在 {1}");
            Add("speed_override", "接管 {0:0}", "overridden at {0:0}", "forcée à {0:0}",
                "überschrieben mit {0:0}", "{0:0} に固定");
            Add("speed_off", "未接管", "not overridden", "non forcée", "nicht überschrieben", "固定なし");

            // ── 状态提示 ───────────────────────────────────────────────────
            Add("st_default", "选择起点巢穴与目标星系，然后点「发射」。",
                "Pick a source hive and a target system, then press LAUNCH.",
                "Choisissez une ruche et un système, puis LANCR.",
                "Quellnest und Zielsystem wählen, dann START.",
                "発射元の巣と目標星系を選び、発射を押してください。");
            Add("st_not_in_game", "未进入游戏（读不到星图）。",
                "Not in a game (cluster not loaded).",
                "Pas en partie (amas non chargé).",
                "Kein Spielstand (Haufen nicht geladen).",
                "ゲーム内ではありません（星団未読込）。");
            Add("st_pick_hive", "请先选择一个起点巢穴。", "Select a source hive first.",
                "Sélectionnez d'abord une ruche.", "Zuerst ein Quellnest wählen.",
                "先に発射元の巣を選んでください。");
            Add("st_pick_star", "请先选择一个目标星系。", "Select a target system first.",
                "Sélectionnez d'abord un système.", "Zuerst ein Zielsystem wählen.",
                "先に目標星系を選んでください。");
            Add("st_refreshed", "列表已刷新。", "List refreshed.", "Liste actualisée.",
                "Liste aktualisiert.", "一覧を更新しました。");
            Add("st_speed_bad", "速度格式不对，请输入数字（单位 m/s）。",
                "Invalid speed, please enter a number (m/s).",
                "Vitesse invalide, entrez un nombre (m/s).",
                "Ungültiges Tempo, bitte eine Zahl eingeben (m/s).",
                "速度の形式が正しくありません（m/s の数値）。");
            Add("st_speed_positive", "速度必须大于 0；要停止接管请点「恢复原版」。",
                "Speed must be > 0; use Restore vanilla to stop.",
                "La vitesse doit être > 0 ; utilisez Rétablir pour arrêter.",
                "Tempo muss > 0 sein; zum Beenden Original wiederherstellen.",
                "速度は 0 より大きい必要があります（解除は「元に戻す」）。");
            Add("st_speed_applied", "已接管飞行速度 {0:0} m/s，立即生效 {1} 个火种。",
                "Flight speed overridden to {0:0} m/s, applied to {1} tinder(s).",
                "Vitesse forcée à {0:0} m/s, appliquée à {1} embrand(s).",
                "Flugtempo auf {0:0} m/s gesetzt, auf {1} Zündkörper angewandt.",
                "飛行速度を {0:0} m/s に固定、{1} 個に適用しました。");
            Add("st_restored", "已停止接管，速度回到原版闭环调速。",
                "Override cleared, back to the vanilla speed controller.",
                "Contrôle rendu au régulateur d'origine.",
                "Überschreibung aufgehoben, Originalregler aktiv.",
                "固定を解除し、元の速度制御に戻しました。");

            Add("btn_apply_one", "应用到选中", "Apply to selected", "Appliquer à la sélection",
                "Auf Auswahl anwenden", "選択に適用");
            Add("btn_apply_all", "应用到全部", "Apply to all", "Appliquer à tous",
                "Auf alle anwenden", "すべてに適用");
            Add("st_pick_tinder", "请先在列表中选中一颗火种。", "Select a tinder in the list first.",
                "Sélectionnez d'abord un embrand.", "Zuerst einen Zündkörper auswählen.",
                "先に一覧から種火を選んでください。");
            Add("st_speed_one", "已把该火种速度设为 {0:0} m/s，立即生效 {1} 颗。",
                "Speed set to {0:0} m/s for that tinder, applied to {1}.",
                "Vitesse de cet embrand fixée à {0:0} m/s ({1} appliqué).",
                "Tempo dieses Zündkörpers auf {0:0} m/s gesetzt ({1} angewandt).",
                "この種火の速度を {0:0} m/s に設定（{1} 件適用）。");

            // ── 发射结果 ───────────────────────────────────────────────────
            Add("lg_not_in_game", "未进入游戏，无法下达指令。", "Not in a game, cannot issue an order.",
                "Pas en partie, ordre impossible.", "Kein Spielstand, Befehl unmöglich.",
                "ゲーム内ではないため命令できません。");
            Add("lg_hive_gone", "起点巢穴已不存在（可能已被摧毁），请刷新列表。",
                "Source hive no longer exists (destroyed?). Refresh the list.",
                "La ruche n'existe plus (détruite ?). Actualisez la liste.",
                "Quellnest existiert nicht mehr (zerstört?). Liste aktualisieren.",
                "発射元の巣が存在しません（破壊済み？）。一覧を更新してください。");
            Add("lg_cap_full", "目标星系 {0} 已达巢穴上限，请换一个。",
                "Target system {0} is at its hive cap, pick another.",
                "Le système {0} a atteint sa limite de ruches.",
                "Zielsystem {0} hat das Nestlimit erreicht.",
                "目標星系 {0} は巣の上限に達しています。");
            Add("lg_launched", "已发射（{0}）：{1} → {2}", "Launched ({0}): {1} → {2}",
                "Lancé ({0}) : {1} → {2}", "Gestartet ({0}): {1} → {2}",
                "発射しました（{0}）：{1} → {2}");
            Add("how_filled", "资源已补满", "resources topped up", "ressources remplies",
                "Ressourcen aufgefüllt", "資源を補充");
            Add("how_spawned", "现造火种并补满资源", "new tinder built", "embrand créé",
                "Zündkörper neu gebaut", "種火を新造");
            Add("lg_create_fail", "在目标星系创建巢穴失败（可能已达上限）。",
                "Failed to create a hive in the target system (cap reached?).",
                "Échec de création de la ruche cible (limite atteinte ?).",
                "Nest im Zielsystem konnte nicht erstellt werden (Limit?).",
                "目標星系に巣を作成できませんでした（上限？）。");
            Add("lg_pending", "该巢穴暂无空闲火种，已登记待发：{0} → {1}",
                "No idle tinder in this hive; queued: {0} → {1}",
                "Aucun embrand libre ; mis en attente : {0} → {1}",
                "Kein freier Zündkörper; in Warteschlange: {0} → {1}",
                "空き種火がないため待機に登録：{0} → {1}");
            Add("lg_none", "没有可用火种。", "No usable tinder.", "Aucun embrand utilisable.",
                "Kein nutzbarer Zündkörper.", "使用できる種火がありません。");
            Add("cancel_ok", "已取消待发指令。", "Queued order cancelled.",
                "Commande annulée.", "Wartender Befehl abgebrochen.", "待機命令を解除しました。");
            Add("cancel_none", "该巢穴没有待发指令。", "This hive has no queued order.",
                "Aucune commande en attente.", "Kein wartender Befehl.", "待機命令はありません。");

            // ── 火种状态 ───────────────────────────────────────────────────
            Add("stage_docked", "在港待发", "docked", "à quai", "angedockt", "ドック待機");
            Add("stage_takeoff", "起飞中", "launching", "décollage", "startet", "発進中");
            Add("stage_sailing", "飞行中", "in flight", "en vol", "im Flug", "航行中");
            Add("stage_other", "状态 {0}", "stage {0}", "état {0}", "Status {0}", "状態 {0}");
            Add("hive_desc", "{0} 轨道 {1}", "{0} orbit {1}", "{0} orbite {1}",
                "{0} Bahn {1}", "{0} 軌道 {1}");
        }
    }
}
