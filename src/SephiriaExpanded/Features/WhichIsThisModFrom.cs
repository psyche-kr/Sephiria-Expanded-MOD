using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaExpanded
{
    internal static class WhichIsThisModFrom
    {
        private const string TextName = "SephiriaExpanded_Text_WhichModeInfoText";
        private const string UnknownMod = "Unknown";
        private const string TextFormat = "<color=#FFFFFF>모드 : </color><color=#FFFF00>{0}</color>";

        private static readonly string[] IgnoredAssemblies = { "mscorlib", "System", "Unity", "Assembly-CSharp", "0Harmony", "Mono", "netstandard", "Mirror", "Newtonsoft" };
        private static readonly Dictionary<int, string> ModNames = new Dictionary<int, string>();
        private static readonly Dictionary<UI_BaseTooltip, TMP_Text> Texts = new Dictionary<UI_BaseTooltip, TMP_Text>();

        internal static void Cleanup()
        {
            foreach (TMP_Text text in Texts.Values)
                if (text != null) Object.Destroy(text.gameObject);

            Texts.Clear();
            ModNames.Clear();
        }

        private static string FindCallerMod()
        {
            foreach (StackFrame frame in new StackTrace(2, false).GetFrames())
            {
                Assembly assembly = frame.GetMethod()?.DeclaringType?.Assembly;
                if (assembly == null || assembly.IsDynamic || assembly == typeof(WhichIsThisModFrom).Assembly || IsIgnored(assembly.GetName().Name)) continue;

                foreach (AddOnLoader.LoadedAddOn addOn in AddOnLoader.LoadedMods)
                    if (addOn.Assembly == assembly) return addOn.Metadata.modName;

                return assembly.GetName().Name;
            }

            return UnknownMod;
        }

        private static bool IsIgnored(string assemblyName)
        {
            foreach (string prefix in IgnoredAssemblies)
                if (assemblyName.StartsWith(prefix, System.StringComparison.Ordinal)) return true;

            return false;
        }

        private static ItemEntity FindItem(ITooltip data)
        {
            return data is IItemEntity entity ? ItemDatabase.FindItemById(entity.IEntityID) : data as ItemEntity;
        }

        private static void Show(UI_BaseTooltip tooltip, ItemEntity item)
        {
            string modName = null;
            bool visible = ModSettings.WhichIsThisModFromEnabled && item != null && tooltip.attachedWorldTarget == null && ModNames.TryGetValue(item.id, out modName);

            TMP_Text text;
            if (!Texts.TryGetValue(tooltip, out text) || text == null)
            {
                if (!visible) return;

                text = CreateText(tooltip);
                if (text == null) return;

                Texts[tooltip] = text;
            }

            if (visible) text.text = string.Format(TextFormat, modName);
            if (text.gameObject.activeSelf != visible) text.gameObject.SetActive(visible);
            if (visible) LayoutRebuilder.ForceRebuildLayoutImmediate(text.transform.parent as RectTransform);
        }

        private static TMP_Text FindAnchor(UI_BaseTooltip tooltip)
        {
            UI_StoneTabletTooltip tablet = tooltip as UI_StoneTabletTooltip;
            if (tablet != null) return tablet.typeText;

            Traverse traverse = Traverse.Create(tooltip);
            return traverse.Field<TMP_Text>("flavorText").Value ?? traverse.Field<TMP_Text>("nameText").Value;
        }

        private static TMP_Text CreateText(UI_BaseTooltip tooltip)
        {
            TMP_Text anchor = FindAnchor(tooltip);
            if (anchor == null) return null;

            GameObject clone = Object.Instantiate(anchor.gameObject, anchor.transform.parent, false);
            clone.name = TextName;
            clone.transform.SetSiblingIndex(anchor.transform.GetSiblingIndex() + 1);
            clone.SetActive(false);

            foreach (UI_LocalizationStringText localization in clone.GetComponentsInChildren<UI_LocalizationStringText>(true))
                localization.enabled = false;

            TMP_Text text = clone.GetComponent<TMP_Text>();
            if (text != null) text.richText = true;

            return text;
        }

        [HarmonyPatch(typeof(ItemDatabase), nameof(ItemDatabase.Initialize))]
        private static class Reset
        {
            [HarmonyPrefix]
            private static void Before()
            {
                ModNames.Clear();
            }
        }

        [HarmonyPatch(typeof(ItemDatabase), nameof(ItemDatabase.Register))]
        private static class Registered
        {
            [HarmonyPostfix]
            private static void After(ItemEntity item)
            {
                if (item == null || ItemDatabase.FindItemById(item.id) != item) return;

                ModNames[item.id] = FindCallerMod();
            }
        }

        [HarmonyPatch(typeof(UI_CharmTooltip), "UpdateData")]
        private static class CharmUpdated
        {
            [HarmonyPostfix]
            private static void After(UI_CharmTooltip __instance, ITooltip data)
            {
                Show(__instance, FindItem(data));
            }
        }

        [HarmonyPatch(typeof(UI_ItemTooltip), nameof(UI_ItemTooltip.Open), new[] { typeof(IUITooltipOpener), typeof(RectTransform), typeof(Vector2), typeof(ITooltip) })]
        private static class ItemOpened
        {
            [HarmonyPostfix]
            private static void After(UI_ItemTooltip __instance, ITooltip data)
            {
                Show(__instance, FindItem(data));
            }
        }

        [HarmonyPatch(typeof(UI_StoneTabletTooltip), nameof(UI_StoneTabletTooltip.Open), new[] { typeof(IUITooltipOpener), typeof(RectTransform), typeof(Vector2), typeof(ITooltip) })]
        private static class TabletOpened
        {
            [HarmonyPostfix]
            private static void After(UI_StoneTabletTooltip __instance, ITooltip data)
            {
                Show(__instance, FindItem(data));
            }
        }

        [HarmonyPatch(typeof(UI_BaseTooltip), nameof(UI_BaseTooltip.AttachToWorldTarget))]
        private static class WorldAttached
        {
            [HarmonyPostfix]
            private static void After(UI_BaseTooltip __instance)
            {
                Show(__instance, null);
            }
        }
    }
}
