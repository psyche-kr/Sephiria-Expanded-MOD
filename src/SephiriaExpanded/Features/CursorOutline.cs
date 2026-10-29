using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UiShadow = UnityEngine.UI.Shadow;

namespace SephiriaExpanded
{
    internal static class CursorOutline
    {
        private const int Thickness = 1;
        private const string SilhouetteShader = "GUI/Text Shader";
        private const string OutlineName = "SephiriaExpanded_CursorOutline";
        private const string FrontName = "SephiriaExpanded_CursorFront";
        private const string LogTag = "[Sephiria Expanded][CursorOutline] ";

        internal static readonly string[] ColorNames = { "검정", "진회색", "남색", "보라", "빨강", "청록", "하늘", "분홍", "노랑", "흰색" };

        private static readonly Color32[] Palette =
        {
            new Color32(0x00, 0x00, 0x00, 0xFF),
            new Color32(0x3A, 0x3A, 0x44, 0xFF),
            new Color32(0x26, 0x30, 0x7A, 0xFF),
            new Color32(0x5A, 0x2A, 0x9A, 0xFF),
            new Color32(0xC4, 0x14, 0x2A, 0xFF),
            new Color32(0x0F, 0x8F, 0x8F, 0xFF),
            new Color32(0x4E, 0xC3, 0xFF, 0xFF),
            new Color32(0xFF, 0x5A, 0xB4, 0xFF),
            new Color32(0xFF, 0xE1, 0x4D, 0xFF),
            new Color32(0xFF, 0xFF, 0xFF, 0xFF)
        };

        private static readonly Color32[] LabelColors =
        {
            new Color32(0xB4, 0xB4, 0xB4, 0xFF),
            new Color32(0x8C, 0x8C, 0x96, 0xFF),
            new Color32(0x6E, 0x80, 0xFF, 0xFF),
            new Color32(0xA7, 0x67, 0xFF, 0xFF),
            new Color32(0xFF, 0x55, 0x55, 0xFF),
            new Color32(0x4D, 0xE3, 0xD8, 0xFF),
            new Color32(0x4E, 0xC3, 0xFF, 0xFF),
            new Color32(0xFF, 0x5A, 0xB4, 0xFF),
            new Color32(0xFF, 0xE1, 0x4D, 0xFF),
            new Color32(0xFF, 0xFF, 0xFF, 0xFF)
        };

        private static readonly Vector2[] Directions = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };

        private sealed class Layer
        {
            internal Image Outline;
            internal Image Front;
            internal UiShadow[] Shadows;
        }

        private static readonly Dictionary<Image, Layer> Layers = new Dictionary<Image, Layer>();
        private static readonly List<Image> Gone = new List<Image>();
        private static Material silhouette;
        private static bool shaderMissingLogged;

        internal static Color?[] ColorChoices()
        {
            Color?[] colors = new Color?[LabelColors.Length];
            for (int i = 0; i < LabelColors.Length; i++)
                colors[i] = LabelColors[i];

            return colors;
        }

        internal static void Tick()
        {
            UI_Cursor cursor = UI_Cursor.Current;
            bool enabled = ModSettings.CursorOutlineEnabled && cursor != null;
            if (enabled)
            {
                Color color = Palette[Mathf.Clamp(ModSettings.CursorOutlineColor, 0, Palette.Length - 1)];
                Apply(cursor.image, color);
                Apply(cursor.battleImage, color);
            }

            Gone.Clear();
            foreach (KeyValuePair<Image, Layer> pair in Layers)
            {
                bool active = enabled && pair.Key != null && (pair.Key == cursor.image || pair.Key == cursor.battleImage);
                if (active) continue;

                Destroy(pair.Value);
                Gone.Add(pair.Key);
            }

            foreach (Image image in Gone)
                Layers.Remove(image);
        }

        internal static void Cleanup()
        {
            foreach (Layer layer in Layers.Values)
                Destroy(layer);

            Layers.Clear();
        }

        private static void Destroy(Layer layer)
        {
            if (layer.Outline != null) Object.Destroy(layer.Outline.gameObject);
            if (layer.Front != null) Object.Destroy(layer.Front.gameObject);
        }

        private static Material Silhouette()
        {
            if (silhouette != null) return silhouette;

            Shader shader = Shader.Find(SilhouetteShader);
            if (shader == null)
            {
                if (!shaderMissingLogged) Debug.LogWarning(LogTag + "단색 셰이더를 찾지 못해 테두리 색이 커서 그림 색과 섞일 수 있습니다.");
                shaderMissingLogged = true;
                return null;
            }

            silhouette = new Material(shader);
            return silhouette;
        }

        private static Image CreateChild(Image source, string name)
        {
            GameObject holder = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = (RectTransform)holder.transform;
            rect.SetParent(source.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = source.rectTransform.pivot;
            Image image = holder.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static Layer Create(Image source)
        {
            Layer layer = new Layer { Outline = CreateChild(source, OutlineName) };
            layer.Front = CreateChild(source, FrontName);
            Material material = Silhouette();
            if (material != null) layer.Outline.material = material;

            layer.Shadows = new UiShadow[Directions.Length * Thickness];
            for (int i = 0; i < layer.Shadows.Length; i++)
            {
                layer.Shadows[i] = layer.Outline.gameObject.AddComponent<UiShadow>();
                layer.Shadows[i].useGraphicAlpha = true;
            }

            Debug.Log(LogTag + "적용: " + source.name + ", 스프라이트 " + (source.sprite != null ? source.sprite.name : "없음") + ", 단색 " + (material != null));
            return layer;
        }

        private static void Apply(Image source, Color color)
        {
            if (source == null) return;

            Layer layer;
            if (!Layers.TryGetValue(source, out layer) || layer.Outline == null || layer.Front == null)
            {
                if (layer != null) Destroy(layer);

                layer = Create(source);
                Layers[source] = layer;
            }

            bool visible = source.enabled && source.sprite != null;
            Sync(layer.Outline, source, visible);
            Sync(layer.Front, source, visible);
            layer.Outline.color = new Color(color.r, color.g, color.b, source.color.a);
            layer.Front.color = source.color;
            if (layer.Front.material != source.material) layer.Front.material = source.material;

            float unit = UnitsPerPixel(source);
            for (int i = 0; i < layer.Shadows.Length; i++)
            {
                UiShadow shadow = layer.Shadows[i];
                if (shadow == null) continue;

                Vector2 distance = Directions[i % Directions.Length] * unit;
                if (shadow.effectDistance != distance) shadow.effectDistance = distance;
                if (shadow.effectColor != color) shadow.effectColor = color;
            }
        }

        private static void Sync(Image target, Image source, bool visible)
        {
            if (target.gameObject.activeSelf != visible) target.gameObject.SetActive(visible);
            if (!visible) return;

            if (target.sprite != source.sprite) target.sprite = source.sprite;
            if (target.type != source.type) target.type = source.type;
            if (target.preserveAspect != source.preserveAspect) target.preserveAspect = source.preserveAspect;
        }

        private static float UnitsPerPixel(Image image)
        {
            Sprite sprite = image.sprite;
            float width = image.rectTransform.rect.width;
            if (sprite == null || sprite.rect.width <= 0f || width <= 0f) return 1f;

            return width / sprite.rect.width;
        }
    }
}
