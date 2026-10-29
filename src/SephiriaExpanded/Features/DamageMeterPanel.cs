using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SephiriaExpanded
{
    internal sealed class DamageMeterPanel
    {
        internal sealed class MeterRow
        {
            internal string Name;
            internal Color Color;
            internal Sprite Icon;
            internal string Number;
            internal string Value;
            internal string Percent;
            internal double Raw;
            internal float Fill;
        }

        private sealed class RowView
        {
            internal Image Bar;
            internal TMP_Text Rank;
            internal Image Icon;
            internal TMP_Text Name;
            internal TMP_Text Value;
            internal TMP_Text Percent;

            internal void SetActive(bool active)
            {
                Bar.gameObject.SetActive(active);
                Rank.gameObject.SetActive(active);
                Icon.gameObject.SetActive(active);
                Name.gameObject.SetActive(active);
                Value.gameObject.SetActive(active);
                Percent.gameObject.SetActive(active);
            }
        }

        private sealed class ButtonView
        {
            internal Image Background;
            internal TMP_Text Label;
            internal bool Enabled = true;
            internal bool Selected;

            internal void SetPosition(Vector2 position)
            {
                Background.rectTransform.anchoredPosition = position;
                Label.rectTransform.anchoredPosition = position;
            }

            internal void SetActive(bool active)
            {
                Background.gameObject.SetActive(active);
                Label.gameObject.SetActive(active);
                Enabled = active;
            }

            internal bool Contains(Vector2 pointer)
            {
                return Enabled && RectTransformUtility.RectangleContainsScreenPoint(Background.rectTransform, pointer, null);
            }

            internal void Refresh(bool hovered)
            {
                Color fill = ButtonColor;
                if (Selected) fill = ButtonSelectedColor;
                else if (Enabled && hovered) fill = ButtonHoverColor;

                Background.color = fill;
                Label.color = !Enabled ? ButtonDisabledText : Selected || hovered ? Color.white : ButtonText;
            }
        }

        internal const int PageCount = 4;
        internal const int RowStep = 4;
        internal const int MaxVisibleRows = 12;
        internal const float MinScale = 0.7f;
        internal const float MaxScale = 1.6f;

        private const string RootName = "SephiriaExpanded_Panel_DamageMeter";
        private const float Padding = 3f;
        private const float GroupLabelHeight = 10f;
        private const float ButtonHeight = 11f;
        private const float ButtonGap = 3f;
        private const float GroupGap = 6f;
        private const float SubtitleHeight = 10f;
        private const float HeaderHeight = Padding + GroupLabelHeight + ButtonHeight + 1f + SubtitleHeight + 1f;
        private const float FooterGap = 2f;
        private const float RowHeight = 10f;
        private const float IconSize = 8f;
        private const float FontSize = 6f;
        private const float SmallFontSize = 5f;
        private const float RankWidth = 7f;
        private const float NameWidth = 44f;
        private const float ValueWidth = 42f;
        private const float PercentWidth = 28f;
        private const float Width = Padding + RankWidth + IconSize + 2f + NameWidth + ValueWidth + PercentWidth + Padding;
        private const float ToggleSize = 8f;
        private const float SwitchWidth = 12f;
        private const float MiniUnitWidth = 12f;
        private const float MiniUnitGap = 2f;
        private const float MiniWidth = 80f;
        private const float MiniHeight = 14f;
        private const float CornerZone = 9f;
        private const float BorderThickness = 1f;
        private const float NearRatio = 0.06f;
        private const float HoldSeconds = 2f;
        private const float HoldTolerance = 4f;
        private const float FeedbackWidth = 96f;
        private const float FeedbackHeight = 17f;
        private const float WheelStreakWindow = 0.3f;
        private const int MaxWheelStep = 4;
        private const float MinScrollSpeed = 30f;
        private const float ScrollCatchUp = 6f;
        private static readonly AccessTools.FieldRef<UI_ItemTooltip, TextMeshProUGUI> TooltipName = AccessTools.FieldRefAccess<UI_ItemTooltip, TextMeshProUGUI>("nameText");
        private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color ButtonColor = new Color(0.2f, 0.2f, 0.23f, 0.95f);
        private static readonly Color ButtonHoverColor = new Color(0.34f, 0.34f, 0.4f, 0.95f);
        private static readonly Color ButtonSelectedColor = new Color(0.2f, 0.46f, 0.82f, 0.98f);
        private static readonly Color ButtonText = new Color(0.82f, 0.82f, 0.86f, 1f);
        private static readonly Color ButtonDisabledText = new Color(0.82f, 0.82f, 0.86f, 0.3f);
        private static readonly Color GroupLabelColor = new Color(0.7f, 0.74f, 0.82f, 1f);
        private static readonly Color ResizeBorderColor = Color.white;
        private static readonly Color SnapBorderColor = new Color(0.2f, 1f, 0.35f, 1f);
        private static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.28f);
        private static readonly Color OriginColor = new Color(1f, 0.85f, 0.3f, 1f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly string[] PageLabels = { "DPS", "전체", "DPS", "전체" };
        private static readonly int[] PageOfButton = { 0, 2, 1, 3 };
        private static readonly string[] FooterLabels = { "업로드", "초기화", "최소화", "-", "+" };
        private static readonly float[] FooterWeights = { 2.2f, 2.2f, 2.2f, 1f, 1f };

        private readonly List<RowView> rows = new List<RowView>();
        private readonly List<ButtonView> pageButtons = new List<ButtonView>();
        private readonly List<ButtonView> footerButtons = new List<ButtonView>();
        private readonly Vector3[] corners = new Vector3[4];
        private readonly List<Image> borderEdges = new List<Image>();
        private GameObject root;
        private RectTransform rect;
        private RectTransform content;
        private ButtonView toggle;
        private ButtonView modeSwitch;
        private TMP_Text miniText;
        private TMP_Text miniUnit;
        private string miniValue = string.Empty;
        private string miniUnitValue = string.Empty;
        private bool layoutMinimized;
        private Canvas canvas;
        private TMP_Text subtitle;
        private GameObject border;
        private RectTransform feedback;
        private TMP_Text feedbackLabel;
        private TMP_Text feedbackMin;
        private TMP_Text feedbackMax;
        private Image feedbackMarker;
        private Image feedbackOrigin;
        private bool dragging;
        private Vector2 lastPointer;
        private bool resizing;
        private int fixedCorner;
        private Vector2 fixedScreen;
        private Vector2 fixedLocal;
        private float startScale;
        private float startDistance;
        private float holdTimer;
        private Vector2 holdPointer;
        private bool snapped;
        private int scroll;
        private int dataCount;
        private int scrollTarget;
        private float scrollCarry;
        private bool dirty;
        private int wheelStreak;
        private float lastWheel;
        private int layoutRows;
        private TMP_FontAsset itemFont;
        private Material itemMaterial;

        internal Action OnUpload;
        internal Action OnReset;

        internal int Page { get; private set; }

        internal bool IsBuilt
        {
            get { return root != null; }
        }

        private static int VisibleRows
        {
            get { return Mathf.Clamp(ModSettings.DamageMeterRows / RowStep * RowStep, RowStep, MaxVisibleRows); }
        }

        private static bool Minimized
        {
            get { return ModSettings.DamageMeterMinimized; }
        }

        internal static bool MiniTotal
        {
            get { return ModSettings.DamageMeterMiniTotal; }
        }

        private static float Scale
        {
            get { return Mathf.Clamp(ModSettings.DamageMeterScale, MinScale, MaxScale); }
        }

        internal bool TryBuild()
        {
            if (root != null) return true;

            UIRoot hud = UIManager.Instance == null ? null : UIManager.Instance.GetRootFromType(EUIObjectPoolingParent.HUD);
            if (hud == null) return false;

            root = new GameObject(RootName, typeof(RectTransform), typeof(Image));
            rect = root.GetComponent<RectTransform>();
            rect.SetParent(hud.transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = TopLeft;
            rect.anchorMax = TopLeft;
            rect.pivot = TopLeft;
            canvas = hud.Canvas;
            rect.localScale = Vector3.one * Scale;
            layoutRows = VisibleRows;
            layoutMinimized = Minimized;
            rect.sizeDelta = LayoutSize();
            rect.anchoredPosition = Clamp(new Vector2(ModSettings.DamageMeterX, ModSettings.DamageMeterY));

            Image background = root.GetComponent<Image>();
            background.color = BackgroundColor;
            background.raycastTarget = false;

            GameObject holder = new GameObject("Content", typeof(RectTransform));
            content = holder.GetComponent<RectTransform>();
            content.SetParent(rect, false);
            content.anchorMin = TopLeft;
            content.anchorMax = TopLeft;
            content.pivot = TopLeft;
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            if (!BuildHeader() || !BuildRows() || !BuildFooter() || !BuildToggle())
            {
                Destroy();
                return false;
            }

            BuildBorder();
            ApplyLayout();
            return true;
        }

        internal void SetActive(bool active)
        {
            if (root != null && root.activeSelf != active) root.SetActive(active);
        }

        internal void Destroy()
        {
            if (root != null) UnityEngine.Object.Destroy(root);

            root = null;
            rect = null;
            content = null;
            toggle = null;
            modeSwitch = null;
            miniText = null;
            miniUnit = null;
            subtitle = null;
            border = null;
            feedback = null;
            feedbackLabel = null;
            feedbackMin = null;
            feedbackMax = null;
            feedbackMarker = null;
            feedbackOrigin = null;
            rows.Clear();
            pageButtons.Clear();
            footerButtons.Clear();
            borderEdges.Clear();
            dragging = false;
            resizing = false;
        }

        internal void HandleInput(bool blocked)
        {
            Mouse mouse = Mouse.current;
            if (root == null || mouse == null) return;

            AnimateScroll();

            Vector2 pointer = mouse.position.ReadValue();
            if (resizing)
            {
                UpdateResize(mouse, pointer);
                return;
            }

            if (dragging)
            {
                if (!mouse.leftButton.isPressed) EndDrag();
                else Move(pointer - lastPointer);

                lastPointer = pointer;
                return;
            }

            bool inside = RectTransformUtility.RectangleContainsScreenPoint(rect, pointer, null);
            RefreshButtons(blocked || !inside ? new Vector2(-10000f, -10000f) : pointer);

            float wheel = mouse.scroll.ReadValue().y;
            if (!blocked && wheel != 0f && inside && !Minimized)
            {
                wheelStreak = Time.unscaledTime - lastWheel < WheelStreakWindow ? wheelStreak + 1 : 0;
                lastWheel = Time.unscaledTime;
                int step = Mathf.Min(MaxWheelStep, 1 + wheelStreak / 3);
                scrollTarget = Mathf.Clamp(scrollTarget + (wheel > 0f ? -step : step), 0, Mathf.Max(0, dataCount - VisibleRows));
            }

            if (blocked || !inside) return;

            Keyboard keyboard = Keyboard.current;
            bool alt = keyboard != null && keyboard.altKey.isPressed;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (alt)
                {
                    dragging = true;
                    lastPointer = pointer;
                    return;
                }

                ClickButtons(pointer);
                return;
            }

            if (alt && mouse.rightButton.wasPressedThisFrame)
            {
                int corner = CornerAt(pointer);
                if (corner >= 0) BeginResize(corner, pointer);
            }
        }

        internal bool ConsumeDirty()
        {
            bool value = dirty;
            dirty = false;
            return value;
        }

        internal void Render(string pageSubtitle, List<MeterRow> data)
        {
            if (root == null) return;

            if (layoutRows != VisibleRows || layoutMinimized != Minimized) ApplyLayout();
            if (!resizing && Mathf.Abs(rect.localScale.x - Scale) > 0.001f) rect.localScale = Vector3.one * Scale;

            miniText.text = miniValue;
            miniUnit.text = miniUnitValue;
            if (Minimized)
            {
                miniUnit.rectTransform.anchoredPosition = new Vector2(Padding + 1f + miniText.preferredWidth + MiniUnitGap, 0f);
                return;
            }


            subtitle.text = pageSubtitle;

            int visible = VisibleRows;
            dataCount = data.Count;
            int count = Mathf.Min(data.Count, visible);
            int maxScroll = Mathf.Max(0, data.Count - visible);
            scroll = Mathf.Clamp(scroll, 0, maxScroll);
            scrollTarget = Mathf.Clamp(scrollTarget, 0, maxScroll);
            for (int i = 0; i < rows.Count; i++)
            {
                RowView view = rows[i];
                view.SetActive(i < count);
                if (i >= count) continue;

                MeterRow row = data[scroll + i];
                float fullWidth = Width - 2f * Padding;
                view.Bar.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, fullWidth * Mathf.Clamp01(row.Fill)), RowHeight - 1f);
                view.Bar.color = new Color(row.Color.r, row.Color.g, row.Color.b, 0.55f);
                view.Rank.text = (scroll + i + 1).ToString();
                view.Icon.sprite = row.Icon;
                view.Icon.enabled = row.Icon != null;
                view.Name.text = row.Name;
                view.Value.text = row.Value;
                view.Percent.text = row.Percent;
            }
        }

        internal void SetMiniText(string value, string unit)
        {
            miniValue = value;
            miniUnitValue = unit;
        }

        internal bool Contains(Vector2 screenPoint)
        {
            return root != null && root.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, null);
        }

        private static float PanelHeight(int visibleRows)
        {
            return HeaderHeight + visibleRows * RowHeight + FooterGap + ButtonHeight + Padding;
        }

        private float FooterTop(int visibleRows)
        {
            return -(HeaderHeight + visibleRows * RowHeight + FooterGap);
        }

        private static Vector2 LayoutSize()
        {
            return Minimized ? new Vector2(MiniWidth, MiniHeight) : new Vector2(Width, PanelHeight(VisibleRows));
        }

        private void ApplyLayout()
        {
            layoutRows = VisibleRows;
            layoutMinimized = Minimized;
            rect.sizeDelta = LayoutSize();
            content.gameObject.SetActive(!layoutMinimized);
            miniText.gameObject.SetActive(layoutMinimized);
            miniUnit.gameObject.SetActive(layoutMinimized);
            float miniTop = -(MiniHeight - ToggleSize) * 0.5f;
            toggle.SetPosition(layoutMinimized ? new Vector2(MiniWidth - Padding - ToggleSize, miniTop) : new Vector2(Width - Padding - ToggleSize, -Padding));
            modeSwitch.SetActive(layoutMinimized);
            modeSwitch.SetPosition(new Vector2(MiniWidth - Padding - ToggleSize - ButtonGap - SwitchWidth, miniTop));
            for (int i = 0; i < footerButtons.Count; i++)
                footerButtons[i].SetPosition(new Vector2(FooterLeft(i), FooterTop(layoutRows)));

            footerButtons[3].Enabled = layoutRows > RowStep;
            footerButtons[4].Enabled = layoutRows < MaxVisibleRows;
            rect.anchoredPosition = Clamp(rect.anchoredPosition);
            dirty = true;
        }

        private static float FooterWidth(int index)
        {
            float total = 0f;
            foreach (float weight in FooterWeights)
                total += weight;

            return (Width - 2f * Padding - (FooterWeights.Length - 1) * ButtonGap) * FooterWeights[index] / total;
        }

        private static float FooterLeft(int index)
        {
            float left = Padding;
            for (int i = 0; i < index; i++)
                left += FooterWidth(i) + ButtonGap;

            return left;
        }

        private void RefreshButtons(Vector2 pointer)
        {
            toggle.Refresh(toggle.Contains(pointer));
            modeSwitch.Refresh(modeSwitch.Contains(pointer));
            if (Minimized) return;

            for (int i = 0; i < pageButtons.Count; i++)
            {
                pageButtons[i].Selected = PageOfButton[i] == Page;
                pageButtons[i].Refresh(pageButtons[i].Contains(pointer));
            }

            foreach (ButtonView button in footerButtons)
                button.Refresh(button.Contains(pointer));
        }

        private void ClickButtons(Vector2 pointer)
        {
            if (toggle.Contains(pointer))
            {
                ModSettings.DamageMeterMinimized = !Minimized;
                ModSettings.Save();
                ApplyLayout();
                return;
            }

            if (modeSwitch.Contains(pointer))
            {
                ModSettings.DamageMeterMiniTotal = !MiniTotal;
                ModSettings.Save();
                dirty = true;
                return;
            }

            if (Minimized) return;

            for (int i = 0; i < pageButtons.Count; i++)
            {
                if (!pageButtons[i].Contains(pointer)) continue;

                SetPage(PageOfButton[i]);
                return;
            }

            for (int i = 0; i < footerButtons.Count; i++)
            {
                if (!footerButtons[i].Contains(pointer)) continue;

                switch (i)
                {
                    case 0:
                        if (OnUpload != null) OnUpload();
                        break;
                    case 1:
                        if (OnReset != null) OnReset();
                        break;
                    case 2:
                        RestoreDefaults();
                        break;
                    case 3:
                        ChangeRows(-RowStep);
                        break;
                    case 4:
                        ChangeRows(RowStep);
                        break;
                }

                return;
            }
        }

        private void SetPage(int page)
        {
            if (page == Page) return;

            Page = page;
            scroll = 0;
            scrollTarget = 0;
            scrollCarry = 0f;
            dirty = true;
        }

        private void ChangeRows(int delta)
        {
            int rowsNow = Mathf.Clamp(VisibleRows + delta, RowStep, MaxVisibleRows);
            if (rowsNow == VisibleRows) return;

            ModSettings.DamageMeterRows = rowsNow;
            ModSettings.Save();
            ApplyLayout();
        }

        private void RestoreDefaults()
        {
            ModSettings.DamageMeterRows = ModSettings.DamageMeterDefaultRows;
            ModSettings.DamageMeterScale = ModSettings.DamageMeterDefaultScale;
            ModSettings.Save();
            rect.localScale = Vector3.one * Scale;
            ApplyLayout();
        }

        private void AnimateScroll()
        {
            if (scroll == scrollTarget)
            {
                scrollCarry = 0f;
                return;
            }

            int distance = scrollTarget - scroll;
            scrollCarry += Time.unscaledDeltaTime * Mathf.Max(MinScrollSpeed, Mathf.Abs(distance) * ScrollCatchUp);
            int move = Mathf.Min((int)scrollCarry, Mathf.Abs(distance));
            if (move <= 0) return;

            scrollCarry -= move;
            scroll += distance > 0 ? move : -move;
            dirty = true;
        }

        private void EndDrag()
        {
            dragging = false;
            ModSettings.DamageMeterX = rect.anchoredPosition.x;
            ModSettings.DamageMeterY = rect.anchoredPosition.y;
            ModSettings.Save();
        }

        private void Move(Vector2 pixels)
        {
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            rect.anchoredPosition = Clamp(rect.anchoredPosition + pixels / scale);
        }

        private Vector2 Clamp(Vector2 position)
        {
            RectTransform parent = rect.parent as RectTransform;
            if (parent == null) return position;

            Vector2 size = parent.rect.size;
            Vector2 own = rect.sizeDelta * rect.localScale.x;
            position.x = Mathf.Clamp(position.x, 0f, Mathf.Max(0f, size.x - own.x));
            position.y = Mathf.Clamp(position.y, -Mathf.Max(0f, size.y - own.y), 0f);
            return position;
        }

        private int CornerAt(Vector2 pointer)
        {
            rect.GetWorldCorners(corners);
            float pixelsPerUnit = Mathf.Abs(corners[3].x - corners[0].x) / Mathf.Max(1f, rect.sizeDelta.x);
            float zone = CornerZone * pixelsPerUnit;
            for (int i = 0; i < 4; i++)
                if (Mathf.Abs(pointer.x - corners[i].x) <= zone && Mathf.Abs(pointer.y - corners[i].y) <= zone) return i;

            return -1;
        }

        private void BeginResize(int corner, Vector2 pointer)
        {
            RectTransform parent = rect.parent as RectTransform;
            if (parent == null) return;

            rect.GetWorldCorners(corners);
            fixedCorner = (corner + 2) % 4;
            fixedScreen = corners[fixedCorner];
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, fixedScreen, null, out fixedLocal);
            startScale = rect.localScale.x;
            startDistance = Mathf.Max(1f, (pointer - fixedScreen).magnitude);
            holdPointer = pointer;
            holdTimer = 0f;
            snapped = false;
            resizing = true;
            BuildFeedback();
            border.SetActive(true);
            feedback.gameObject.SetActive(true);
            UpdateResizeVisuals(startScale);
        }

        private void UpdateResize(Mouse mouse, Vector2 pointer)
        {
            if (!mouse.rightButton.isPressed)
            {
                EndResize();
                return;
            }

            float wanted = Mathf.Clamp(startScale * Mathf.Max(1f, (pointer - fixedScreen).magnitude) / startDistance, MinScale, MaxScale);
            bool near = Mathf.Abs(wanted - startScale) <= startScale * NearRatio;
            if ((pointer - holdPointer).sqrMagnitude > HoldTolerance * HoldTolerance)
            {
                holdPointer = pointer;
                holdTimer = 0f;
            }
            else
            {
                holdTimer += Time.unscaledDeltaTime;
            }

            if (!near)
            {
                snapped = false;
                holdTimer = 0f;
            }
            else if (!snapped && holdTimer >= HoldSeconds)
            {
                snapped = true;
            }

            UpdateResizeVisuals(snapped ? startScale : wanted);
        }

        private void UpdateResizeVisuals(float scale)
        {
            RectTransform parent = rect.parent as RectTransform;
            if (parent == null) return;

            rect.localScale = Vector3.one * scale;
            Vector2 size = rect.sizeDelta * scale;
            Vector2 topLeft = fixedLocal;
            if (fixedCorner == 2 || fixedCorner == 3) topLeft.x -= size.x;
            if (fixedCorner == 0 || fixedCorner == 3) topLeft.y += size.y;

            Rect parentRect = parent.rect;
            rect.anchoredPosition = Clamp(new Vector2(topLeft.x - parentRect.xMin, topLeft.y - parentRect.yMax));

            Color color = snapped ? SnapBorderColor : ResizeBorderColor;
            foreach (Image edge in borderEdges)
                edge.color = color;

            feedback.localScale = Vector3.one / scale;
            feedback.anchoredPosition = new Vector2(0f, -(rect.sizeDelta.y + 2f));
            feedbackLabel.text = snapped ? "조정 없음" : "크기 " + Mathf.RoundToInt(scale * 100f) + "%";
            feedbackLabel.color = snapped ? SnapBorderColor : Color.white;
            feedbackMin.text = Mathf.RoundToInt(MinScale * 100f) + "%";
            feedbackMax.text = Mathf.RoundToInt(MaxScale * 100f) + "%";

            float trackX = FeedbackWidth * 0.17f;
            float trackWidth = FeedbackWidth - 2f * trackX;
            feedbackMarker.rectTransform.anchoredPosition = new Vector2(trackX + Mathf.InverseLerp(MinScale, MaxScale, scale) * trackWidth - 1f, -8.5f);
            feedbackMarker.color = color;
            feedbackOrigin.rectTransform.anchoredPosition = new Vector2(trackX + Mathf.InverseLerp(MinScale, MaxScale, startScale) * trackWidth - 0.5f, -8.5f);
        }

        private void EndResize()
        {
            resizing = false;
            border.SetActive(false);
            feedback.gameObject.SetActive(false);
            ModSettings.DamageMeterScale = rect.localScale.x;
            ModSettings.DamageMeterX = rect.anchoredPosition.x;
            ModSettings.DamageMeterY = rect.anchoredPosition.y;
            ModSettings.Save();
            dirty = true;
        }

        private bool BuildHeader()
        {
            float groupWidth = (Width - 2f * Padding - GroupGap) * 0.5f;
            float buttonWidth = (groupWidth - ButtonGap) * 0.5f;
            string[] groups = { "개인", "파티" };
            for (int group = 0; group < 2; group++)
            {
                float groupX = Padding + group * (groupWidth + GroupGap);
                TMP_Text label = CreateText(content, "Group" + group, new Vector2(groupX, -Padding), new Vector2(groupWidth, GroupLabelHeight), TextAlignmentOptions.Center, SmallFontSize);
                if (label == null) return false;

                label.text = groups[group];
                label.color = GroupLabelColor;
                for (int b = 0; b < 2; b++)
                {
                    int index = group * 2 + b;
                    ButtonView button = CreateButton(content, "Page" + index, new Vector2(groupX + b * (buttonWidth + ButtonGap), -(Padding + GroupLabelHeight)), new Vector2(buttonWidth, ButtonHeight), PageLabels[index]);
                    if (button == null) return false;

                    pageButtons.Add(button);
                }
            }

            subtitle = CreateText(content, "Subtitle", new Vector2(Padding, -(Padding + GroupLabelHeight + ButtonHeight + 1f)), new Vector2(Width - 2f * Padding, SubtitleHeight), TextAlignmentOptions.Center, FontSize);
            return subtitle != null;
        }

        private bool BuildRows()
        {
            for (int i = 0; i < MaxVisibleRows; i++)
            {
                RowView row = CreateRow(i);
                if (row == null) return false;

                rows.Add(row);
            }

            return true;
        }

        private bool BuildFooter()
        {
            for (int i = 0; i < FooterLabels.Length; i++)
            {
                ButtonView button = CreateButton(content, "Footer" + i, new Vector2(FooterLeft(i), FooterTop(layoutRows)), new Vector2(FooterWidth(i), ButtonHeight), FooterLabels[i]);
                if (button == null) return false;

                footerButtons.Add(button);
            }

            return true;
        }

        private void BuildBorder()
        {
            border = new GameObject("Border", typeof(RectTransform));
            RectTransform borderRect = border.GetComponent<RectTransform>();
            borderRect.SetParent(rect, false);
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            CreateEdge(borderRect, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, BorderThickness));
            CreateEdge(borderRect, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, BorderThickness));
            CreateEdge(borderRect, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(BorderThickness, 0f));
            CreateEdge(borderRect, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(BorderThickness, 0f));
            border.SetActive(false);
        }

        private void CreateEdge(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
        {
            GameObject edge = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform edgeRect = edge.GetComponent<RectTransform>();
            edgeRect.SetParent(parent, false);
            edgeRect.anchorMin = anchorMin;
            edgeRect.anchorMax = anchorMax;
            edgeRect.pivot = pivot;
            edgeRect.anchoredPosition = Vector2.zero;
            edgeRect.sizeDelta = size;

            Image image = edge.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = ResizeBorderColor;
            borderEdges.Add(image);
        }

        private void BuildFeedback()
        {
            if (feedback != null) return;

            GameObject holder = new GameObject("Feedback", typeof(RectTransform), typeof(Image));
            feedback = holder.GetComponent<RectTransform>();
            feedback.SetParent(rect, false);
            feedback.anchorMin = TopLeft;
            feedback.anchorMax = TopLeft;
            feedback.pivot = TopLeft;
            feedback.sizeDelta = new Vector2(FeedbackWidth, FeedbackHeight);

            Image background = holder.GetComponent<Image>();
            background.color = BackgroundColor;
            background.raycastTarget = false;

            float trackX = FeedbackWidth * 0.17f;
            float trackWidth = FeedbackWidth - 2f * trackX;
            feedbackLabel = CreateText(feedback, "Label", new Vector2(0f, -1f), new Vector2(FeedbackWidth, 7f), TextAlignmentOptions.Center, SmallFontSize);
            feedbackMin = CreateText(feedback, "Min", new Vector2(0f, -8f), new Vector2(trackX, 7f), TextAlignmentOptions.Center, SmallFontSize);
            feedbackMax = CreateText(feedback, "Max", new Vector2(FeedbackWidth - trackX, -8f), new Vector2(trackX, 7f), TextAlignmentOptions.Center, SmallFontSize);
            CreateImage(feedback, "Track", new Vector2(trackX, -10.5f), new Vector2(trackWidth, 2f)).color = TrackColor;
            feedbackOrigin = CreateImage(feedback, "Origin", new Vector2(trackX, -8.5f), new Vector2(1f, 6f));
            feedbackOrigin.color = OriginColor;
            feedbackMarker = CreateImage(feedback, "Marker", new Vector2(trackX, -8.5f), new Vector2(2f, 6f));
            feedback.gameObject.SetActive(false);
        }

        private RowView CreateRow(int index)
        {
            float top = -(HeaderHeight + index * RowHeight);

            Image bar = CreateImage(content, "Bar" + index, new Vector2(Padding, top), new Vector2(1f, RowHeight - 1f));
            TMP_Text rank = CreateText(content, "Rank" + index, new Vector2(Padding, top), new Vector2(RankWidth, RowHeight), TextAlignmentOptions.Midline, FontSize);
            Image icon = CreateImage(content, "Icon" + index, new Vector2(Padding + RankWidth + IconSize * 0.5f, top - RowHeight * 0.5f), new Vector2(IconSize, IconSize));
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icon.preserveAspect = true;
            float nameX = Padding + RankWidth + IconSize + 2f;
            float valueX = nameX + NameWidth;
            float percentX = valueX + ValueWidth;
            TMP_Text name = CreateText(content, "Name" + index, new Vector2(nameX, top), new Vector2(NameWidth, RowHeight), TextAlignmentOptions.MidlineLeft, FontSize);
            TMP_Text value = CreateText(content, "Value" + index, new Vector2(valueX, top), new Vector2(ValueWidth, RowHeight), TextAlignmentOptions.MidlineRight, FontSize);
            TMP_Text percent = CreateText(content, "Percent" + index, new Vector2(percentX, top), new Vector2(PercentWidth, RowHeight), TextAlignmentOptions.MidlineRight, FontSize);
            if (rank == null || name == null || value == null || percent == null) return null;

            name.overflowMode = TextOverflowModes.Ellipsis;
            RowView view = new RowView { Bar = bar, Rank = rank, Icon = icon, Name = name, Value = value, Percent = percent };
            view.SetActive(false);
            return view;
        }

        private bool BuildToggle()
        {
            toggle = CreateButton(rect, "Toggle", Vector2.zero, new Vector2(ToggleSize, ToggleSize), "&");
            modeSwitch = CreateButton(rect, "Switch", Vector2.zero, new Vector2(SwitchWidth, ToggleSize), "/");
            float areaX = Padding + 1f;
            float areaWidth = MiniWidth - 2f * Padding - ToggleSize - SwitchWidth - 2f * ButtonGap - 1f;
            miniText = CreateText(rect, "Mini", new Vector2(areaX, 0f), new Vector2(areaWidth - MiniUnitWidth, MiniHeight), TextAlignmentOptions.MidlineLeft, FontSize);
            miniUnit = CreateText(rect, "MiniUnit", new Vector2(areaX, 0f), new Vector2(MiniUnitWidth, MiniHeight), TextAlignmentOptions.MidlineLeft, FontSize);
            return toggle != null && modeSwitch != null && miniText != null && miniUnit != null;
        }

        private ButtonView CreateButton(RectTransform parent, string name, Vector2 position, Vector2 size, string text)
        {
            Image background = CreateImage(parent, name, position, size);
            TMP_Text label = CreateText(parent, name + "Label", position, size, TextAlignmentOptions.Center, FontSize);
            if (label == null) return null;

            label.text = text;
            ButtonView button = new ButtonView { Background = background, Label = label };
            button.Refresh(false);
            return button;
        }

        private void ApplyItemFont(TMP_Text text)
        {
            if (itemFont == null)
            {
                UI_ItemTooltip tooltip = UIManager.Instance == null ? null : UIManager.Instance.GetElement<UI_ItemTooltip>();
                TextMeshProUGUI source = tooltip == null ? null : TooltipName(tooltip);
                if (source == null) return;

                itemFont = source.font;
                itemMaterial = source.fontSharedMaterial;
            }

            text.font = itemFont;
            text.fontSharedMaterial = itemMaterial;
        }

        private Image CreateImage(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject image = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform imageRect = image.GetComponent<RectTransform>();
            imageRect.SetParent(parent, false);
            imageRect.anchorMin = TopLeft;
            imageRect.anchorMax = TopLeft;
            imageRect.pivot = TopLeft;
            imageRect.anchoredPosition = position;
            imageRect.sizeDelta = size;

            Image component = image.GetComponent<Image>();
            component.raycastTarget = false;
            return component;
        }

        private TMP_Text CreateText(RectTransform parent, string name, Vector2 position, Vector2 size, TextAlignmentOptions alignment, float fontSize)
        {
            TMP_Text text = HudTextFactory.Create("SephiriaExpanded_Text_DamageMeter_" + name, TopLeft, position, size, fontSize, alignment);
            if (text == null) return null;

            text.transform.SetParent(parent, false);
            text.rectTransform.anchoredPosition = position;
            ApplyItemFont(text);
            return text;
        }
    }
}
