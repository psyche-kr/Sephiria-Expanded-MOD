using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaExpanded
{
    internal static class BossHpText
    {
        private const string LabelName = "SephiriaExpanded_BossHpText";
        private static readonly List<UI_BossHPBar> BossBars = new List<UI_BossHPBar>();
        private static readonly List<UI_MiniBossHPBar> MiniBossBars = new List<UI_MiniBossHPBar>();
        private static float nextRefreshTime;
        private static bool hasLabels;

        internal static void Tick()
        {
            if (Time.unscaledTime < nextRefreshTime)
                return;

            nextRefreshTime = Time.unscaledTime + 0.5f;

            if (!ModSettings.BossHpTextEnabled)
            {
                RemoveAll();
                return;
            }

            AttachAll();
        }

        private static void AttachAll()
        {
            UIManager manager = UIManager.Instance;
            UIRoot root = manager != null ? manager.GetRootFromType(EUIObjectPoolingParent.HUD) : null;
            if (root != null)
            {
                BossBars.Clear();
                root.GetComponentsInChildren(true, BossBars);
                foreach (UI_BossHPBar bar in BossBars)
                    AttachBoss(bar);
            }

            GameObject pool = SingletonObject.Find("MiniBossHPBarPool");
            if (pool == null)
                return;

            MiniBossBars.Clear();
            pool.GetComponentsInChildren(true, MiniBossBars);
            foreach (UI_MiniBossHPBar bar in MiniBossBars)
                Attach(bar, bar.valueImage, bar.nameText, HpLabel.MiniBossAvatarField);
        }

        private static void RemoveAll()
        {
            if (!hasLabels) return;

            hasLabels = false;
            foreach (HpLabel label in Resources.FindObjectsOfTypeAll<HpLabel>())
            {
                if (label != null)
                    UnityEngine.Object.Destroy(label.gameObject);
            }
        }

        private static void AttachBoss(UI_BossHPBar bar)
        {
            if (bar == null)
                return;

            Attach(bar, bar.barImage, bar.nameText, HpLabel.BossAvatarField);

            UI_LibraryBossHPBar libraryBar = bar as UI_LibraryBossHPBar;
            if (libraryBar != null)
                Attach(libraryBar, libraryBar.golemHPBarImage, libraryBar.nameText, HpLabel.GolemAvatarField);
        }

        private static void Attach(MonoBehaviour bar, Image fill, TextMeshProUGUI source, FieldInfo avatarField)
        {
            if (!ModSettings.BossHpTextEnabled || bar == null || fill == null || source == null || avatarField == null)
                return;

            foreach (HpLabel label in bar.GetComponentsInChildren<HpLabel>(true))
            {
                if (label.Fill == fill.rectTransform)
                    return;
            }

            GameObject labelObject = UnityEngine.Object.Instantiate(source.gameObject, bar.transform, false);
            labelObject.name = LabelName;

            foreach (Component component in labelObject.GetComponents<Component>())
            {
                if (component is RectTransform || component is CanvasRenderer || component is TextMeshProUGUI)
                    continue;

                UnityEngine.Object.Destroy(component);
            }

            for (int i = labelObject.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(labelObject.transform.GetChild(i).gameObject);

            TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
            RectTransform rect = labelObject.transform as RectTransform;
            if (text == null || rect == null)
            {
                UnityEngine.Object.Destroy(labelObject);
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.SetAsLastSibling();

            text.fontSize = source.fontSize * 0.72f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.enableVertexGradient = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = string.Empty;

            HpLabel hpLabel = labelObject.AddComponent<HpLabel>();
            hpLabel.Bind(bar, text, avatarField, fill.rectTransform);
            hasLabels = true;
        }

        [HarmonyPatch(typeof(UI_BossHPBar), "OnOpened")]
        private static class BossBarOpened
        {
            [HarmonyPostfix]
            private static void After(UI_BossHPBar __instance)
            {
                AttachBoss(__instance);
            }
        }

        [HarmonyPatch(typeof(UI_MiniBossHPBar), "SetTarget", new[] { typeof(UnitAvatar) })]
        private static class MiniBossTargeted
        {
            [HarmonyPostfix]
            private static void After(UI_MiniBossHPBar __instance)
            {
                if (__instance != null)
                    Attach(__instance, __instance.valueImage, __instance.nameText, HpLabel.MiniBossAvatarField);
            }
        }

        private sealed class HpLabel : MonoBehaviour
        {
            internal static readonly FieldInfo BossAvatarField = typeof(UI_BossHPBar).GetField("bossAvatar", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            internal static readonly FieldInfo MiniBossAvatarField = typeof(UI_UnitHPBar).GetField("target", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            internal static readonly FieldInfo GolemAvatarField = typeof(UI_LibraryBossHPBar).GetField("golemAvatar", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            private MonoBehaviour bar;
            private TextMeshProUGUI text;
            private FieldInfo avatarField;
            private RectTransform fill;
            private RectTransform rect;
            private float shownHp = float.NaN;
            private float shownMaxHp = float.NaN;
            private int shownFormat = -1;
            private string shownPhase;
            private bool styled;
            private static TextMeshProUGUI fontSource;

            internal RectTransform Fill => fill;

            internal void Bind(MonoBehaviour sourceBar, TextMeshProUGUI sourceText, FieldInfo sourceAvatarField, RectTransform sourceFill)
            {
                bar = sourceBar;
                text = sourceText;
                avatarField = sourceAvatarField;
                fill = sourceFill;
                rect = transform as RectTransform;
                Place();
            }

            private void LateUpdate()
            {
                if (!ModSettings.BossHpTextEnabled)
                {
                    UnityEngine.Object.Destroy(gameObject);
                    return;
                }

                if (bar == null || text == null || avatarField == null || fill == null || !fill.gameObject.activeInHierarchy)
                {
                    if (text != null)
                        text.text = string.Empty;
                    return;
                }

                UnitAvatar avatar = avatarField.GetValue(bar) as UnitAvatar;
                if (avatar == null)
                {
                    text.text = string.Empty;
                    return;
                }

                Place();
                if (!styled) ApplyStyle();

                bool isDualBarPilot = bar is UI_LibraryBossHPBar && avatarField == BossAvatarField;
                string phase = isDualBarPilot ? FormatPhase(avatar) : null;
                if (phase != null)
                {
                    if (phase == shownPhase) return;

                    shownPhase = phase;
                    text.text = phase;
                    return;
                }

                float hp = avatar.hp;
                float maxHp = avatar.MaxHp;
                if (Mathf.Abs(hp - shownHp) < 0.5f && Mathf.Abs(maxHp - shownMaxHp) < 0.5f && shownFormat == ModSettings.BossHpTextFormat)
                    return;

                shownHp = hp;
                shownMaxHp = maxHp;
                shownFormat = ModSettings.BossHpTextFormat;
                text.text = FormatText(hp, maxHp);
            }

            private static string FormatPhase(UnitAvatar avatar)
            {
                UnitAI_BossBasic ai = avatar != null ? avatar.GetComponent<UnitAI_BossBasic>() : null;
                if (ai == null || ai.Life <= 0) return null;

                int total = ai.Life + 1;
                int current = ai.Life - ai.CurrentLife + 1;
                return current + " / " + total;
            }

            private static string FormatText(float hp, float maxHp)
            {
                string current = Format(hp);
                string max = Format(maxHp);
                string percent = maxHp > 0f ? Percent(hp, maxHp).ToString("0.0", CultureInfo.InvariantCulture) + "%" : "-%";

                switch (ModSettings.BossHpTextFormat)
                {
                    case 1: return current;
                    case 2: return current + " (" + percent + ")";
                    case 3: return percent;
                    case 4: return current + " / " + max + " (" + percent + ")";
                    default: return current + " / " + max;
                }
            }

            private static float Percent(float hp, float maxHp)
            {
                float percent = Mathf.Round(Mathf.Clamp01(hp / maxHp) * 1000f) / 10f;
                if (hp < maxHp) percent = Mathf.Min(percent, 99.9f);
                if (hp > 0f) percent = Mathf.Max(percent, 0.1f);
                return percent;
            }

            private void ApplyStyle()
            {
                TextMeshProUGUI source = FontSource();
                if (source == null || source.font == null) return;

                text.font = source.font;
                text.fontSharedMaterial = source.fontSharedMaterial;
                text.enableAutoSizing = false;
                text.fontSize = source.fontSize;
                text.fontStyle = source.fontStyle;
                text.characterSpacing = source.characterSpacing;
                styled = true;
                Debug.Log("[Sephiria Expanded][BossHpText] 글꼴 적용: " + source.name + " (" + source.GetComponentInParent<UIBase>()?.GetType().Name + "), 글꼴 " + source.font.name + ", 크기 " + source.fontSize);
            }

            private static TextMeshProUGUI FontSource()
            {
                if (fontSource != null) return fontSource;

                UIManager manager = UIManager.Instance;
                UIRoot root = manager != null ? manager.GetRootFromType(EUIObjectPoolingParent.HUD) : null;
                if (root == null) return null;

                UI_CurrentQuickSlotHUD quickSlot = root.GetComponentInChildren<UI_CurrentQuickSlotHUD>(true);
                if (quickSlot != null && quickSlot.quantityText != null) return fontSource = quickSlot.quantityText;

                UI_HPBar hpBar = root.GetComponentInChildren<UI_HPBar>(true);
                return fontSource = hpBar != null ? hpBar.valueText : null;
            }

            private void Place()
            {
                if (rect == null || fill == null || rect.parent == null)
                    return;

                rect.sizeDelta = fill.rect.size;
                rect.localPosition = rect.parent.InverseTransformPoint(fill.TransformPoint(fill.rect.center));
            }

            private static string Format(float value)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    return "-";

                return Math.Max(0, Math.Ceiling(value)).ToString("N0", CultureInfo.InvariantCulture);
            }
        }
    }
}
