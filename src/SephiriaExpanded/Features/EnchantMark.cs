using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class EnchantMark
    {
        private const string LabelName = "SephiriaExpanded_EnchantText";
        private const float RefreshInterval = 0.25f;
        private static readonly Color MarkColor = Color.white;
        private static readonly List<Entry> Entries = new List<Entry>();
        private static float nextRefresh;

        internal static void Tick()
        {
            if (!ModSettings.EnchantMarkEnabled)
            {
                Strip();
                return;
            }

            if (Entries.Count == 0 || Time.unscaledTime < nextRefresh)
                return;

            nextRefresh = Time.unscaledTime + RefreshInterval;
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                Entry entry = Entries[i];
                if (entry.Icon == null || entry.Label == null)
                {
                    Entries.RemoveAt(i);
                    continue;
                }

                if (entry.Icon.gameObject.activeInHierarchy)
                    Draw(entry.Icon, entry.Label);
            }
        }

        internal static void Cleanup()
        {
            Strip();
        }

        [HarmonyPatch(typeof(UI_NewInventoryIcon), "UpdateIcon")]
        private static class InventoryIconPatch
        {
            private static void Postfix(UI_NewInventoryIcon __instance)
            {
                if (!ModSettings.EnchantMarkEnabled || __instance == null)
                    return;

                try
                {
                    TextMeshProUGUI label = Ensure(__instance);
                    if (label != null)
                        Draw(__instance, label);
                }
                catch
                {
                }
            }
        }

        private static TextMeshProUGUI Ensure(UI_NewInventoryIcon icon)
        {
            TextMeshProUGUI powerText = icon.powerText;
            if (powerText == null || powerText.transform.parent == null)
                return null;

            Transform parent = powerText.transform.parent;
            Transform existing = parent.Find(LabelName);
            if (existing != null)
            {
                TextMeshProUGUI existingLabel = existing.GetComponent<TextMeshProUGUI>();
                if (existingLabel != null)
                {
                    Remember(icon, existingLabel);
                    return existingLabel;
                }

                UnityEngine.Object.Destroy(existing.gameObject);
            }

            GameObject clone = UnityEngine.Object.Instantiate(powerText.gameObject, parent);
            clone.name = LabelName;
            TextMeshProUGUI label = clone.GetComponent<TextMeshProUGUI>();
            if (label == null)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            Flip(powerText.rectTransform, label.rectTransform);
            label.alignment = TextAlignmentOptions.BottomRight;
            label.color = MarkColor;
            label.text = string.Empty;
            label.raycastTarget = false;
            clone.SetActive(true);
            Remember(icon, label);
            return label;
        }

        private static void Flip(RectTransform source, RectTransform target)
        {
            Vector2 anchorMin = source.anchorMin;
            Vector2 anchorMax = source.anchorMax;
            target.anchorMin = new Vector2(1f - anchorMax.x, 1f - anchorMax.y);
            target.anchorMax = new Vector2(1f - anchorMin.x, 1f - anchorMin.y);
            target.pivot = new Vector2(1f - source.pivot.x, 1f - source.pivot.y);
            target.sizeDelta = source.sizeDelta;
            target.anchoredPosition = -source.anchoredPosition;
            target.localScale = source.localScale;
            target.localRotation = Quaternion.identity;
        }

        private static void Remember(UI_NewInventoryIcon icon, TextMeshProUGUI label)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Icon != icon)
                    continue;

                Entries[i] = new Entry { Icon = icon, Label = label };
                return;
            }

            Entries.Add(new Entry { Icon = icon, Label = label });
        }

        private static void Draw(UI_NewInventoryIcon icon, TextMeshProUGUI label)
        {
            int count = Count(icon);
            string text = count > 0 ? "+" + count : string.Empty;
            if (label.text != text)
                label.text = text;
        }

        private static int Count(UI_NewInventoryIcon icon)
        {
            try
            {
                DungeonManager dungeon = DungeonManager.Instance;
                GridInventory inventory = icon.Inventory;
                if (dungeon == null || inventory == null || inventory.netId == 0U)
                    return 0;

                NewItemOwnInstance item = inventory.FindItem(icon.X, icon.Y);
                if (item == null)
                    return 0;

                int count;
                return int.TryParse(dungeon.GetGlobalItemStatValue(item.InstanceID, "Enchant"), out count)
                    ? count
                    : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static void Strip()
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                TextMeshProUGUI label = Entries[i].Label;
                if (label != null)
                    UnityEngine.Object.Destroy(label.gameObject);
            }

            Entries.Clear();
            nextRefresh = 0f;
        }

        private struct Entry
        {
            internal UI_NewInventoryIcon Icon;
            internal TextMeshProUGUI Label;
        }
    }
}
