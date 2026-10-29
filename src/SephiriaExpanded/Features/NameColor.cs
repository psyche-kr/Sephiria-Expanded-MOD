using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class NameColor
    {
        private const string OffLabel = "끄기";
        private const string NoneLabel = "없음";

        private static readonly Regex Tags = new Regex("<[^>]*>");
        private static readonly System.Collections.Generic.List<Color> stops = new System.Collections.Generic.List<Color>();

        private static readonly string[] PaletteNames = { "빨강", "주황", "노랑", "연두", "초록", "청록", "하늘", "파랑", "보라", "분홍", "흰색", "검정" };

        private static readonly Color32[] Palette =
        {
            new Color32(255, 85, 85, 255),
            new Color32(255, 162, 59, 255),
            new Color32(255, 225, 77, 255),
            new Color32(166, 240, 77, 255),
            new Color32(77, 224, 123, 255),
            new Color32(77, 227, 216, 255),
            new Color32(92, 192, 255, 255),
            new Color32(92, 124, 255, 255),
            new Color32(167, 103, 255, 255),
            new Color32(255, 124, 224, 255),
            new Color32(255, 255, 255, 255),
            new Color32(0, 0, 0, 255)
        };

        internal static int PaletteSize
        {
            get { return Palette.Length; }
        }

        internal static string[] Labels(string first)
        {
            string[] labels = new string[PaletteNames.Length + 1];
            labels[0] = first;
            PaletteNames.CopyTo(labels, 1);
            return labels;
        }

        internal static string[] StartLabels()
        {
            return Labels(OffLabel);
        }

        internal static string[] EndLabels()
        {
            return Labels(NoneLabel);
        }

        internal static Color?[] Colors(Color? first)
        {
            Color?[] colors = new Color?[Palette.Length + 1];
            colors[0] = first;
            for (int i = 0; i < Palette.Length; i++)
                colors[i + 1] = Palette[i];

            return colors;
        }

        internal static string Decorate(string name)
        {
            string plain = Tags.Replace(name ?? string.Empty, string.Empty);
            int start = ModSettings.NameColorStart;
            if (start <= 0 || start > Palette.Length || string.IsNullOrEmpty(plain)) return plain;

            stops.Clear();
            stops.Add(Palette[start - 1]);
            AddStop(ModSettings.NameColorA);
            AddStop(ModSettings.NameColorB);
            if (stops.Count == 1) return "<color=#" + ColorUtility.ToHtmlStringRGB(stops[0]) + ">" + plain + "</color>";

            int visible = 0;
            foreach (char letter in plain)
                if (!char.IsWhiteSpace(letter)) visible++;

            StringBuilder builder = new StringBuilder();
            int index = 0;
            foreach (char letter in plain)
            {
                if (char.IsWhiteSpace(letter))
                {
                    builder.Append(letter);
                    continue;
                }

                builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(Blend(visible <= 1 ? 0f : (float)index / (visible - 1)))).Append('>').Append(letter).Append("</color>");
                index++;
            }

            return builder.ToString();
        }

        private static void AddStop(int value)
        {
            if (value > 0 && value <= Palette.Length) stops.Add(Palette[value - 1]);
        }

        private static Color Blend(float t)
        {
            float position = Mathf.Clamp01(t) * (stops.Count - 1);
            int segment = Mathf.Min((int)position, stops.Count - 2);
            return Color.Lerp(stops[segment], stops[segment + 1], position - segment);
        }

        internal static void Refresh()
        {
            PlayerAvatar local = LocalPlayer.Find();
            if (local != null) local.SetPlayerName(local.playerNameSource);
        }

        [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.SetPlayerName))]
        private static class SetName
        {
            [HarmonyPrefix]
            private static void Before(PlayerAvatar __instance, ref string name)
            {
                if (__instance.isLocalPlayer) name = Decorate(name);
            }
        }

        [HarmonyPatch(typeof(UI_RenameButton), nameof(UI_RenameButton.Rename))]
        private static class Rename
        {
            [HarmonyPostfix]
            private static void After()
            {
                Refresh();
            }
        }
    }
}
