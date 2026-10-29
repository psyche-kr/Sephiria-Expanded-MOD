using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SephiriaExpanded
{
    internal static class ExpandedOptionRows
    {
        private static readonly List<GameObject> CreatedRows = new List<GameObject>();
        private static readonly Dictionary<GameObject, int> SectionOrders = new Dictionary<GameObject, int>();
        private static readonly Dictionary<GameObject, string> RowSections = new Dictionary<GameObject, string>();
        private static readonly Dictionary<GameObject, SliderBinding> SliderBindings = new Dictionary<GameObject, SliderBinding>();
        private static readonly List<Action> ValueRefreshers = new List<Action>();
        private static readonly List<Action> TitleRefreshers = new List<Action>();
        private const string SubItemPrefix = "   ";

        private static int currentSectionOrder;
        private static string currentSection;

        internal sealed class SliderBinding
        {
            internal Slider Slider;
            internal TextMeshProUGUI Text;
            internal Func<int> Getter;
            internal Func<int, string> Format;
            internal Action<int> Apply;
        }

        internal sealed class RowScope
        {
            internal readonly List<GameObject> Rows = new List<GameObject>();
            internal readonly Dictionary<GameObject, int> Orders = new Dictionary<GameObject, int>();
            internal readonly Dictionary<GameObject, string> Sections = new Dictionary<GameObject, string>();
            internal readonly Dictionary<GameObject, SliderBinding> Sliders = new Dictionary<GameObject, SliderBinding>();
            internal readonly List<Action> Refreshers = new List<Action>();
            internal readonly List<Action> TitleRefreshers = new List<Action>();
            internal int SectionOrder;
            internal string Section;
        }

        internal static RowScope Detach()
        {
            RowScope scope = new RowScope();
            scope.Rows.AddRange(CreatedRows);
            foreach (KeyValuePair<GameObject, int> pair in SectionOrders) scope.Orders[pair.Key] = pair.Value;
            foreach (KeyValuePair<GameObject, string> pair in RowSections) scope.Sections[pair.Key] = pair.Value;
            foreach (KeyValuePair<GameObject, SliderBinding> pair in SliderBindings) scope.Sliders[pair.Key] = pair.Value;
            scope.Refreshers.AddRange(ValueRefreshers);
            scope.TitleRefreshers.AddRange(TitleRefreshers);
            scope.SectionOrder = currentSectionOrder;
            scope.Section = currentSection;

            CreatedRows.Clear();
            SectionOrders.Clear();
            RowSections.Clear();
            SliderBindings.Clear();
            ValueRefreshers.Clear();
            TitleRefreshers.Clear();
            currentSectionOrder = 0;
            currentSection = null;
            return scope;
        }

        internal static void Attach(RowScope scope)
        {
            CreatedRows.Clear();
            SectionOrders.Clear();
            RowSections.Clear();
            SliderBindings.Clear();
            ValueRefreshers.Clear();
            TitleRefreshers.Clear();
            if (scope == null) return;

            CreatedRows.AddRange(scope.Rows);
            TitleRefreshers.AddRange(scope.TitleRefreshers);
            foreach (KeyValuePair<GameObject, int> pair in scope.Orders) SectionOrders[pair.Key] = pair.Value;
            foreach (KeyValuePair<GameObject, string> pair in scope.Sections) RowSections[pair.Key] = pair.Value;
            foreach (KeyValuePair<GameObject, SliderBinding> pair in scope.Sliders) SliderBindings[pair.Key] = pair.Value;
            ValueRefreshers.AddRange(scope.Refreshers);
            currentSectionOrder = scope.SectionOrder;
            currentSection = scope.Section;
        }

        internal static void RefreshValues(RowScope scope)
        {
            if (scope == null) return;

            foreach (Action refresh in scope.Refreshers)
            {
                try
                {
                    refresh();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        internal static void Destroy(RowScope scope)
        {
            if (scope == null) return;

            foreach (GameObject row in scope.Rows)
            {
                if (row == null) continue;

                row.SetActive(false);
                UnityEngine.Object.Destroy(row);
            }

            scope.Rows.Clear();
            scope.Orders.Clear();
            scope.Sections.Clear();
            scope.Sliders.Clear();
            scope.Refreshers.Clear();
            scope.TitleRefreshers.Clear();
        }

        private const int SubItemFirstIndent = 4;
        private const int SubItemStepIndent = 4;
        private const string Bullet = "▪";
        private const string BulletActiveColor = "#00FF00";
        private const string BulletInactiveColor = "#FF0000";
        private const string BulletNeutralColor = "#FFFFFF";
        private const string BulletLockedColor = "#FF0000";

        private sealed class RowTitle : MonoBehaviour
        {
            internal TMP_Text Text;
            internal string Title;
            private bool active;
            private bool locked;
            private string color;

            internal void Set(bool value)
            {
                active = value;
                Apply();
            }

            internal void SetLocked(bool value)
            {
                locked = value;
                Apply();
            }

            private void Apply()
            {
                string next = locked ? BulletLockedColor : active ? BulletActiveColor : BulletInactiveColor;
                if (Text == null || color == next) return;

                color = next;
                Text.text = FormatTitle(Title, next);
            }
        }

        private sealed class TooltipHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
        {
            internal UI_CommonTooltipOpener Opener;

            public void OnPointerEnter(PointerEventData eventData)
            {
                Show(eventData);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                Hide(eventData);
            }

            public void OnSelect(BaseEventData eventData)
            {
                Show(eventData);
            }

            public void OnDeselect(BaseEventData eventData)
            {
                Hide(eventData);
            }

            private void OnDisable()
            {
                if (Opener != null) Opener.Showing = false;
            }

            private void Show(BaseEventData eventData)
            {
                if (Opener == null || UIManager.Instance == null || UIManager.Instance.GetElement<UI_CommonTooltip>() == null) return;

                Opener.OnSelect(eventData);
            }

            private void Hide(BaseEventData eventData)
            {
                if (Opener != null) Opener.OnDeselect(eventData);
            }
        }

        internal static void Clear()
        {
            for (int i = CreatedRows.Count - 1; i >= 0; i--)
            {
                GameObject row = CreatedRows[i];

                if (row == null) continue;

                row.SetActive(false);
                UnityEngine.Object.Destroy(row);
            }

            CreatedRows.Clear();
            SectionOrders.Clear();
            RowSections.Clear();
            SliderBindings.Clear();
            ValueRefreshers.Clear();
            TitleRefreshers.Clear();
            currentSectionOrder = 0;
            currentSection = null;
        }

        internal static void RefreshTitles()
        {
            foreach (Action refresh in TitleRefreshers)
            {
                try
                {
                    refresh();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        internal static List<GameObject> SnapshotRows()
        {
            return CreatedRows.Where(row => row != null).ToList();
        }

        internal static string GetSection(GameObject row)
        {
            string section;
            return row != null && RowSections.TryGetValue(row, out section) ? section : null;
        }

        internal static void RemoveRow(GameObject row)
        {
            if (row == null) return;

            CreatedRows.Remove(row);
            SectionOrders.Remove(row);
            RowSections.Remove(row);
            SliderBindings.Remove(row);

            row.SetActive(false);
            UnityEngine.Object.Destroy(row);
        }

        internal static void SetSection(string title)
        {
            currentSection = title;
            currentSectionOrder = GetSectionOrder(title);
        }

        internal static void ApplySectionOrder()
        {
            var ordered = CreatedRows.OrderBy(row => SectionOrders[row]).ToList();
            for (int i = 0; i < ordered.Count; i++)
                if (ordered[i] != null)
                    ordered[i].transform.SetSiblingIndex(i);
        }

        internal static void MoveRowsAfter(GameObject anchor, params GameObject[] rows)
        {
            if (anchor == null || rows == null) return;
            int index = CreatedRows.IndexOf(anchor);
            if (index < 0) return;
            foreach (GameObject row in rows)
            {
                if (row == null) continue;
                CreatedRows.Remove(row);
                CreatedRows.Insert(++index, row);
            }
        }

        internal static void MoveRowsAfter(string anchorKey, params GameObject[] rows)
        {
            GameObject anchor = CreatedRows.FirstOrDefault(row => row != null && row.name.Contains(anchorKey));
            MoveRowsAfter(anchor, rows);
        }

        internal static int GetSectionOrder(string title)
        {
            switch (title)
            {
                case "일반": return 0;
                case "전투": return 1;
                case "꾸미기": return 2;
                case "패턴 강화": return 3;
                case "생성 배율": return 4;
                case "체력 배율": return 5;
                case "데미지 배율": return 6;
                case "속도 배율": return 7;
                default: return 100;
            }
        }

        private static void Track(GameObject row, string title = null)
        {
            CreatedRows.Add(row);
            SectionOrders[row] = currentSectionOrder;
            RowSections[row] = currentSection;
        }

        private static void ApplyTooltip(GameObject row, string name)
        {
            string title;
            string description;
            if (row == null || !OptionTooltips.TryGet(name, out title, out description)) return;

            UI_CommonTooltipOpener opener = row.GetComponentInChildren<UI_CommonTooltipOpener>(true);
            if (opener == null) opener = row.AddComponent<UI_CommonTooltipOpener>();

            opener.tooltipName = new LocalizedString(title);
            opener.tooltipContext = new LocalizedString(description);
            opener.UpdateTooltipData();

            TooltipHover hover = row.GetComponent<TooltipHover>();
            if (hover == null) hover = row.AddComponent<TooltipHover>();
            hover.Opener = opener;
        }

        internal static GameObject AddHeader(GameObject template, Transform parent, string title)
        {
            if (template == null || parent == null) return null;

            SetSection(title);

            GameObject clone = UnityEngine.Object.Instantiate(template, parent, false);
            clone.name = "SephiriaExpanded_Header_" + title;

            UI_LocalizationStringText localization =
                clone.GetComponentInChildren<UI_LocalizationStringText>(true);

            if (localization != null && localization.text != null)
            {
                localization.enabled = false;
                localization.text.text = title;
            }
            else
            {
                TextMeshProUGUI text =
                    clone.GetComponentInChildren<TextMeshProUGUI>(true);

                if (text != null)
                    text.text = title;
            }

            clone.SetActive(true);
            Track(clone);

            return clone;
        }

        internal static GameObject AddBlank(GameObject template, Transform parent)
        {
            if (template == null || parent == null) return null;

            GameObject clone = UnityEngine.Object.Instantiate(template, parent, false);
            clone.name = "SephiriaExpanded_Blank_" + CreatedRows.Count;
            clone.SetActive(true);
            Track(clone);
            return clone;
        }

        internal static GameObject AddFooterHeader(
            GameObject template,
            Transform parent,
            string name,
            string title,
            Color color)
        {
            if (template == null || parent == null)
                return null;

            GameObject clone = UnityEngine.Object.Instantiate(template, parent, false);
            clone.name = "SephiriaExpanded_FooterHeader_" + name;
            SetTitleLiteral(clone, null, title, false, BulletNeutralColor);

            foreach (TextMeshProUGUI text in clone.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text == null)
                    continue;

                text.text = title;
                text.color = color;
            }

            clone.SetActive(true);
            Track(clone);
            return clone;
        }

        internal static GameObject AddBool(
            UI_MuteBackgroundAudioToggle template,
            Transform parent,
            string name,
            string title,
            Func<bool> getter,
            Action<bool> setter,
            Action changed = null)
        {
            if (template == null ||
               parent == null ||
               getter == null ||
               setter == null)
            {
                return null;
            }

            GameObject clone =
                UnityEngine.Object.Instantiate(
                    template.gameObject,
                    parent,
                    false
                );

            clone.name = "SephiriaExpanded_Bool_" + name;

            UI_MuteBackgroundAudioToggle original =
                clone.GetComponent<UI_MuteBackgroundAudioToggle>();

            Toggle toggle =
                original != null
                    ? original.toggle
                    : clone.GetComponentInChildren<Toggle>(true);

            if (original != null)
            {
                original.enabled = false;
                UnityEngine.Object.Destroy(original);
            }

            if (toggle == null)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            RowTitle rowTitle = BindTitle(clone, SetTitleLiteral(clone, null, title), title);

            toggle.onValueChanged = new Toggle.ToggleEvent();

            bool current = false;

            try
            {
                current = getter();
            }
            catch
            {
            }

            toggle.SetIsOnWithoutNotify(current);
            SetTitleState(rowTitle, current);

            toggle.onValueChanged.AddListener(value =>
            {
                try
                {
                    setter(value);
                    SetTitleState(rowTitle, value);
                    RefreshTitles();
                    ModSettings.Save();
                    changed?.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);

                    try
                    {
                        toggle.SetIsOnWithoutNotify(getter());
                        SetTitleState(rowTitle, toggle.isOn);
                    }
                    catch
                    {
                    }
                }
            });

            ValueRefreshers.Add(() =>
            {
                toggle.SetIsOnWithoutNotify(getter());
                SetTitleState(rowTitle, toggle.isOn);
            });

            clone.SetActive(true);
            Track(clone, title);
            ApplyTooltip(clone, name);

            return clone;
        }

        internal static GameObject AddActionButton(
            UI_MuteBackgroundAudioToggle template,
            Transform parent,
            string name,
            string title,
            UnityEngine.Events.UnityAction action)
        {
            if (template == null || parent == null || action == null) return null;
            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
            clone.name = "SephiriaExpanded_Button_" + name;
            UI_MuteBackgroundAudioToggle toggle = clone.GetComponent<UI_MuteBackgroundAudioToggle>();
            if (toggle == null || toggle.toggle == null)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }
            toggle.toggle.SetIsOnWithoutNotify(false);
            toggle.toggle.onValueChanged.RemoveAllListeners();
            toggle.toggle.onValueChanged.AddListener(value => action());
            SetTitleLiteral(clone, null, title);
            clone.SetActive(true);
            Track(clone);
            return clone;
        }

        internal static GameObject AddHorayButton(
            UI_HorayButton template,
            Transform parent,
            string name,
            string title,
            Action action)
        {
            if (template == null || parent == null) return null;

            GameObject source = template.gameObject;
            Transform templateRoot = template.transform.parent;
            if (templateRoot != null && templateRoot.Find("Name") != null)
                source = templateRoot.gameObject;

            GameObject clone = UnityEngine.Object.Instantiate(source, parent, false);
            clone.name = "SephiriaExpanded_Button_" + name;

            Transform resetButton = clone.transform.Find("ResetButton");
            if (resetButton == null)
            {
                resetButton = clone.transform.Find("Main Key Image");
                if (resetButton != null)
                    resetButton.name = "ResetButton";
            }

            Transform nameTransform = clone.transform.Find("Name");
            if (nameTransform == null)
            {
                GameObject nameObject = new GameObject("Name", typeof(RectTransform));
                nameTransform = nameObject.transform;
                nameTransform.SetParent(clone.transform, false);
            }

            TextMeshProUGUI nameText = nameTransform.GetComponentInChildren<TextMeshProUGUI>(true);
            if (nameText == null)
                nameText = nameTransform.gameObject.AddComponent<TextMeshProUGUI>();

            TextMeshProUGUI sourceText = nameTransform.GetComponentInChildren<TextMeshProUGUI>(true);
            if (sourceText != null)
            {
                nameText.font = sourceText.font;
                nameText.fontSize = sourceText.fontSize;
                nameText.alignment = sourceText.alignment;
            }

            string display = FormatTitle(title, BulletNeutralColor);
            nameText.color = Color.white;
            nameText.text = display;
            SetTitleLiteral(nameTransform.gameObject, null, title, false, BulletNeutralColor);
            foreach (TextMeshProUGUI text in nameTransform.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text == null) continue;
                text.color = Color.white;
                text.text = display;
            }
            nameTransform.SetSiblingIndex(0);
            if (resetButton != null)
                resetButton.SetSiblingIndex(1);

            GameObject buttonObject = resetButton != null ? resetButton.gameObject : clone;
            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
                button = buttonObject.AddComponent<Button>();

            button.onClick = new Button.ButtonClickedEvent();
            if (action != null)
                button.onClick.AddListener(new UnityAction(action));

            foreach (MonoBehaviour component in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) continue;

                string typeName = component.GetType().Name;
                if (typeName.IndexOf("Rebind", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("Binding", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    component.enabled = false;
                    UnityEngine.Object.Destroy(component);
                }
            }

            for (int i = clone.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = clone.transform.GetChild(i);
                if (child != nameTransform && child != resetButton)
                    child.gameObject.SetActive(false);
            }

            clone.SetActive(true);
            Track(clone, title);
            ApplyTooltip(clone, name);
            return clone;
        }

        internal static GameObject AddRebind(
            GameObject template,
            Transform parent,
            string name,
            string title,
            InputAction action,
            UnityAction<RebindActionUI, InputActionRebindingExtensions.RebindingOperation> stopped = null,
            Func<bool> activeWhen = null,
            Graphic background = null)
        {
            if (template == null || parent == null || action == null) return null;

            GameObject clone = UnityEngine.Object.Instantiate(template, parent, false);
            clone.name = "SephiriaExpanded_Rebind_" + name;
            ApplyRowBackground(clone, background);

            RebindActionUI rebind = clone.GetComponentInChildren<RebindActionUI>(true);
            if (rebind == null)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            InputActionReference reference = InputActionReference.Create(action);
            rebind.actionReference = reference;
            rebind.bindingId = action.bindings[0].id.ToString();
            if (stopped != null) rebind.stopRebindEvent.AddListener(stopped);

            UI_ActionRemap_Keyboard remap = clone.GetComponentInChildren<UI_ActionRemap_Keyboard>(true);
            if (remap != null)
            {
                if (rebind.bindingImage == null) rebind.bindingImage = remap.glyphImage;
                remap.enabled = false;
                UnityEngine.Object.Destroy(remap);
            }

            if (rebind.bindingImage != null) rebind.bindingImage.enabled = true;

            Transform nameTransform = clone.transform.Find("Name");
            RowTitle rowTitle = BindTitle(clone, SetTitleLiteral(nameTransform != null ? nameTransform.gameObject : clone, null, title), title);
            RenameButtons(clone, name);

            if (activeWhen != null)
            {
                SetTitleState(rowTitle, activeWhen());
                TitleRefreshers.Add(() => SetTitleState(rowTitle, activeWhen()));
                rebind.stopRebindEvent.AddListener((ui, operation) => SetTitleState(rowTitle, activeWhen()));
            }

            ValueRefreshers.Add(rebind.UpdateBindingDisplay);

            clone.SetActive(true);
            Track(clone, title);
            ApplyTooltip(clone, name);
            return clone;
        }

        private static void ApplyRowBackground(GameObject row, Graphic background)
        {
            Image source = background as Image;
            if (source == null) return;

            Image target = row.GetComponent<Image>();
            if (target == null) target = row.AddComponent<Image>();

            target.sprite = source.sprite;
            target.type = source.type;
            target.color = source.color;
            target.material = source.material;
            target.raycastTarget = source.raycastTarget;
        }

        private static void RenameButtons(GameObject clone, string name)
        {
            foreach (Button button in clone.GetComponentsInChildren<Button>(true))
            {
                if (button.gameObject == clone) continue;

                bool reset = button.name.IndexOf("Reset", StringComparison.OrdinalIgnoreCase) >= 0;
                button.gameObject.name = "SephiriaExpanded_Button_" + name + (reset ? "_Reset" : "_Rebind");
            }
        }

        internal static GameObject AddChoice(
            UI_OptionBox_Common_Integer template,
            Transform parent,
            string name,
            string title,
            string[] values,
            Func<int> getter,
            Action<int> setter,
            Action changed = null,
            Color?[] valueColors = null,
            Func<bool> activeWhen = null)
        {
            if (template == null ||
               parent == null ||
               values == null ||
               values.Length == 0 ||
               getter == null ||
               setter == null)
            {
                return null;
            }

            GameObject clone =
                UnityEngine.Object.Instantiate(
                    template.gameObject,
                    parent,
                    false
                );

            clone.name = "SephiriaExpanded_Choice_" + name;

            UI_OptionBox_Common_Integer original =
                clone.GetComponent<UI_OptionBox_Common_Integer>();

            if (original == null ||
               original.box == null ||
               original.valueText == null ||
               original.valueText.text == null)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            UI_HorizontalSelectionBox box = original.box;
            UI_LocalizationStringText localization = original.valueText;
            TextMeshProUGUI valueText = localization.text;

            RowTitle rowTitle = BindTitle(clone, SetTitleLiteral(clone, localization, title), title);

            original.enabled = false;
            localization.enabled = false;

            box.numberOfElements = values.Length;

            int index = 0;

            try
            {
                index = Mathf.Clamp(getter(), 0, values.Length - 1);
            }
            catch
            {
            }

            int shown = index;

            bool IsActive()
            {
                return activeWhen != null ? activeWhen() : shown != 0;
            }

            SetTitleState(rowTitle, IsActive());
            if (activeWhen != null) TitleRefreshers.Add(() => SetTitleState(rowTitle, IsActive()));

            string DisplayValue(int idx)
            {
                Color? color = valueColors != null && idx >= 0 && idx < valueColors.Length ? valueColors[idx] : null;
                return color.HasValue ? "<color=#" + ColorUtility.ToHtmlStringRGB(color.Value) + ">" + values[idx] + "</color>" : values[idx];
            }

            box.ChangeValueWithoutNotify(index);
            valueText.text = DisplayValue(index);

            box.OnValueChanged += value =>
            {
                value = Mathf.Clamp(value, 0, values.Length - 1);

                try
                {
                    setter(value);
                    valueText.text = DisplayValue(value);
                    shown = value;
                    SetTitleState(rowTitle, IsActive());
                    RefreshTitles();

                    ModSettings.Save();
                    changed?.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);

                    int fallback = 0;

                    try
                    {
                        fallback = Mathf.Clamp(
                            getter(),
                            0,
                            values.Length - 1
                        );
                    }
                    catch
                    {
                    }

                    box.ChangeValueWithoutNotify(fallback);
                    valueText.text = DisplayValue(fallback);
                    shown = fallback;
                    SetTitleState(rowTitle, IsActive());
                }
            };

            ValueRefreshers.Add(() =>
            {
                int current = Mathf.Clamp(getter(), 0, values.Length - 1);
                box.ChangeValueWithoutNotify(current);
                valueText.text = DisplayValue(current);
                shown = current;
                SetTitleState(rowTitle, IsActive());
            });

            UnityEngine.Object.Destroy(original);

            clone.SetActive(true);
            Track(clone, title);
            ApplyTooltip(clone, name);

            return clone;
        }

        internal static GameObject AddSlider(
            UI_VolumeSlider template,
            Transform parent,
            string name,
            string title,
            Func<int> getter,
            Action<int> setter,
            int min,
            int max,
            Func<int, string> format = null,
            int? defaultValue = null,
            bool alwaysActive = false,
            Func<int> dynamicMin = null)
        {
            if (template == null ||
               parent == null ||
               getter == null ||
               setter == null ||
               max < min)
            {
                return null;
            }

            GameObject clone =
                UnityEngine.Object.Instantiate(
                    template.gameObject,
                    parent,
                    false
                );

            clone.name = "SephiriaExpanded_Slider_" + name;

            UI_VolumeSlider original =
                clone.GetComponent<UI_VolumeSlider>();

            Slider slider =
                original != null
                    ? original.slider
                    : clone.GetComponentInChildren<Slider>(true);

            TextMeshProUGUI valueText =
                original != null
                    ? original.valueText
                    : null;

            if (slider == null || valueText == null)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            if (original != null)
            {
                original.enabled = false;
                UnityEngine.Object.Destroy(original);
            }

            RowTitle rowTitle = BindTitle(clone, SetTitleLiteral(clone, null, title), title);

            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            valueText.overflowMode = TextOverflowModes.Overflow;

            slider.onValueChanged = new Slider.SliderEvent();

            slider.wholeNumbers = true;
            slider.minValue = min;
            slider.maxValue = max;

            int Lower()
            {
                return dynamicMin == null ? min : Mathf.Clamp(dynamicMin(), min, max);
            }

            int current = min;

            try
            {
                current = Mathf.Clamp(getter(), Lower(), max);
            }
            catch
            {
            }

            int baseline = defaultValue ?? current;

            void UpdateColor(int value)
            {
                valueText.color = value == baseline ? Color.white : Color.green;
                SetTitleState(rowTitle, alwaysActive || (defaultValue.HasValue ? value != defaultValue.Value : value > min));
            }

            slider.SetValueWithoutNotify(current);
            valueText.text = format != null ? format(current) : current.ToString();
            UpdateColor(current);

            slider.onValueChanged.AddListener(raw =>
            {
                int value =
                    Mathf.Clamp(
                        Mathf.RoundToInt(raw),
                        Lower(),
                        max
                    );
                if (value != Mathf.RoundToInt(raw)) slider.SetValueWithoutNotify(value);

                try
                {
                    setter(value);
                    valueText.text = format != null ? format(value) : value.ToString();
                    UpdateColor(value);
                    RefreshTitles();
                    ModSettings.Save();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);

                    int fallback = min;

                    try
                    {
                        fallback = Mathf.Clamp(
                            getter(),
                            min,
                            max
                        );
                    }
                    catch
                    {
                    }

                    slider.SetValueWithoutNotify(fallback);
                    valueText.text = format != null ? format(fallback) : fallback.ToString();
                    UpdateColor(fallback);
                }
            });

            SliderBindings[clone] = new SliderBinding
            {
                Slider = slider,
                Text = valueText,
                Getter = getter,
                Format = format,
                Apply = value =>
                {
                    value = Mathf.Clamp(value, Lower(), max);
                    slider.SetValueWithoutNotify(value);
                    valueText.text = format != null ? format(value) : value.ToString();
                    UpdateColor(value);
                }
            };

            ValueRefreshers.Add(() =>
            {
                int value = Mathf.Clamp(getter(), Lower(), max);
                slider.SetValueWithoutNotify(value);
                valueText.text = format != null ? format(value) : value.ToString();
                UpdateColor(value);
            });

            clone.SetActive(true);
            Track(clone, title);
            ApplyTooltip(clone, name);

            return clone;
        }

        internal static GameObject AddSapphireInput(
            UI_OptionBox_Common_Integer template,
            Transform parent)
        {
            if (template == null || parent == null)
                return null;

            GameObject clone =
                UnityEngine.Object.Instantiate(
                    template.gameObject,
                    parent,
                    false
                );

            clone.name = "SephiriaExpanded_SapphireGrant";

            UI_OptionBox_Common_Integer original =
                clone.GetComponent<UI_OptionBox_Common_Integer>();

            if (original == null ||
               original.box == null ||
               original.valueText == null ||
               original.valueText.text == null)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            UI_HorizontalSelectionBox box = original.box;
            UI_LocalizationStringText valueLocalization = original.valueText;
            TextMeshProUGUI sourceText = valueLocalization.text;

            SetTitleLiteral(
                clone,
                valueLocalization,
                "사파이어 지급",
                true
            );

            original.enabled = false;
            valueLocalization.enabled = false;

            Selectable[] oldSelectables =
                box.GetComponentsInChildren<Selectable>(true);

            for (int i = 0; i < oldSelectables.Length; i++)
            {
                if (oldSelectables[i] != null)
                    oldSelectables[i].interactable = false;
            }

            Graphic[] oldGraphics =
                box.GetComponentsInChildren<Graphic>(true);

            for (int i = 0; i < oldGraphics.Length; i++)
            {
                if (oldGraphics[i] != null)
                    oldGraphics[i].raycastTarget = false;
            }

            box.enabled = false;

            GameObject inputObject = new GameObject(
                "SapphireInput",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(TMP_InputField)
            );

            RectTransform inputRect =
                inputObject.GetComponent<RectTransform>();

            RectTransform sourceRect =
                sourceText.rectTransform;

            inputRect.SetParent(
                sourceText.transform.parent,
                false
            );

            inputRect.anchorMin = sourceRect.anchorMin;
            inputRect.anchorMax = sourceRect.anchorMax;
            inputRect.pivot = sourceRect.pivot;
            inputRect.anchoredPosition = sourceRect.anchoredPosition;
            inputRect.sizeDelta = sourceRect.sizeDelta;
            inputRect.localScale = Vector3.one;

            inputObject.transform.SetAsLastSibling();

            Image background =
                inputObject.GetComponent<Image>();

            background.color =
                new Color(0f, 0f, 0f, 0.001f);

            background.raycastTarget = true;

            GameObject textObject = new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)
            );

            RectTransform textRect =
                textObject.GetComponent<RectTransform>();

            textRect.SetParent(inputRect, false);

            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI inputText =
                textObject.GetComponent<TextMeshProUGUI>();

            inputText.font = sourceText.font;
            inputText.fontSize = sourceText.fontSize;
            inputText.fontStyle = sourceText.fontStyle;
            inputText.alignment = sourceText.alignment;
            inputText.color = sourceText.color;

            inputText.raycastTarget = false;
            inputText.textWrappingMode = TextWrappingModes.NoWrap;
            inputText.overflowMode = TextOverflowModes.Overflow;

            TMP_InputField input =
                inputObject.GetComponent<TMP_InputField>();

            input.targetGraphic = background;
            input.textViewport = inputRect;
            input.textComponent = inputText;

            input.contentType =
                TMP_InputField.ContentType.IntegerNumber;

            input.lineType =
                TMP_InputField.LineType.SingleLine;

            input.characterLimit = 11;
            input.interactable = true;
            input.readOnly = false;
            input.caretWidth = 2;

            input.navigation = new Navigation
            {
                mode = Navigation.Mode.Automatic
            };

            sourceText.gameObject.SetActive(false);

            input.SetTextWithoutNotify("0");

            input.onSelect.AddListener(delegate
            {
                input.ActivateInputField();
            });

            input.onEndEdit.AddListener(text =>
            {
                int delta;

                if (!int.TryParse(text, out delta))
                {
                    input.SetTextWithoutNotify("0");
                    return;
                }

                if (delta != 0)
                {
                    bool success =
                        MoreSapphire.ApplyDelta(delta);

                    if (!success)
                    {
                        Debug.LogWarning(
                            "[Sephiria Expanded] 사파이어 지급 실패"
                        );
                    }
                }

                input.SetTextWithoutNotify("0");
            });

            UnityEngine.Object.Destroy(original);

            ApplyTooltip(clone, "SapphireGrant");
            clone.SetActive(true);
            Track(clone);

            return clone;
        }

        internal static void SetVisible(
            GameObject row,
            bool visible)
        {
            if (row == null) return;

            if (visible)
            {
                SliderBinding binding;

                if (SliderBindings.TryGetValue(
                    row,
                    out binding))
                {
                    if (binding != null &&
                       binding.Slider != null &&
                       binding.Text != null &&
                       binding.Getter != null)
                    {
                        int min =
                            Mathf.RoundToInt(
                                binding.Slider.minValue
                            );

                        int max =
                            Mathf.RoundToInt(
                                binding.Slider.maxValue
                            );

                        int value = min;

                        try
                        {
                            value =
                                Mathf.Clamp(
                                    binding.Getter(),
                                    min,
                                    max
                                );
                        }
                        catch
                        {
                        }

                        if (binding.Apply != null)
                        {
                            binding.Apply(value);
                        }
                        else
                        {
                            binding.Slider.SetValueWithoutNotify(
                                value
                            );

                            binding.Text.text =
                                binding.Format != null ? binding.Format(value) : value.ToString();
                        }
                    }
                }
            }

            if (row.activeSelf != visible)
                row.SetActive(visible);
        }

        internal static void SetInteractable(GameObject row, bool interactable)
        {
            if (row == null) return;

            Toggle toggle = row.GetComponentInChildren<Toggle>(true);
            if (toggle != null)
                toggle.interactable = interactable;

            RowTitle rowTitle = row.GetComponent<RowTitle>();
            if (rowTitle != null)
                rowTitle.SetLocked(!interactable);
        }

        internal static void RefreshValues()
        {
            for (int i = 0; i < ValueRefreshers.Count; i++)
            {
                try
                {
                    ValueRefreshers[i]();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            RefreshTitles();
        }

        private static bool IsSubItem(string title)
        {
            return title == null || title.StartsWith(SubItemPrefix, StringComparison.Ordinal);
        }

        private static string FormatTitle(string title, bool active)
        {
            return FormatTitle(title, active ? BulletActiveColor : BulletInactiveColor);
        }

        private static string FormatTitle(string title, string color)
        {
            if (title == null) return title;

            string marker = "<color=" + color + ">" + Bullet + "</color> ";
            if (!IsSubItem(title)) return marker + title;

            string name = title.TrimStart(' ');
            int level = Math.Max(1, (title.Length - name.Length) / SubItemPrefix.Length);
            return new string(' ', SubItemFirstIndent + (level - 1) * SubItemStepIndent) + marker + name;
        }

        private static RowTitle BindTitle(GameObject row, TMP_Text text, string title)
        {
            if (text == null || title == null) return null;

            RowTitle rowTitle = row.AddComponent<RowTitle>();
            rowTitle.Text = text;
            rowTitle.Title = title;
            return rowTitle;
        }

        private static void SetTitleState(RowTitle rowTitle, bool active)
        {
            if (rowTitle != null) rowTitle.Set(active);
        }

        private static TMP_Text SetTitleLiteral(
            GameObject clone,
            UI_LocalizationStringText skip,
            string title,
            bool active = false,
            string color = null)
        {
            title = color != null ? FormatTitle(title, color) : FormatTitle(title, active);

            UI_LocalizationStringText[] localized =
                clone.GetComponentsInChildren<
                    UI_LocalizationStringText
                >(true);

            for (int i = 0; i < localized.Length; i++)
            {
                UI_LocalizationStringText current =
                    localized[i];

                if (current == null ||
                   current == skip ||
                   current.text == null)
                {
                    continue;
                }

                current.enabled = false;
                current.text.text = title;
                current.text.color = Color.white;

                return current.text;
            }

            TextMeshProUGUI skipText =
                skip != null
                    ? skip.text
                    : null;

            TextMeshProUGUI[] texts =
                clone.GetComponentsInChildren<
                    TextMeshProUGUI
                >(true);

            for (int i = 0; i < texts.Length; i++)
            {
                TextMeshProUGUI text = texts[i];

                if (text == null || text == skipText)
                    continue;

                text.text = title;
                text.color = Color.white;
                return text;
            }

            return null;
        }
    }
}
