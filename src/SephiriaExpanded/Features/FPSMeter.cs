using TMPro;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class FPSMeter
    {
        private const string TextName = "SephiriaExpanded_Text_FPSMeter";
        private const float FontSize = 7f;
        private const float SampleInterval = 0.5f;
        private const float FindInterval = 2f;

        private static readonly Vector2 Anchor = new Vector2(0f, 1f);
        private static readonly Vector2 Margin = new Vector2(4f, -4f);
        private static readonly Vector2 Size = new Vector2(60f, 12f);

        private static TMP_Text text;
        private static float nextFind;
        private static float elapsed;
        private static int frames;

        internal static void Tick()
        {
            if (!ModSettings.FPSMeterEnabled)
            {
                if (text != null && text.gameObject.activeSelf) text.gameObject.SetActive(false);
                return;
            }

            if (text == null && !TryCreate()) return;

            if (!text.gameObject.activeSelf) text.gameObject.SetActive(true);

            frames++;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < SampleInterval) return;

            text.text = Mathf.RoundToInt(frames / elapsed) + " FPS";
            frames = 0;
            elapsed = 0f;
        }

        internal static void Cleanup()
        {
            if (text != null) Object.Destroy(text.gameObject);

            text = null;
        }

        private static bool TryCreate()
        {
            if (Time.unscaledTime < nextFind) return false;

            nextFind = Time.unscaledTime + FindInterval;
            if (LocalPlayer.Find() == null) return false;

            text = HudTextFactory.Create(TextName, Anchor, Margin, Size, FontSize, TextAlignmentOptions.TopLeft);
            elapsed = 0f;
            frames = 0;
            return text != null;
        }
    }
}
