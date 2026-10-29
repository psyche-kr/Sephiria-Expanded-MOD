using TMPro;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class HudTextFactory
    {
        private const string SourceName = "Count";
        private const string SourceParentName = "CurrentQuickSlot";
        private const string HudName = "[UI] HUD";

        internal static TMP_Text Create(string name, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            Transform source = FindSource();
            if (source == null) return null;

            Transform hud = source.parent;
            while (hud != null && hud.name != HudName) hud = hud.parent;
            if (hud == null) hud = source.parent.parent;
            if (hud == null) return null;

            GameObject clone = Object.Instantiate(source.gameObject, hud, false);
            clone.name = name;
            clone.transform.SetAsLastSibling();

            foreach (UI_LocalizationStringText localization in clone.GetComponentsInChildren<UI_LocalizationStringText>(true))
                localization.enabled = false;

            TMP_Text text = clone.GetComponent<TMP_Text>();
            if (text == null)
            {
                Object.Destroy(clone);
                return null;
            }

            RectTransform rect = text.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;

            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.color = Color.white;
            text.text = string.Empty;

            clone.SetActive(true);
            return text;
        }

        private static Transform FindSource()
        {
            UIManager manager = UIManager.Instance;
            Transform hud = manager == null ? null : manager.GetElement("HUD") ?? manager.GetElement(HudName);
            Transform found = hud == null ? null : hud.Find(SourceParentName + "/" + SourceName);
            if (found != null) return found;

            foreach (TMP_Text candidate in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (candidate == null || candidate.name != SourceName || !candidate.gameObject.scene.IsValid()) continue;

                Transform parent = candidate.transform.parent;
                if (parent != null && parent.name == SourceParentName) return candidate.transform;
            }

            return null;
        }
    }
}
