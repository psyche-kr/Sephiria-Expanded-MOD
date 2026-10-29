using System.Collections.Generic;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class IconColor
    {
        private const int Levels = 16;
        private const byte MinAlpha = 128;
        private const byte MinValue = 64;
        private const float GraySaturation = 0.18f;
        private const float WhiteValue = 0.65f;
        private const float GrayLevel = 0.55f;
        private const float MinBrightness = 0.62f;
        private const int HueBins = 18;
        private const float VividSaturation = 0.25f;
        private const float MinPixelValue = 0.25f;
        private const float VividShare = 0.12f;

        private static readonly Dictionary<Sprite, Color> Cache = new Dictionary<Sprite, Color>();

        internal static Color Of(Sprite sprite, Color fallback)
        {
            if (sprite == null) return fallback;

            Color color;
            if (Cache.TryGetValue(sprite, out color)) return color;

            color = Compute(sprite, fallback);
            Cache[sprite] = color;
            return color;
        }

        internal static void Clear()
        {
            Cache.Clear();
        }

        private static Color Compute(Sprite sprite, Color fallback)
        {
            Color32[] pixels = ReadPixels(sprite);
            if (pixels == null) return fallback;

            Color? vivid = Vivid(pixels);
            if (vivid.HasValue) return Brighten(vivid.Value);

            int[] counts = new int[Levels * Levels * Levels];
            int[] reds = new int[counts.Length];
            int[] greens = new int[counts.Length];
            int[] blues = new int[counts.Length];
            int bestKey = -1;
            foreach (Color32 pixel in pixels)
            {
                if (pixel.a < MinAlpha || System.Math.Max(pixel.r, System.Math.Max(pixel.g, pixel.b)) < MinValue) continue;

                int key = (pixel.r >> 4) * Levels * Levels + (pixel.g >> 4) * Levels + (pixel.b >> 4);
                counts[key]++;
                reds[key] += pixel.r;
                greens[key] += pixel.g;
                blues[key] += pixel.b;
                if (bestKey < 0 || counts[key] > counts[bestKey]) bestKey = key;
            }

            if (bestKey < 0) return fallback;

            Color dominant = new Color(reds[bestKey] / (255f * counts[bestKey]), greens[bestKey] / (255f * counts[bestKey]), blues[bestKey] / (255f * counts[bestKey]));
            float hue;
            float saturation;
            float value;
            Color.RGBToHSV(dominant, out hue, out saturation, out value);
            if (saturation < GraySaturation && value > WhiteValue) return new Color(GrayLevel, GrayLevel, GrayLevel);

            return Brighten(dominant);
        }

        private static Color? Vivid(Color32[] pixels)
        {
            int opaque = 0;
            int vivid = 0;
            int[] counts = new int[HueBins];
            float[] reds = new float[HueBins];
            float[] greens = new float[HueBins];
            float[] blues = new float[HueBins];
            foreach (Color32 pixel in pixels)
            {
                if (pixel.a < MinAlpha) continue;

                opaque++;
                float hue;
                float saturation;
                float value;
                Color.RGBToHSV(pixel, out hue, out saturation, out value);
                if (saturation < VividSaturation || value < MinPixelValue) continue;

                int bin = Mathf.Min(HueBins - 1, (int)(hue * HueBins));
                vivid++;
                counts[bin]++;
                reds[bin] += pixel.r;
                greens[bin] += pixel.g;
                blues[bin] += pixel.b;
            }

            if (opaque == 0 || vivid < opaque * VividShare) return null;

            int best = 0;
            for (int i = 1; i < HueBins; i++)
                if (counts[i] > counts[best]) best = i;

            return new Color(reds[best] / (255f * counts[best]), greens[best] / (255f * counts[best]), blues[best] / (255f * counts[best]));
        }

        private static Color Brighten(Color color)
        {
            float hue;
            float saturation;
            float value;
            Color.RGBToHSV(color, out hue, out saturation, out value);
            return Color.HSVToRGB(hue, saturation, Mathf.Max(value, MinBrightness));
        }

        internal static Color32[] ReadPixels(Sprite sprite)
        {
            Texture2D texture = sprite.texture;
            Rect rect = sprite.textureRect;
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            if (texture == null || width <= 0 || height <= 0) return null;

            RenderTexture buffer = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, buffer);
                RenderTexture.active = buffer;
                copy.ReadPixels(new Rect(rect.x, rect.y, width, height), 0, 0);
                return copy.GetPixels32();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[Sephiria Expanded] 아이콘 색 추출 실패: " + exception.Message);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(buffer);
                Object.Destroy(copy);
            }
        }
    }
}
