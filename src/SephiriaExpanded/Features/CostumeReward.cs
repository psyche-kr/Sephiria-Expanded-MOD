using System;
using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaExpanded
{
    public class CostumeFrameWatcher : MonoBehaviour
    {
        private const int Threshold = 60;
        private const float PanelSearchInterval = 2f;
        private const EItemRarity NameRarity = EItemRarity.Rare;

        private static CostumeFrameWatcher instance;

        private UI_CostumePanel panel;
        private UI_CostumeListElement[] elements;
        private float rescanTimer;
        private bool loggedOnce;
        private bool applied;

        private readonly HashSet<int> styled = new HashSet<int>();
        private readonly Dictionary<TMP_Text, OriginalStyle> originalStyles = new Dictionary<TMP_Text, OriginalStyle>();

        private sealed class OriginalStyle
        {
            internal Color Color;
            internal VertexGradient Gradient;
            internal bool Enabled;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void OnDestroy()
        {
            RestoreAll();

            if (instance == this) instance = null;
        }

        internal static void ForceRefresh()
        {
            if (instance == null) return;

            instance.rescanTimer = 0f;
            instance.RefreshTargets(true);

            if (ModSettings.CostumeFrameWatcherEnabled)
                instance.Apply();
            else
                instance.RestoreAll();
        }

        internal static void ForceRefresh(UI_CostumePanel target)
        {
            if (instance == null) return;

            instance.panel = target;
            instance.rescanTimer = 0f;
            instance.RefreshTargets(true);

            if (ModSettings.CostumeFrameWatcherEnabled)
                instance.Apply();
            else
                instance.RestoreAll();
        }

        private void LateUpdate()
        {
            try
            {
                if (!ModSettings.CostumeFrameWatcherEnabled)
                {
                    if (applied)
                    {
                        RestoreAll();
                        applied = false;
                    }

                    return;
                }

                RefreshTargets(false);
                Apply();
                applied = true;
            }
            catch (Exception exception)
            {
                if (loggedOnce) return;

                loggedOnce = true;
                Debug.LogWarning(exception);
            }
        }

        private void RefreshTargets(bool force)
        {
            rescanTimer -= Time.unscaledDeltaTime;

            if (panel == null)
            {
                if (!force && rescanTimer > 0f) return;

                UI_CostumePanel[] panels = Resources.FindObjectsOfTypeAll<UI_CostumePanel>();

                if (panels != null)
                {
                    for (int i = 0; i < panels.Length; i++)
                    {
                        if (panels[i] == null) continue;

                        panel = panels[i];
                        break;
                    }
                }

                if (panel == null)
                {
                    rescanTimer = PanelSearchInterval;
                    return;
                }
            }

            if (!force && elements != null && rescanTimer > 0f) return;

            elements = panel.GetComponentsInChildren<UI_CostumeListElement>(true);
            rescanTimer = panel.gameObject.activeInHierarchy ? 0.1f : 0.5f;
        }

        private void Apply()
        {
            if (elements == null || elements.Length == 0) return;

            SaveData save = SaveManager.Current;

            if (save == null) return;

            for (int i = 0; i < elements.Length; i++)
            {
                UI_CostumeListElement element = elements[i];

                if (element == null || !element.gameObject.activeInHierarchy) continue;

                CostumeEntity costume = element.Costume;

                if (costume == null || string.IsNullOrEmpty(costume.id)) continue;

                int point = save.GetInt("HardModeClearedHighestPoint_Costume_" + costume.id, 0);
                bool top = point >= Threshold;

                int key = element.GetInstanceID();
                TMP_Text nameText = element.nameText;

                if (nameText != null)
                {
                    if (top)
                    {
                        if (!originalStyles.ContainsKey(nameText))
                        {
                            originalStyles.Add(nameText, new OriginalStyle
                            {
                                Color = nameText.color,
                                Gradient = nameText.colorGradient,
                                Enabled = nameText.enableVertexGradient
                            });
                        }

                        if (!nameText.enableVertexGradient)
                            ApplyDualNameStyle(nameText);

                        styled.Add(key);
                    }
                    else if (styled.Contains(key))
                    {
                        OriginalStyle old;

                        if (originalStyles.TryGetValue(nameText, out old))
                        {
                            nameText.color = old.Color;
                            nameText.colorGradient = old.Gradient;
                            nameText.enableVertexGradient = old.Enabled;

                            originalStyles.Remove(nameText);
                        }

                        styled.Remove(key);
                    }
                }

                UpdateAura(element, top);
            }
        }

        private void RestoreAll()
        {
            foreach (var pair in originalStyles)
            {
                if (pair.Key == null) continue;

                pair.Key.color = pair.Value.Color;
                pair.Key.colorGradient = pair.Value.Gradient;
                pair.Key.enableVertexGradient = pair.Value.Enabled;
            }

            originalStyles.Clear();
            styled.Clear();

            if (elements == null) return;

            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i] != null) UpdateAura(elements[i], false);
            }
        }

        private void ApplyDualNameStyle(TMP_Text text)
        {
            KeywordEntity dualKeyword = KeywordDatabase.GetEntity("ItemRarity_Dual");

            if (dualKeyword == null) return;

            Color top = ItemDatabase.GetColorViaItemRarity(NameRarity);
            Color bottom = dualKeyword.textColor;

            text.color = Color.white;
            text.colorGradient = new VertexGradient(top, top, bottom, bottom);
            text.enableVertexGradient = true;
        }

        private void UpdateAura(UI_CostumeListElement element, bool wanted)
        {
            if (element.iconImage == null) return;

            RectTransform slot = element.iconImage.rectTransform.parent as RectTransform;

            if (slot == null) slot = element.iconImage.rectTransform;

            IconAura aura = slot.GetComponent<IconAura>();

            if (wanted)
            {
                if (aura == null) slot.gameObject.AddComponent<IconAura>();
            }
            else if (aura != null)
            {
                aura.Cleanup();
                Destroy(aura);
            }
        }

        [HarmonyPatch(typeof(UI_CostumePanel), "OnOpened")]
        private static class CostumePanelOpened
        {
            [HarmonyPostfix]
            private static void After(UI_CostumePanel __instance)
            {
                ForceRefresh(__instance);
            }
        }
    }

    public class IconAura : MonoBehaviour
    {
        private const int Count = 10;

        private static readonly Color AuraColor = new Color(1f, 0.45f, 0.25f);
        private static Sprite dot;

        private RectTransform area;
        private RectTransform[] parts;
        private Image[] images;
        private float[] phase;
        private float[] speed;
        private float[] x;
        private float[] size;

        private void Awake()
        {
            area = transform as RectTransform;

            if (area == null)
            {
                enabled = false;
                return;
            }

            EnsureDot();

            parts = new RectTransform[Count];
            images = new Image[Count];
            phase = new float[Count];
            speed = new float[Count];
            x = new float[Count];
            size = new float[Count];

            Rect rect = area.rect;

            for (int i = 0; i < Count; i++)
            {
                GameObject obj = new GameObject("HM60_Aura_" + i, typeof(RectTransform));

                RectTransform rt = obj.GetComponent<RectTransform>();
                rt.SetParent(area, false);
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                Image image = obj.AddComponent<Image>();

                image.sprite = dot;
                image.raycastTarget = false;
                image.color = AuraColor;

                parts[i] = rt;
                images[i] = image;
                phase[i] = UnityEngine.Random.value;
                speed[i] = UnityEngine.Random.Range(0.45f, 0.8f);
                x[i] = UnityEngine.Random.Range(-rect.width * 0.42f, rect.width * 0.42f);
                size[i] = UnityEngine.Random.Range(3f, 6f);

                rt.sizeDelta = new Vector2(size[i], size[i]);
            }
        }

        private void Update()
        {
            if (parts == null || area == null) return;

            Rect rect = area.rect;
            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < Count; i++)
            {
                if (parts[i] == null) continue;

                phase[i] += dt * speed[i];

                if (phase[i] >= 1f)
                {
                    phase[i] -= 1f;
                    x[i] = UnityEngine.Random.Range(-rect.width * 0.42f, rect.width * 0.42f);
                    size[i] = UnityEngine.Random.Range(3f, 6f);
                    parts[i].sizeDelta = new Vector2(size[i], size[i]);
                }

                float t = phase[i];
                float y = Mathf.Lerp(-rect.height * 0.45f, rect.height * 0.55f, t);
                float wobble = Mathf.Sin(t * 6.283f + i) * 2.5f;

                parts[i].anchoredPosition = new Vector2(x[i] + wobble, y);

                Color color = AuraColor;
                color.a = Mathf.Sin(t * 3.1416f) * 0.85f;

                images[i].color = color;
            }
        }

        public void Cleanup()
        {
            if (parts == null) return;

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null) Destroy(parts[i].gameObject);
            }

            parts = null;
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private static void EnsureDot()
        {
            if (dot != null) return;

            const int sizeValue = 16;

            Texture2D texture = new Texture2D(sizeValue, sizeValue, TextureFormat.RGBA32, false);

            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2((sizeValue - 1) * 0.5f, (sizeValue - 1) * 0.5f);
            float radius = sizeValue * 0.5f;

            for (int y = 0; y < sizeValue; y++)
            {
                for (int x = 0; x < sizeValue; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Clamp01(1f - distance);

                    alpha *= alpha;

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            dot = Sprite.Create(
                texture,
                new Rect(0f, 0f, sizeValue, sizeValue),
                new Vector2(0.5f, 0.5f));

            dot.hideFlags = HideFlags.HideAndDontSave;
        }
    }
}

