using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaExpanded
{
    internal static class MoreMultiplayerInfo
    {
        private const string ContainerName = "SephiriaExpanded_Panel_MultiplayerInfo";
        private const float UpdateInterval = 0.5f;
        private const float HeightRatio = 0.45f;
        private const float Gap = 1f;
        private const float FontRatio = 1.25f;
        private const float SlotGapEm = 1.1f;
        private const string SpritePrefix = "<sprite=\"Keyword\" name=";
        private const string DiceReference = SpritePrefix + "Dice>88";
        private const string LeafReference = SpritePrefix + "Leaf>8888";

        private sealed class InfoRow
        {
            internal RectTransform Container;
            internal TextMeshProUGUI Text;
            internal float NextUpdate;
        }

        private static readonly AccessTools.FieldRef<UI_MultiplayerHPBar, PlayerAvatar> PlayerField = AccessTools.FieldRefAccess<UI_MultiplayerHPBar, PlayerAvatar>("player");
        private static readonly AccessTools.FieldRef<UI_MultiplayerHPBar, PlayerSpawner> SpawnerField = AccessTools.FieldRefAccess<UI_MultiplayerHPBar, PlayerSpawner>("spawner");
        private static readonly Dictionary<UI_MultiplayerHPBar, InfoRow> Rows = new Dictionary<UI_MultiplayerHPBar, InfoRow>();
        private static readonly List<UI_MultiplayerHPBar> Stale = new List<UI_MultiplayerHPBar>();
        private static TMP_FontAsset font;
        private static Material material;
        private static Color color = Color.white;
        private static HorizontalOrVerticalLayoutGroup spacingGroup;
        private static float baseSpacing;

        [HarmonyPatch(typeof(UI_MultiplayerHPBar), "Update")]
        private static class UpdatePatch
        {
            [HarmonyPostfix]
            private static void After(UI_MultiplayerHPBar __instance)
            {
                Tick(__instance);
            }
        }

        private static void Tick(UI_MultiplayerHPBar bar)
        {
            InfoRow row;
            Rows.TryGetValue(bar, out row);
            if (!ModSettings.MoreMultiplayerInfoEnabled)
            {
                if (row != null && row.Container != null) row.Container.gameObject.SetActive(false);

                ApplySpacing(false, 0f);
                return;
            }

            if (row == null || row.Container == null)
            {
                row = Build(bar);
                if (row == null) return;

                Rows[bar] = row;
            }

            if (GroupOf(bar) == null) return;

            if (!row.Container.gameObject.activeSelf) row.Container.gameObject.SetActive(true);

            ApplySpacing(true, row.Container.sizeDelta.y + Gap);
            if (Time.unscaledTime < row.NextUpdate) return;

            row.NextUpdate = Time.unscaledTime + UpdateInterval;
            Refresh(bar, row);
        }

        private static void ApplySpacing(bool enabled, float extra)
        {
            if (spacingGroup == null)
            {
                UIManager manager = UIManager.Instance;
                UI_MultiplayerHUD hud = manager == null ? null : manager.GetElement<UI_MultiplayerHUD>();
                RectTransform zone = hud == null ? null : hud.contentsZone;
                spacingGroup = zone == null ? null : zone.GetComponent<HorizontalOrVerticalLayoutGroup>();
                if (spacingGroup == null) return;

                baseSpacing = spacingGroup.spacing;
            }

            float wanted = enabled ? baseSpacing + extra : baseSpacing;
            if (!Mathf.Approximately(spacingGroup.spacing, wanted)) spacingGroup.spacing = wanted;
        }

        private static RectTransform GroupOf(UI_MultiplayerHPBar bar)
        {
            return bar.hpBar == null ? null : bar.hpBar.rectTransform.parent as RectTransform;
        }

        private static void Refresh(UI_MultiplayerHPBar bar, InfoRow row)
        {
            PlayerAvatar player = PlayerField(bar);
            PlayerSpawner spawner = SpawnerField(bar);
            if (player == null || spawner == null) return;

            PlayerLocalDataStorage storage = player.GetComponent<PlayerLocalDataStorage>();
            int sapphire = (storage == null ? 0 : storage.sapphire) + spawner.sapphireInRun;
            Layout(row, GroupOf(bar), SpritePrefix + "Dice>" + player.rerollDice, SpritePrefix + "Leaf>" + player.Money, SpritePrefix + "Sapphire>" + sapphire);
        }

        private static InfoRow Build(UI_MultiplayerHPBar bar)
        {
            RectTransform group = GroupOf(bar);
            if (group == null || !EnsureFont()) return null;

            Stale.Clear();
            foreach (KeyValuePair<UI_MultiplayerHPBar, InfoRow> pair in Rows)
                if (pair.Key == null) Stale.Add(pair.Key);

            foreach (UI_MultiplayerHPBar gone in Stale)
                Rows.Remove(gone);

            GameObject holder = new GameObject(ContainerName, typeof(RectTransform));
            InfoRow row = new InfoRow { Container = holder.GetComponent<RectTransform>() };
            row.Container.SetParent(group, false);

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(row.Container, false);
            row.Text = textObject.GetComponent<TextMeshProUGUI>();
            row.Text.font = font;
            row.Text.fontSharedMaterial = material;
            row.Text.color = color;
            row.Text.alignment = TextAlignmentOptions.MidlineLeft;
            row.Text.enableAutoSizing = false;
            row.Text.richText = true;
            row.Text.textWrappingMode = TextWrappingModes.NoWrap;
            row.Text.overflowMode = TextOverflowModes.Overflow;
            row.Text.raycastTarget = false;
            return row;
        }

        private static float Measure(InfoRow row, string value)
        {
            return row.Text.GetPreferredValues(value).x;
        }

        private static string Position(float width, float size)
        {
            return "<pos=" + (width / size).ToString("0.###", CultureInfo.InvariantCulture) + "em>";
        }

        private static void Layout(InfoRow row, RectTransform group, string dice, string leaf, string sapphire)
        {
            if (group == null) return;

            float height = Mathf.Max(4f, group.rect.height * HeightRatio);
            RectTransform container = row.Container;
            container.anchorMin = Vector2.zero;
            container.anchorMax = Vector2.zero;
            container.pivot = new Vector2(0f, 1f);
            container.anchoredPosition = new Vector2(0f, -Gap);

            row.Text.fontSize = height * FontRatio;
            float size = row.Text.fontSize;
            float gap = SlotGapEm * size;
            float leafX = Mathf.Max(Measure(row, dice), Measure(row, DiceReference)) + gap;
            float sapphireX = leafX + Mathf.Max(Measure(row, leaf), Measure(row, LeafReference)) + gap;
            row.Text.text = dice + Position(leafX, size) + leaf + Position(sapphireX, size) + sapphire;
            float width = Mathf.Max(1f, sapphireX + Measure(row, sapphire));
            RectTransform text = row.Text.rectTransform;
            text.anchorMin = new Vector2(0f, 1f);
            text.anchorMax = new Vector2(0f, 1f);
            text.pivot = new Vector2(0f, 1f);
            text.anchoredPosition = Vector2.zero;
            text.sizeDelta = new Vector2(width + 1f, height);
            container.sizeDelta = new Vector2(width + 1f, height);
        }

        private static bool EnsureFont()
        {
            if (font != null) return true;

            UIManager manager = UIManager.Instance;
            if (manager == null) return false;

            UI_DiceIndicator dice = manager.GetElement<UI_DiceIndicator>();
            UI_MoneyIndicator money = manager.GetElement<UI_MoneyIndicator>();
            UI_SapphireIndicator sapphire = manager.GetElement<UI_SapphireIndicator>();
            TMP_Text source = dice != null && dice.valueText != null ? dice.valueText : money != null && money.valueText != null ? money.valueText : sapphire == null ? null : sapphire.valueText;
            if (source == null || source.font == null) return false;

            font = source.font;
            material = source.fontSharedMaterial;
            color = source.color;
            return true;
        }
    }
}
