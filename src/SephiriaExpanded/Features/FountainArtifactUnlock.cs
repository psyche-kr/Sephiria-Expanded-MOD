using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class FountainArtifactUnlock
    {
        private const string BondKey = "SEPHIRIA_BOND";
        private static readonly Color BondRarityColor = new Color(0f, 0.7647f, 0.4314f, 1f);
        private static readonly Dictionary<UI_DimensionPocketPanel, UI_JournalPanel_SearchOptionButton> BondButtons = new Dictionary<UI_DimensionPocketPanel, UI_JournalPanel_SearchOptionButton>();

        internal static bool Eligible(ItemEntity item)
        {
            return item != null && item.type == EItemType.Charm && item.activeType != EItemActiveType.Hidden && item.activeType != EItemActiveType.Disabled && item.rarity != EItemRarity.Eternal;
        }

        internal static bool IsBond(ItemEntity item)
        {
            if (item == null) return false;

            if (item.categories != null)
            {
                for (int i = 0; i < item.categories.Count; i++)
                {
                    string category = item.categories[i];
                    if (string.IsNullOrEmpty(category)) continue;

                    try
                    {
                        ItemCategoryEntity entity = ItemDatabase.FindItemCategory(category);
                        if (entity != null && entity.categoryName.ToString() == "결속") return true;
                    }
                    catch
                    {
                    }

                    string value = category.ToLowerInvariant();
                    if (value.Contains("bond") || value.Contains("dual") || value.Contains("bind") || value.Contains("bound")) return true;
                }
            }

            if (ReadBool(item, "useRarityDualBG")) return true;
            if (ReadBool(item, "isDualCharm")) return true;
            if (ReadBool(item, "isDual")) return true;

            if (item.resourcePrefab != null)
            {
                Component[] components = item.resourcePrefab.GetComponents<Component>();

                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];
                    if (component == null) continue;

                    if (ReadBool(component, "isDualCharmMode")) return true;
                    if (ReadBool(component, "useRarityDualBG")) return true;

                    string type = component.GetType().Name.ToLowerInvariant();
                    if (type.Contains("dualcharm") || type.Contains("bondcharm")) return true;
                }
            }

            return false;
        }

        private static bool IsBondButton(UI_JournalPanel_SearchOptionButton button)
        {
            if (button == null || string.IsNullOrEmpty(button.data)) return false;
            if (button.data == BondKey) return true;

            try
            {
                ItemCategoryEntity category = ItemDatabase.FindItemCategory(button.data);
                if (category != null && category.categoryName.ToString() == "결속") return true;
            }
            catch
            {
            }

            string value = button.data.ToLowerInvariant();
            return value.Contains("bond") || value.Contains("dual") || value.Contains("bind") || value.Contains("bound");
        }

        private static bool ReadBool(object target, string name)
        {
            if (target == null) return false;

            try
            {
                Type type = target.GetType();
                FieldInfo field = AccessTools.Field(type, name);

                if (field != null && field.FieldType == typeof(bool)) return (bool)field.GetValue(target);

                PropertyInfo property = AccessTools.Property(type, name);

                if (property != null && property.PropertyType == typeof(bool) && property.GetIndexParameters().Length == 0)
                    return (bool)property.GetValue(target, null);
            }
            catch
            {
            }

            return false;
        }

        private static IEnumerable<Type> FindFilterPanelTypes()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int a = 0; a < assemblies.Length; a++)
            {
                Type[] types;

                try
                {
                    types = assemblies[a].GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }
                catch
                {
                    continue;
                }

                if (types == null) continue;

                for (int i = 0; i < types.Length; i++)
                {
                    Type type = types[i];
                    if (type == null) continue;

                    MethodInfo selectTag = FindFilterMethod(type, "SelectTag");
                    MethodInfo selectRarity = FindFilterMethod(type, "SelectRarity");

                    if (selectTag == null || selectRarity == null) continue;
                    if (AccessTools.Field(type, "selectedRarityButton") == null) continue;
                    if (AccessTools.Field(type, "selectedTagButton") == null) continue;
                    if (AccessTools.Field(type, "selectedRarity") == null) continue;
                    if (AccessTools.Field(type, "selectedTag") == null) continue;

                    yield return type;
                }
            }
        }

        private static MethodInfo FindFilterMethod(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;

            return type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(UI_JournalPanel_SearchOptionButton) },
                null
            );
        }

        private static Color GetJournalTagColor(UI_DimensionPocketPanel panel)
        {
            try
            {
                foreach (Type type in FindFilterPanelTypes())
                {
                    if (type == typeof(UI_DimensionPocketPanel)) continue;
                    if (type.Name.IndexOf("Journal", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    FieldInfo field = AccessTools.Field(type, "tagButtonDefaultColor");
                    if (field == null || field.FieldType != typeof(Color)) continue;

                    UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(type);

                    if (objects == null) continue;

                    for (int i = 0; i < objects.Length; i++)
                    {
                        if (objects[i] == null) continue;

                        object value = field.GetValue(objects[i]);

                        if (value is Color)
                            return (Color)value;
                    }
                }
            }
            catch
            {
            }

            return panel != null ? panel.tagButtonDefaultColor : Color.white;
        }

        private static void ClearRarity(object panel)
        {
            if (panel == null) return;

            try
            {
                Traverse traverse = Traverse.Create(panel);
                UI_JournalPanel_SearchOptionButton selected = traverse.Field<UI_JournalPanel_SearchOptionButton>("selectedRarityButton").Value;

                if (selected != null) selected.SetSelected(false);

                traverse.Field<UI_JournalPanel_SearchOptionButton>("selectedRarityButton").Value = null;
                traverse.Field<string>("selectedRarity").Value = "";
            }
            catch
            {
            }
        }

        private static void ClearBondTag(object panel)
        {
            if (panel == null) return;

            try
            {
                Traverse traverse = Traverse.Create(panel);
                UI_JournalPanel_SearchOptionButton selected = traverse.Field<UI_JournalPanel_SearchOptionButton>("selectedTagButton").Value;

                if (!IsBondButton(selected)) return;

                selected.SetSelected(false);
                traverse.Field<UI_JournalPanel_SearchOptionButton>("selectedTagButton").Value = null;
                traverse.Field<string>("selectedTag").Value = "";
            }
            catch
            {
            }
        }

        [HarmonyPatch]
        private static class BondTagExclusive
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                foreach (Type type in FindFilterPanelTypes())
                {
                    MethodInfo method = FindFilterMethod(type, "SelectTag");
                    if (method != null) yield return method;
                }
            }

            [HarmonyPrefix]
            private static void Before(object __instance, UI_JournalPanel_SearchOptionButton button)
            {
                if (ModSettings.FountainArtifactUnlockEnabled && IsBondButton(button)) ClearRarity(__instance);
            }
        }

        [HarmonyPatch]
        private static class RarityExclusive
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                foreach (Type type in FindFilterPanelTypes())
                {
                    MethodInfo method = FindFilterMethod(type, "SelectRarity");
                    if (method != null) yield return method;
                }
            }

            [HarmonyPrefix]
            private static void Before(object __instance)
            {
                if (ModSettings.FountainArtifactUnlockEnabled) ClearBondTag(__instance);
            }
        }

        [HarmonyPatch(typeof(UI_DimensionPocketPanel), "Awake")]
        private static class SetupBondFilter
        {
            [HarmonyPostfix]
            private static void After(UI_DimensionPocketPanel __instance)
            {
                if (__instance == null || __instance.searchOptionButtonPrefab == null || __instance.searchOptionButtonContainer == null) return;
                if (BondButtons.ContainsKey(__instance)) return;

                UI_JournalPanel_SearchOptionButton existing = __instance.searchOptionButtonContainer.GetComponentsInChildren<UI_JournalPanel_SearchOptionButton>(true).FirstOrDefault(x => x != null && x.data == BondKey);

                if (existing != null)
                {
                    BondButtons[__instance] = existing;
                    SetBondRarityColor(existing);
                    return;
                }

                UI_JournalPanel_SearchOptionButton button = UnityEngine.Object.Instantiate(__instance.searchOptionButtonPrefab, __instance.searchOptionButtonContainer);
                button.name = "SephiriaExpanded_Button_BondRarity";
                button.Initialize("결속", BondKey, __instance.SelectRarity, GetJournalTagColor(__instance), null);
                SetBondRarityColor(button);

                UI_JournalPanel_SearchOptionButton legend = __instance.searchOptionButtonContainer.GetComponentsInChildren<UI_JournalPanel_SearchOptionButton>(true).FirstOrDefault(x => x != null && x.data == EItemRarity.Legend.ToString());

                if (legend != null) button.transform.SetSiblingIndex(legend.transform.GetSiblingIndex() + 1);

                try
                {
                    List<UI_JournalPanel_SearchOptionButton> rarityButtons = Traverse.Create(__instance).Field<List<UI_JournalPanel_SearchOptionButton>>("rarityOptionButtons").Value;

                    if (rarityButtons != null && !rarityButtons.Contains(button))
                        rarityButtons.Add(button);
                }
                catch
                {
                }

                BondButtons[__instance] = button;
                button.gameObject.SetActive(false);
            }
        }

        private static void SetBondRarityColor(UI_JournalPanel_SearchOptionButton button)
        {
            if (button != null && button.image != null)
                button.image.color = BondRarityColor;
        }

        [HarmonyPatch(typeof(UI_DimensionPocketPanel), nameof(UI_DimensionPocketPanel.OnOpened))]
        private static class BondVisibility
        {
            [HarmonyPostfix]
            private static void After(UI_DimensionPocketPanel __instance)
            {
                UI_JournalPanel_SearchOptionButton button;

                if (BondButtons.TryGetValue(__instance, out button) && button != null)
                    button.gameObject.SetActive(ModSettings.FountainArtifactUnlockEnabled);
            }
        }

        private static bool MatchesRarity(ItemEntity item, string rarity)
        {
            if (string.IsNullOrEmpty(rarity)) return true;

            return rarity == BondKey ? IsBond(item) : item.rarity.ToString() == rarity;
        }

        private static void ShowOnly(List<UI_DimensionVaultItemIcon> icons, Func<ItemEntity, bool> match, bool favorite, string tag)
        {
            for (int i = 0; i < icons.Count; i++)
            {
                UI_DimensionVaultItemIcon icon = icons[i];

                if (icon == null || icon.itemIcon == null) continue;

                ItemEntity item = ItemDatabase.FindItemById(icon.itemIcon.Item.entityID);
                bool visible = item != null && match(item);

                if (visible && favorite && !UI_DimensionPocketPanel.IsItemFavorite(item.id))
                    visible = false;

                if (visible && !string.IsNullOrEmpty(tag))
                {
                    if (tag == "NONE")
                        visible = item.categories == null || item.categories.Count == 0;
                    else
                        visible = item.categories != null && item.categories.Contains(tag);
                }

                icon.gameObject.SetActive(visible);
            }
        }

        private static void Hide(List<UI_DimensionVaultItemIcon> icons, Func<ItemEntity, bool> match)
        {
            for (int i = 0; i < icons.Count; i++)
            {
                UI_DimensionVaultItemIcon icon = icons[i];

                if (icon == null || !icon.gameObject.activeSelf || icon.itemIcon == null) continue;

                ItemEntity item = ItemDatabase.FindItemById(icon.itemIcon.Item.entityID);

                if (item != null && match(item))
                    icon.gameObject.SetActive(false);
            }
        }

        [HarmonyPatch(typeof(UI_DimensionPocketPanel), "ApplySearchOption")]
        private static class BondSearch
        {
            [HarmonyPostfix]
            private static void After(UI_DimensionPocketPanel __instance)
            {
                if (__instance == null) return;

                Traverse traverse = Traverse.Create(__instance);
                string rarity = traverse.Field<string>("selectedRarity").Value;
                string tag = traverse.Field<string>("selectedTag").Value;
                bool favorite = traverse.Field<bool>("isFavoriteItemMode").Value;
                List<UI_DimensionVaultItemIcon> icons = traverse.Field<List<UI_DimensionVaultItemIcon>>("icons").Value;

                if (icons == null) return;

                if (tag == FountainTabletUnlock.Key)
                    ShowOnly(icons, item => FountainTabletUnlock.IsTablet(item) && MatchesRarity(item, rarity), favorite, string.Empty);
                else if (rarity == BondKey)
                    ShowOnly(icons, IsBond, favorite, tag);
                else if (!string.IsNullOrEmpty(rarity))
                    Hide(icons, item => IsBond(item) || FountainTabletUnlock.IsTablet(item));

                MethodInfo sorting = AccessTools.Method(typeof(UI_DimensionPocketPanel), "SortingIcons");

                if (sorting != null)
                    sorting.Invoke(__instance, null);
            }
        }

        [HarmonyPatch(typeof(UI_DimensionPocketPanel), "Open")]
        private static class Candidates
        {
            [HarmonyPrefix]
            private static void Add(ref List<ItemMetadata> cachedDimensionPocketItem)
            {
                if (!ModSettings.FountainArtifactUnlockEnabled) return;

                List<ItemMetadata> list = cachedDimensionPocketItem == null ? new List<ItemMetadata>() : new List<ItemMetadata>(cachedDimensionPocketItem);
                HashSet<int> ids = new HashSet<int>();

                foreach (ItemMetadata item in list)
                    ids.Add(item.entityID);

                foreach (int id in ItemDatabase.GetAllItemID())
                {
                    ItemEntity item = ItemDatabase.FindItemById(id);

                    if ((Eligible(item) || FountainTabletUnlock.Eligible(item)) && ids.Add(id))
                        list.Add(new ItemMetadata(-1, id, 1));
                }

                cachedDimensionPocketItem = list;
            }
        }

        [HarmonyPatch(typeof(UI_DimensionPocketPanel), nameof(UI_DimensionPocketPanel.GetCapacity))]
        private static class Capacity
        {
            [HarmonyPostfix]
            private static void Get(EItemRarity rarity, ref int __result)
            {
                if (ModSettings.FountainArtifactUnlockEnabled && rarity > EItemRarity.Legend)
                    __result = rarity == EItemRarity.Eternal ? 7 : 6;
            }
        }

        [HarmonyPatch(typeof(UI_DimensionPocketPanel), "OnDestroy")]
        private static class CleanupButton
        {
            [HarmonyPostfix]
            private static void After(UI_DimensionPocketPanel __instance)
            {
                BondButtons.Remove(__instance);
            }
        }
    }
}
