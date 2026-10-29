using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SephiriaExpanded
{
    internal static class DamageMeter
    {
        private const float UiInterval = 0.2f;
        private const int MaxSources = 40;
        private const int UploadRows = 4;
        private const string Separator = " · ";
        private const float ErrorLogInterval = 10f;

        private const string IceCategory = "얼음무구";
        private const string IceColorItem = "눈보라망치";

        private struct IconEntry
        {
            internal Sprite Icon;
            internal bool Ice;
        }

        private struct SourceView
        {
            internal string Name;
            internal Sprite Icon;
            internal Color Color;
        }

        private static readonly Color32[] PlayerColors =
        {
            new Color32(120, 190, 255, 255),
            new Color32(255, 200, 90, 255),
            new Color32(120, 225, 160, 255),
            new Color32(255, 130, 150, 255),
            new Color32(190, 130, 255, 255),
            new Color32(255, 160, 80, 255),
            new Color32(90, 220, 220, 255),
            new Color32(220, 220, 120, 255)
        };

        private static readonly DamageMeterModel Model = new DamageMeterModel();
        private static readonly List<PlayerAvatar> Players = new List<PlayerAvatar>();
        private static readonly List<DamageMeterPanel.MeterRow> Rows = new List<DamageMeterPanel.MeterRow>();
        private static readonly List<DamageMeterPanel.MeterRow> RowPool = new List<DamageMeterPanel.MeterRow>();
        private static readonly List<KeyValuePair<string, double>> Sources = new List<KeyValuePair<string, double>>();
        private static readonly Dictionary<string, float> RunSources = new Dictionary<string, float>();
        private static readonly Dictionary<uint, Dictionary<string, float>> RunBaseline = new Dictionary<uint, Dictionary<string, float>>();
        private static readonly Dictionary<uint, float> RunBaselineTotal = new Dictionary<uint, float>();
        private static readonly Dictionary<string, SourceView> SourceCache = new Dictionary<string, SourceView>();
        private static readonly Dictionary<string, IconEntry> IconsByNameKey = new Dictionary<string, IconEntry>();
        private static readonly Dictionary<string, IconEntry> IconsByName = new Dictionary<string, IconEntry>();
        private static readonly Dictionary<string, IconEntry> IconsBySpriteName = new Dictionary<string, IconEntry>();
        private static readonly Dictionary<string, bool> IceCategories = new Dictionary<string, bool>();
        private static readonly Dictionary<string, Sprite> AllSprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<Sprite, string> IconOwners = new Dictionary<Sprite, string>();

        private static readonly Dictionary<string, string[]> ForcedIcons = new Dictionary<string, string[]>
        {
            { "플라즈마", new[] { "플라즈마 헬멧" } },
            { "태양검", new[] { "InventoryAccessory_New_11", "태양검" } },
            { "빙결", new[] { "InventoryAccessory_New_09", "빙하" } },
            { "화상", new[] { "InventoryAccessory_New_12", "잉걸불" } },
            { "먹구름", new[] { "InventoryAccessory_New_13" } },
            { "감전", new[] { "InventoryAccessory_New_06", "마법공학" } },
            { "중독", new[] { "자갑벌궁", "Crossbow_Poison" } },
            { "상처악화", new[] { "심홍의 소용돌이", "Icon_GreatSword_Red" } }
        };

        private const string DamageSuffix = "피해";

        private static readonly Dictionary<EDamageElementalType, string> ElementKeywords = new Dictionary<EDamageElementalType, string>
        {
            { EDamageElementalType.Fire, "FireDamage" },
            { EDamageElementalType.Ice, "IceDamage" },
            { EDamageElementalType.Lightning, "LightningDamage" },
            { EDamageElementalType.IceAndLightning, "Elemental_IceAndLightning" },
            { EDamageElementalType.FireAndLightning, "Elemental_FireAndLightning" },
            { EDamageElementalType.FireAndIce, "Elemental_FireAndIce" }
        };

        private static readonly Dictionary<EDamageElementalType, string> ElementFallbacks = new Dictionary<EDamageElementalType, string>
        {
            { EDamageElementalType.Fire, "화염" },
            { EDamageElementalType.Ice, "냉기" },
            { EDamageElementalType.Lightning, "번개" },
            { EDamageElementalType.Chaos, "혼돈" },
            { EDamageElementalType.IceAndLightning, "냉기·번개" },
            { EDamageElementalType.FireAndLightning, "화염·번개" },
            { EDamageElementalType.FireAndIce, "화염·냉기" }
        };

        private static readonly Dictionary<EDamageElementalType, string> ElementNames = new Dictionary<EDamageElementalType, string>();

        private static DamageMeterCollector collector;
        private static DamageMeterPanel panel;
        private static bool visible = true;
        private static float nextUi;
        private static float nextErrorLog;
        private static string uploadHeader = string.Empty;

        internal static ModKeyBinding Key { get; private set; }

        internal static bool IsPointerOverPanel()
        {
            Mouse mouse = Mouse.current;
            return panel != null && mouse != null && panel.Contains(mouse.position.ReadValue());
        }

        [HarmonyPatch(typeof(PlayerInputController), nameof(PlayerInputController.HandleOnFire))]
        private static class BlockFire
        {
            [HarmonyPrefix]
            private static bool Before(InputAction.CallbackContext input)
            {
                return !(input.action != null && input.action.IsPressed() && IsPointerOverPanel());
            }
        }

        [HarmonyPatch(typeof(PlayerInputController), nameof(PlayerInputController.HandleOnSubFire))]
        private static class BlockSubFire
        {
            [HarmonyPrefix]
            private static bool Before(InputAction.CallbackContext input)
            {
                return !(input.action != null && input.action.IsPressed() && IsPointerOverPanel());
            }
        }

        [HarmonyPatch(typeof(PlayerInputController), nameof(PlayerInputController.HandleOnSelectNextQuickSlotItem))]
        private static class BlockQuickSlotScroll
        {
            [HarmonyPrefix]
            private static bool Before()
            {
                return !IsPointerOverPanel();
            }
        }

        internal static void Initialize()
        {
            Key = new ModKeyBinding("DamageMeter", () => ModSettings.DamageMeterBindings, json => ModSettings.DamageMeterBindings = json);
            Key.Initialize();
            collector = new DamageMeterCollector(Model);
            panel = new DamageMeterPanel { OnUpload = Upload, OnReset = ResetMeter };
        }

        internal static void Cleanup()
        {
            if (collector != null) collector.Release();
            if (panel != null) panel.Destroy();
            if (Key != null) Key.Cleanup();

            Model.Clear();
            RunBaseline.Clear();
            RunBaselineTotal.Clear();
            SourceCache.Clear();
            IconsByNameKey.Clear();
            IconsByName.Clear();
            IconsBySpriteName.Clear();
            AllSprites.Clear();
            IceCategories.Clear();
            IconOwners.Clear();
            ElementNames.Clear();
            IconColor.Clear();
            collector = null;
            panel = null;
            Key = null;
        }

        internal static void Tick()
        {
            if (collector == null || panel == null) return;

            try
            {
                Refresh();
            }
            catch (Exception exception)
            {
                if (Time.unscaledTime < nextErrorLog) return;

                nextErrorLog = Time.unscaledTime + ErrorLogInterval;
                Debug.LogWarning("[Sephiria Expanded][DamageMeter] 갱신 오류: " + exception);
            }
        }

        private static void Refresh()
        {
            float now = Time.time;
            PlayerAvatar local = ModSettings.DamageMeterEnabled ? LocalPlayer.Find() : null;
            if (local == null)
            {
                collector.Release();

                Model.Clear();
                panel.SetActive(false);
                return;
            }

            bool blocked = UIManager.Instance == null || UIManager.Instance.CurrentControlStack != null;
            if (Key.Pressed && !blocked) visible = !visible;

            DamageMeterCollector.CollectPlayers(Players);
            collector.Tick(now, Players);
            Model.Tick(now, collector.InBattle);

            if (!visible || !panel.TryBuild())
            {
                panel.SetActive(false);
                return;
            }

            panel.SetActive(true);
            panel.HandleInput(blocked);

            if (!panel.ConsumeDirty() && Time.unscaledTime < nextUi) return;

            nextUi = Time.unscaledTime + UiInterval;
            Render(now, local);
        }

        private static void Render(float now, PlayerAvatar local)
        {
            if (DamageMeterPanel.MiniTotal) panel.SetMiniText(Amount(RunTotalSince(local)), string.Empty);
            else panel.SetMiniText(Amount(Model.Of(local.netId) / Math.Max(1f, Model.Seconds(now))), "dps");
            int page = panel.Page;
            bool run = page >= 2;
            string subtitle = page % 2 == 0 ? RenderPersonal(now, local, run) : RenderParty(now, run);
            panel.Render(subtitle, Rows);
        }

        private static void ResetMeter()
        {
            Model.Clear();
            if (collector != null) collector.Release();

            RunBaseline.Clear();
            RunBaselineTotal.Clear();
            foreach (PlayerAvatar player in Players)
            {
                Dictionary<string, float> sources = new Dictionary<string, float>();
                DamageMeterCollector.CollectRunSources(player, sources);
                RunBaseline[player.netId] = sources;
                RunBaselineTotal[player.netId] = DamageMeterCollector.RunTotal(player);
            }

            nextUi = 0f;
        }

        private static float RunSince(uint netId, string source, float value)
        {
            Dictionary<string, float> baseline;
            float before;
            if (!RunBaseline.TryGetValue(netId, out baseline) || !baseline.TryGetValue(source, out before)) return value;

            if (value < before)
            {
                baseline[source] = 0f;
                return value;
            }

            return value - before;
        }

        private static double RunTotalSince(PlayerAvatar player)
        {
            float total = DamageMeterCollector.RunTotal(player);
            float before;
            if (!RunBaselineTotal.TryGetValue(player.netId, out before)) return total;

            if (total < before)
            {
                RunBaselineTotal[player.netId] = 0f;
                return total;
            }

            return total - before;
        }

        private static void Upload()
        {
            PlayerAvatar local = LocalPlayer.Find();
            DungeonManager manager = DungeonManager.Instance;
            if (local == null || manager == null || !NetworkClient.active || Rows.Count == 0 || string.IsNullOrEmpty(uploadHeader)) return;

            manager.Chat(local, local.Name, uploadHeader);
            int count = Math.Min(Rows.Count, UploadRows);
            for (int i = 0; i < count; i++)
            {
                DamageMeterPanel.MeterRow row = Rows[i];
                manager.Chat(local, local.Name, (i + 1) + ". " + row.Name + Separator + row.Number + Separator + row.Percent);
            }
        }

        private static string RenderPersonal(float now, PlayerAvatar local, bool run)
        {
            Sources.Clear();
            double total = 0d;
            if (run)
            {
                DamageMeterCollector.CollectRunSources(local, RunSources);
                foreach (KeyValuePair<string, float> pair in RunSources)
                {
                    float since = RunSince(local.netId, pair.Key, pair.Value);
                    if (since > 0f) Sources.Add(new KeyValuePair<string, double>(pair.Key, since));
                }
            }
            else
            {
                foreach (KeyValuePair<string, double> pair in Model.SourcesOf(local.netId))
                    Sources.Add(pair);
            }

            foreach (KeyValuePair<string, double> pair in Sources)
                total += pair.Value;

            Sources.Sort((a, b) => b.Value.CompareTo(a.Value));

            float seconds = Math.Max(1f, Model.Seconds(now));
            Rows.Clear();
            int count = Math.Min(Sources.Count, MaxSources);
            double top = count > 0 ? Sources[0].Value : 0d;
            for (int i = 0; i < count; i++)
            {
                KeyValuePair<string, double> pair = Sources[i];
                SourceView view = ViewOf(pair.Key);
                DamageMeterPanel.MeterRow row = RentRow(i);
                row.Name = view.Name;
                row.Icon = view.Icon;
                row.Color = view.Color;
                row.Raw = pair.Value;
                row.Fill = top > 0d ? (float)(pair.Value / top) : 0f;
                row.Number = run ? Amount(pair.Value) : Amount(pair.Value / seconds);
                row.Value = run ? row.Number : row.Number + " dps";
                row.Percent = Percent(pair.Value, total);
                Rows.Add(row);
            }

            string name = DamageMeterCollector.NameOf(local);
            if (run)
            {
                uploadHeader = name + " 전체 피해량" + Separator + "합계 " + Amount(total);
                return "합계 " + Amount(total);
            }

            string current = Amount(total / seconds);
            string best = Amount(Model.BestOf(local.netId));
            uploadHeader = name + " DPS" + Separator + "현재 " + current + Separator + "최고 " + best;
            return Model.CombatStart <= 0f ? "대기 중" : "현재 " + current + " dps   최고 " + best + " dps";
        }

        private static string RenderParty(float now, bool run)
        {
            Rows.Clear();
            double sum = 0d;
            float seconds = Math.Max(1f, Model.Seconds(now));
            for (int i = 0; i < Players.Count; i++)
            {
                PlayerAvatar player = Players[i];
                double value = run ? RunTotalSince(player) : Model.Of(player.netId) / seconds;
                sum += value;
                DamageMeterPanel.MeterRow row = RentRow(i);
                row.Name = DamageMeterCollector.NameOf(player);
                row.Icon = ComboIcon(player);
                row.Color = IconColor.Of(row.Icon, PlayerColors[(int)(player.netId % (uint)PlayerColors.Length)]);
                row.Raw = value;
                Rows.Add(row);
            }

            Rows.Sort((a, b) => b.Raw.CompareTo(a.Raw));

            double top = Rows.Count > 0 ? Rows[0].Raw : 0d;
            foreach (DamageMeterPanel.MeterRow row in Rows)
            {
                row.Number = Amount(row.Raw);
                row.Value = run ? row.Number : row.Number + " dps";
                row.Percent = Percent(row.Raw, sum);
                row.Fill = top > 0d ? (float)(row.Raw / top) : 0f;
            }

            if (!run)
            {
                uploadHeader = "파티 DPS";
                return string.Empty;
            }

            uploadHeader = "파티 전체 피해량" + Separator + "합계 " + Amount(sum);
            return "합계 " + Amount(sum);
        }

        private static DamageMeterPanel.MeterRow RentRow(int index)
        {
            while (RowPool.Count <= index)
                RowPool.Add(new DamageMeterPanel.MeterRow());

            return RowPool[index];
        }

        private static SourceView ViewOf(string key)
        {
            SourceView view;
            if (SourceCache.TryGetValue(key, out view)) return view;

            string id;
            EDamageElementalType element;
            DamageMeterCollector.SplitSource(key, out id, out element);
            view = BaseViewOf(id);
            string elementName = ElementName(element);
            if (!string.IsNullOrEmpty(elementName)) view.Name += " (" + elementName + ")";
            if (IconsByNameKey.Count > 0) SourceCache[key] = view;
            return view;
        }

        private static string ElementName(EDamageElementalType element)
        {
            string name;
            if (ElementNames.TryGetValue(element, out name)) return name;

            string fallback;
            if (!ElementFallbacks.TryGetValue(element, out fallback)) return null;

            name = fallback;
            string keyword;
            KeywordEntity entity = ElementKeywords.TryGetValue(element, out keyword) ? KeywordDatabase.GetEntity(keyword) : null;
            if (entity != null)
            {
                string converted = Regex.Replace(entity.Convert(false, false) ?? string.Empty, "<[^>]*>", string.Empty).Trim();
                if (converted.EndsWith(DamageSuffix, StringComparison.Ordinal)) converted = converted.Substring(0, converted.Length - DamageSuffix.Length).Trim();
                if (!string.IsNullOrEmpty(converted)) name = converted;
            }

            ElementNames[element] = name;
            return name;
        }

        private static SourceView BaseViewOf(string id)
        {
            SourceView view;
            if (SourceCache.TryGetValue(id, out view)) return view;

            EnsureIconIndex();
            DamageIdEntity entity = KeywordDatabase.GetDamageIdEntity(id);
            string name = entity == null ? Fallback(id) : KeywordDatabase.Convert(entity.aName.ToString(), useColor: false, useSprite: false);
            string key = entity == null || entity.aName == null ? null : entity.aName.key;

            IconEntry entry;
            bool found = FindForced(Compact(name), out entry) || !string.IsNullOrEmpty(key) && IconsByNameKey.TryGetValue(key, out entry);
            Sprite icon;
            Color color;
            if (found)
            {
                icon = entry.Icon;
                color = entry.Ice ? IceColor(IconColor.Of(icon, CategoryColor(entity))) : IconColor.Of(icon, CategoryColor(entity));
            }
            else
            {
                icon = entity == null ? null : entity.icon;
                color = IconColor.Of(icon, CategoryColor(entity));
                string owner;
                if (icon != null && IconOwners.TryGetValue(icon, out owner) && owner != id) color = HashColor(id);
                else if (icon != null) IconOwners[icon] = id;

                Debug.Log("[Sephiria Expanded][DamageMeter] 출처 아이콘 연결 안 됨: " + id + " (" + name + ", 이름키 " + key + ")");
            }

            view = new SourceView { Name = name, Icon = icon, Color = color };
            if (IconsByNameKey.Count > 0) SourceCache[id] = view;
            return view;
        }

        private static bool FindForced(string compactName, out IconEntry entry)
        {
            entry = default(IconEntry);
            string[] candidates;
            if (!ForcedIcons.TryGetValue(compactName, out candidates)) return false;

            foreach (string candidate in candidates)
            {
                if (IconsBySpriteName.TryGetValue(SpriteKey(candidate), out entry) || IconsByName.TryGetValue(Compact(candidate), out entry)) return true;

                Sprite sprite = SpriteByName(candidate);
                if (sprite == null) continue;

                IconEntry owner;
                entry = new IconEntry { Icon = sprite, Ice = IconsBySpriteName.TryGetValue(SpriteKey(sprite.name), out owner) && owner.Ice };
                return true;
            }

            Debug.Log("[Sephiria Expanded][DamageMeter] 강제 아이콘을 찾지 못함: " + compactName);
            return false;
        }

        private static string SpriteKey(string name)
        {
            Match match = Regex.Match(name, @"^(.*?)(\d{1,6})$");
            return match.Success ? match.Groups[1].Value + int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : name;
        }

        private static Sprite SpriteByName(string name)
        {
            if (AllSprites.Count == 0)
            {
                foreach (Sprite sprite in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    string key = sprite == null ? null : SpriteKey(sprite.name);
                    if (key != null && !AllSprites.ContainsKey(key)) AllSprites[key] = sprite;
                }
            }

            Sprite found;
            return AllSprites.TryGetValue(SpriteKey(name), out found) ? found : null;
        }

        private static Color IceColor(Color fallback)
        {
            IconEntry hammer;
            return IconsByName.TryGetValue(Compact(IceColorItem), out hammer) ? IconColor.Of(hammer.Icon, fallback) : fallback;
        }

        private static string Compact(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace(" ", string.Empty);
        }

        private static void EnsureIconIndex()
        {
            if (IconsByNameKey.Count > 0) return;

            try
            {
                foreach (int id in ItemDatabase.GetAllItemID())
                {
                    ItemEntity item = ItemDatabase.FindItemById(id);
                    if (item != null && item.icon != null) Index(item.aName.key, item.Name, item.icon, IsIce(item));
                }

                foreach (WeaponEntity weapon in WeaponDatabase.GetAll())
                    if (weapon != null && weapon.icon != null) Index(weapon.aName.key, weapon.Name, weapon.icon, false);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Sephiria Expanded] 미터기 아이콘 색인 실패: " + exception.Message);
            }
        }

        private static bool IsIce(ItemEntity item)
        {
            if (item.categories == null) return false;

            foreach (string category in item.categories)
            {
                bool ice;
                if (!IceCategories.TryGetValue(category, out ice))
                {
                    ItemCategoryEntity entity = ItemDatabase.FindItemCategory(category);
                    ice = entity != null && Compact(entity.Name) == IceCategory;
                    IceCategories[category] = ice;
                }

                if (ice) return true;
            }

            return false;
        }

        private static void Index(string key, string name, Sprite icon, bool ice)
        {
            IconEntry entry = new IconEntry { Icon = icon, Ice = ice };
            if (!string.IsNullOrEmpty(key) && !IconsByNameKey.ContainsKey(key)) IconsByNameKey[key] = entry;

            string plain = Compact(KeywordDatabase.Convert(name, useColor: false, useSprite: false));
            if (!string.IsNullOrEmpty(plain) && !IconsByName.ContainsKey(plain)) IconsByName[plain] = entry;
            string spriteKey = SpriteKey(icon.name);
            if (!IconsBySpriteName.ContainsKey(spriteKey)) IconsBySpriteName[spriteKey] = entry;
        }

        private static Color HashColor(string id)
        {
            int hash = 17;
            foreach (char letter in id)
                hash = hash * 31 + letter;

            return Color.HSVToRGB((hash & 0x7fffffff) % 360 / 360f, 0.55f, 0.85f);
        }

        private static string Fallback(string id)
        {
            if (id == DamageMeterCollector.UnknownId) return "기타";

            return id.StartsWith("Companion_", StringComparison.Ordinal) ? "동료" : id;
        }

        private static Color CategoryColor(DamageIdEntity entity)
        {
            if (entity == null) return new Color32(160, 170, 185, 255);

            switch (entity.category)
            {
                case DamageIdEntity.ECategory.Weapon: return new Color32(120, 190, 255, 255);
                case DamageIdEntity.ECategory.Magic: return new Color32(190, 130, 255, 255);
                case DamageIdEntity.ECategory.Charm: return new Color32(255, 200, 90, 255);
                case DamageIdEntity.ECategory.Ability: return new Color32(120, 225, 160, 255);
                default: return new Color32(160, 170, 185, 255);
            }
        }

        private static Sprite ComboIcon(PlayerAvatar player)
        {
            GridInventory inventory = player.Inventory;
            if (inventory == null) return null;

            string best = null;
            int bestCount = 0;
            foreach (KeyValuePair<string, int> pair in inventory.currentSetEffectCount)
            {
                if (pair.Value <= bestCount || !inventory.lastAppliedComboEffects.ContainsKey(pair.Key)) continue;

                best = pair.Key;
                bestCount = pair.Value;
            }

            ItemCategoryEntity category = best == null ? null : ItemDatabase.FindItemCategory(best);
            return category == null ? null : category.categoryIcon;
        }

        private static string Percent(double value, double total)
        {
            return (total > 0d ? value / total * 100d : 0d).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        private static string Amount(double value)
        {
            return Math.Round(value).ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
