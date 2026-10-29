using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class LevelUpItemCount
    {
        private const string LabelName = "SephiriaExpanded_LevelUpItemCount";
        private static readonly FieldInfo AvatarController = AccessTools.Field(typeof(UI_SephiriteRewardPanel), "avatarToLevelController");
        private static readonly FieldInfo RewardElements = AccessTools.Field(typeof(UI_SephiriteRewardPanel), "rewardElements");
        private static TextMeshProUGUI label;
        private static int shown = -1;

        internal static void Tick()
        {
            try
            {
                UI_SephiriteRewardPanel panel = GetPanel();
                if (!ModSettings.LevelUpItemCountEnabled || panel == null)
                {
                    Hide();
                    return;
                }

                int remaining = Remaining(panel);
                if (remaining < 2)
                {
                    Hide();
                    return;
                }

                TextMeshProUGUI target = GetLabel(panel);
                if (target == null) return;
                if (!target.gameObject.activeSelf) target.gameObject.SetActive(true);
                if (shown == remaining) return;

                shown = remaining;
                target.text = "남은 보상 " + remaining + "회";
            }
            catch
            {
            }
        }

        internal static void Cleanup()
        {
            try
            {
                if (label != null) UnityEngine.Object.Destroy(label.gameObject);
            }
            catch
            {
            }
            label = null;
            shown = -1;
        }

        private static UI_SephiriteRewardPanel GetPanel()
        {
            try
            {
                UIManager manager = UIManager.Instance;
                UI_SephiriteRewardPanel panel = manager == null ? null : manager.GetElement<UI_SephiriteRewardPanel>();
                return panel != null && panel.IsOpened ? panel : null;
            }
            catch
            {
                return null;
            }
        }

        private static int Remaining(UI_SephiriteRewardPanel panel)
        {
            if (AvatarController == null || RewardElements == null) return 0;

            LevelController controller = AvatarController.GetValue(panel) as LevelController;
            if (controller == null || controller.AvailableStatPoint <= 0 || controller.levelUpQueue == null || controller.levelUpQueue.Count == 0)
                return 0;

            if (controller.levelUpQueue[0] == null || controller.levelUpQueue[0] != panel.sephirite)
                return 0;

            List<UI_SephiriteRewardElement> rewards = RewardElements.GetValue(panel) as List<UI_SephiriteRewardElement>;
            return rewards == null || rewards.Count == 0 ? 0 : controller.AvailableStatPoint;
        }

        private static TextMeshProUGUI GetLabel(UI_SephiriteRewardPanel panel)
        {
            if (label != null) return label;
            if (panel.rewardZone == null) return null;

            Transform existing = panel.rewardZone.Find(LabelName);
            if (existing != null)
            {
                label = existing.GetComponent<TextMeshProUGUI>();
                if (label != null) return label;
                UnityEngine.Object.Destroy(existing.gameObject);
            }

            TextMeshProUGUI source = panel.rerollGuideText != null
                ? panel.rerollGuideText
                : panel.skipSelectText != null
                    ? panel.skipSelectText
                    : panel.convertRerollDiceText;
            if (source == null) return null;

            GameObject objectRoot = new GameObject(LabelName, typeof(RectTransform));
            RectTransform rect = (RectTransform)objectRoot.transform;
            rect.SetParent(panel.rewardZone, false);
            RectTransform center = panel.rewardCenterPosition;
            rect.anchorMin = center == null ? new Vector2(0.5f, 0.5f) : center.anchorMin;
            rect.anchorMax = center == null ? new Vector2(0.5f, 0.5f) : center.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center == null ? Vector2.zero : center.anchoredPosition;
            rect.sizeDelta = new Vector2(108f, 34f);
            rect.localScale = Vector3.one;
            rect.SetAsFirstSibling();

            label = objectRoot.AddComponent<TextMeshProUGUI>();
            label.font = source.font;
            label.fontSharedMaterial = source.fontSharedMaterial;
            label.fontSize = 13f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 9f;
            label.fontSizeMax = 13f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.92f, 0.62f, 1f);
            label.richText = false;
            label.raycastTarget = false;
            label.text = string.Empty;
            return label;
        }

        private static void Hide()
        {
            if (label != null && label.gameObject.activeSelf)
                label.gameObject.SetActive(false);
            shown = -1;
        }
    }
}

