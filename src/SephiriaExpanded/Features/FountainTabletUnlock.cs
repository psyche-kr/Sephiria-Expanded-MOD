using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SephiriaExpanded
{
    internal static class FountainTabletUnlock
    {
        internal const string Key = "SEPHIRIA_TABLET";

        private const string IconName = "UITablet";

        private static readonly Dictionary<UI_DimensionPocketPanel, UI_JournalPanel_SearchOptionButton> Buttons = new Dictionary<UI_DimensionPocketPanel, UI_JournalPanel_SearchOptionButton>();
        private static Sprite tabletSprite;

        internal static bool IsTablet(ItemEntity item)
        {
            return item != null && item.type == EItemType.StoneTablet;
        }

        internal static bool Eligible(ItemEntity item)
        {
            return IsTablet(item) && item.activeType != EItemActiveType.Hidden && item.activeType != EItemActiveType.Disabled && item.rarity != EItemRarity.Eternal;
        }

        private static Sprite GetSprite()
        {
            if (tabletSprite != null) return tabletSprite;

            Texture2D texture = Resources.FindObjectsOfTypeAll<Texture2D>().FirstOrDefault(x => x != null && x.name == IconName);
            if (texture == null)
            {
                Debug.LogWarning("[Sephiria Expanded] " + IconName + " 텍스처를 찾지 못했습니다.");
                return null;
            }

            tabletSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            tabletSprite.name = "SephiriaExpanded_Sprite_" + IconName;
            tabletSprite.hideFlags = HideFlags.HideAndDontSave;
            return tabletSprite;
        }

        [HarmonyPatch(typeof(UI_DimensionPocketPanel), "Awake")]
        private static class SetupFilter
        {
            [HarmonyPostfix]
            private static void After(UI_DimensionPocketPanel __instance)
            {
                if (__instance == null || __instance.searchOptionButtonPrefab == null || __instance.searchOptionButtonContainer == null) return;
                if (Buttons.ContainsKey(__instance)) return;

                UI_JournalPanel_SearchOptionButton button = UnityEngine.Object.Instantiate(__instance.searchOptionButtonPrefab, __instance.searchOptionButtonContainer);
                button.name = "SephiriaExpanded_Button_TabletRarity";
                button.Initialize("석판", Key, __instance.SelectTag, __instance.tagButtonDefaultColor, GetSprite());
                button.transform.SetAsLastSibling();

                Buttons[__instance] = button;
                button.gameObject.SetActive(false);
            }
        }

        [HarmonyPatch(typeof(UI_DimensionPocketPanel), nameof(UI_DimensionPocketPanel.OnOpened))]
        private static class ShowFilter
        {
            [HarmonyPostfix]
            private static void After(UI_DimensionPocketPanel __instance)
            {
                UI_JournalPanel_SearchOptionButton button;
                if (Buttons.TryGetValue(__instance, out button) && button != null)
                    button.gameObject.SetActive(ModSettings.FountainArtifactUnlockEnabled);
            }
        }

        [HarmonyPatch(typeof(UI_DimensionPocketPanel), nameof(UI_DimensionPocketPanel.OnOpened))]
        private static class AddIcons
        {
            [HarmonyPostfix]
            private static void After(UI_DimensionPocketPanel __instance)
            {
                if (!ModSettings.FountainArtifactUnlockEnabled || __instance == null) return;

                Traverse panel = Traverse.Create(__instance);
                List<ItemMetadata> cached = panel.Field<List<ItemMetadata>>("cachedDimensionPocketItem").Value;
                List<ItemMetadata> current = panel.Field<List<ItemMetadata>>("currentSelectedItem").Value;
                List<UI_DimensionVaultItemIcon> icons = panel.Field<List<UI_DimensionVaultItemIcon>>("icons").Value;
                List<UI_DimensionVaultItemIcon> selected = panel.Field<List<UI_DimensionVaultItemIcon>>("selectedVaultIcons").Value;
                Dictionary<UI_DimensionVaultItemIcon, bool> checks = panel.Field<Dictionary<UI_DimensionVaultItemIcon, bool>>("iconCharmWeaponCheckList").Value;
                int storage = panel.Field<int>("cachedDimensionPocketStorage").Value;
                if (cached == null || current == null || icons == null || selected == null || checks == null) return;

                Action<PointerEventData.InputButton, UI_ItemIcon> click = (Action<PointerEventData.InputButton, UI_ItemIcon>)Delegate.CreateDelegate(
                    typeof(Action<PointerEventData.InputButton, UI_ItemIcon>), __instance, AccessTools.Method(typeof(UI_DimensionPocketPanel), "HandleItemClick"));

                int used = selected.Sum(icon => UI_DimensionPocketPanel.GetCapacity(ItemDatabase.FindItemById(icon.itemIcon.Item.entityID).rarity));
                int added = 0;

                foreach (ItemMetadata metadata in cached)
                {
                    ItemEntity item = ItemDatabase.FindItemById(metadata.entityID);
                    if (!Eligible(item)) continue;

                    UI_DimensionVaultItemIcon icon = UnityEngine.Object.Instantiate(__instance.iconPrefab, __instance.contentsZone);
                    icon.name = "SephiriaExpanded_Icon_Tablet" + item.id;
                    icon.SetCost(item.rarity);
                    icon.itemIcon.SetItem(new ItemMetadata(metadata.instanceID, metadata.entityID, metadata.quantity), click);
                    icon.itemIcon.tooltipPosition = __instance.tooltipPosition;
                    icon.itemIcon.SetFavorite(UI_DimensionPocketPanel.IsItemFavorite(metadata.entityID), false);

                    for (int i = 0; i < current.Count; i++)
                    {
                        if (current[i].entityID != metadata.entityID) continue;

                        int capacity = UI_DimensionPocketPanel.GetCapacity(item.rarity);
                        if (used + capacity > storage)
                        {
                            current.RemoveAt(i--);
                            continue;
                        }

                        used += capacity;
                        selected.Add(icon);
                    }

                    checks[icon] = true;
                    icons.Add(icon);
                    added++;
                }

                if (added == 0) return;

                AccessTools.Method(typeof(UI_DimensionPocketPanel), "SortingIcons").Invoke(__instance, null);
                AccessTools.Method(typeof(UI_DimensionPocketPanel), "UpdateCapacityText").Invoke(__instance, new object[] { selected, storage });
                __instance.defaultSelectable = icons[0].gameObject;
            }
        }

        [HarmonyPatch(typeof(UI_DimensionPocketPanel), "OnDestroy")]
        private static class CleanupButton
        {
            [HarmonyPostfix]
            private static void After(UI_DimensionPocketPanel __instance)
            {
                Buttons.Remove(__instance);
            }
        }
    }
}
