using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SephiriaExpanded
{
    internal static class ExpandedSettingsPanel
    {
        private const string PanelName = "SephiriaExpanded_SettingsPanel";
        private const string PauseButtonName = "SephiriaExpanded_PauseSettingsButton";
        private const string OpenSoundEvent = "event:/System/GameStart";
        private const string DevPauseButtonName = "SephiriaExpanded_PauseDevButton";
        private static readonly Color DevPauseButtonColor = new Color(1f, 0.55f, 0.1f, 1f);
        private static GameObject DevPauseButtonRoot;

        private static UI_OptionsPanel SourcePanel;
        private static UI_OptionsPanel Panel;
        private static GameObject PanelRoot;
        private static UI_Tab Tab;
        private static UI_TabContent Content;
        private static Transform RowParent;
        private static ScrollRect Scroll;
        private static readonly string[] TabTitlesKorean = { "로컬", "멀티", "배율", "패턴 강화", "기타" };
        private static readonly string[] TabTitlesEnglish = { "Local", "Multi", "Scaling", "Patterns", "Misc" };
        private static string[] TabTitles
        {
            get { return ModText.T(TabTitlesKorean, TabTitlesEnglish); }
        }
        private static readonly UI_TabContent[] TabContents = new UI_TabContent[TabTitles.Length];
        private static readonly Transform[] TabRowParents = new Transform[TabTitles.Length];
        private static readonly ScrollRect[] TabScrolls = new ScrollRect[TabTitles.Length];

        private static UI_OptionBox_Common_Integer IntegerTemplate;
        private static UI_MuteBackgroundAudioToggle ToggleTemplate;
        private static UI_VolumeSlider SliderTemplate;
        private static GameObject HeaderTemplate;
        private static GameObject BlankTemplate;
        private static UI_HorayButton HorayButtonTemplate;
        private static GameObject RebindTemplate;

        private static UI_PausePanel PausePanel;
        private static GameObject PauseButtonRoot;
        private static Button PauseButton;
        private static Button VanillaOptionButton;

        private static readonly List<GameObject> SpawnFloorRows = new List<GameObject>();
        private static readonly List<GameObject> HpFloorRows = new List<GameObject>();
        private static readonly List<GameObject> DamageTypeRows = new List<GameObject>();
        private static readonly List<GameObject> SpawnTypeRows = new List<GameObject>();
        private static readonly List<GameObject> HpTypeRows = new List<GameObject>();
        private static readonly List<GameObject> DamageFloorRows = new List<GameObject>();
        private static readonly List<GameObject> SpeedFloorRows = new List<GameObject>();
        private static readonly List<GameObject> SpeedTypeRows = new List<GameObject>();
        private static readonly List<GameObject> DifficultyBossRows = new List<GameObject>();

        private static GameObject DifficultyGlobalRow;
        private static GameObject DifficultyBreakRow;
        private static GameObject DifficultyRecommendedRow;
        private static GameObject SpawnGlobalRow;
        private static GameObject HpGlobalRow;
        private static GameObject SpeedGlobalRow;
        private static GameObject DamageGlobalRow;
        private static GameObject FountainCapacityRow;
        private static GameObject MinimapOpenTeleportRow;
        private static GameObject MoreInventoryLevelIntervalRow;
        private static GameObject MoreInventorySlotsRow;
        private static GameObject MoreMapRow;
        private static GameObject CutsceneSkipRow;
        private static GameObject ChatTimeModeRow;
        private static GameObject CursorOutlineColorRow;
        private const int BossHpFormatAll = 4;
        private static GameObject FriendlyFireMultiplierRow;
        private static GameObject KillLogRow;
        private static GameObject DamageMeterKeyRow;
        private static readonly List<GameObject> NameColorExtraRows = new List<GameObject>();
        private static readonly List<GameObject> WalletShareRows = new List<GameObject>();
        private static bool? MoreMapInteractable;
        private static bool? CutsceneSkipInteractable;
        private static readonly List<GameObject>[] TabRows =
        {
            new List<GameObject>(),
            new List<GameObject>(),
            new List<GameObject>(),
            new List<GameObject>(),
            new List<GameObject>()
        };

        private static float NextEnsure;
        private static bool WasOpened;
        private static bool PendingPauseSelection;
        private static int ActiveTab;

        internal static bool Ready
        {
            get
            {
                return Panel != null && PanelRoot != null && Tab != null && Content != null && RowParent != null &&
                       IntegerTemplate != null && ToggleTemplate != null && SliderTemplate != null && HeaderTemplate != null;
            }
        }

        internal sealed class Kit
        {
            internal UI_OptionsPanel Panel;
            internal GameObject PanelRoot;
            internal UI_OptionBox_Common_Integer Integer;
            internal UI_MuteBackgroundAudioToggle Toggle;
            internal UI_VolumeSlider Slider;
            internal UI_HorayButton HorayButton;
            internal GameObject Header;
            internal GameObject Blank;
        }

        internal static Kit GetKit()
        {
            if (!Ready) return null;

            UI_HorayButton horayButton = HorayButtonTemplate;
            if (horayButton == null)
            {
                Transform resetRow = PanelRoot.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(x => x != null && x.name == "SephiriaExpanded_Button_ResetAllSettings");
                horayButton = resetRow == null ? null : resetRow.GetComponentInChildren<UI_HorayButton>(true);
            }

            if (horayButton == null) return null;

            return new Kit
            {
                Panel = Panel,
                PanelRoot = PanelRoot,
                Integer = IntegerTemplate,
                Toggle = ToggleTemplate,
                Slider = SliderTemplate,
                HorayButton = horayButton,
                Header = HeaderTemplate,
                Blank = BlankTemplate
            };
        }

        internal static void Tick()
        {
            FirstAnvil.Tick();
            FirstArtifactLegendary.Tick();
            MoreMultiplayer.Tick();
            BossAggro.Tick();
            BossHpText.Tick();
            ChatConvenience.Tick();

            if (Time.unscaledTime >= NextEnsure)
            {
                NextEnsure = Time.unscaledTime + 0.25f;

                EnsurePauseButton();
                SyncDevPauseButton();

                if (!Ready)
                    TryCreatePanel();
            }

            if (Panel != null && Panel.IsOpened)
                SyncTabHighlight();

            if (LanguageRefreshPending)
                ApplyLanguageRefresh();

            SyncMoreMapRow();
            SyncCutsceneSkipRow();
            HandleReturnToPause();
        }

        private static void SyncMoreMapRow()
        {
            if (MoreMapRow == null) return;

            bool interactable = !MoreMapCompatibility.IsOptionLocked();
            if (MoreMapInteractable == interactable) return;

            MoreMapInteractable = interactable;
            ExpandedOptionRows.SetInteractable(MoreMapRow, interactable);
        }

        private static void SyncCutsceneSkipRow()
        {
            if (CutsceneSkipRow == null) return;

            bool interactable = !CutsceneSkip.IsOptionLocked();
            if (CutsceneSkipInteractable == interactable) return;

            CutsceneSkipInteractable = interactable;
            ExpandedOptionRows.SetInteractable(CutsceneSkipRow, interactable);
        }

        private static void TryCreatePanel()
        {
            UI_OptionsPanel vanilla = null;

            try
            {
                if (UIManager.Instance != null)
                    vanilla = UIManager.Instance.GetElement<UI_OptionsPanel>();
            }
            catch
            {
            }

            if (vanilla == null || vanilla.IsOpened || vanilla.ParentRoot == null) return;
            if (Ready && SourcePanel == vanilla) return;

            if (PanelRoot != null || SourcePanel != null)
                CleanupPanelOnly();

            GameObject clone = null;

            try
            {
                RemoveNamedChild(vanilla.transform.parent, PanelName);

                clone = UnityEngine.Object.Instantiate(vanilla.gameObject, vanilla.transform.parent);
                clone.name = PanelName;
                clone.SetActive(false);

                UI_OptionsPanel clonedPanel = clone.GetComponent<UI_OptionsPanel>();

                if (clonedPanel == null)
                    throw new InvalidOperationException("UI_OptionsPanel 복제 실패");

                UI_Tab clonedTab = clonedPanel.tab;

                if (clonedTab == null)
                    throw new InvalidOperationException("UI_Tab을 찾지 못했습니다.");

                if (clonedTab.tabButtons == null || clonedTab.tabButtons.Length < 5)
                    throw new InvalidOperationException("5개의 옵션 탭 버튼을 찾지 못했습니다.");

                if (clonedTab.tabContents == null || clonedTab.tabContents.Length < 5)
                    throw new InvalidOperationException("5개의 옵션 탭 Content를 찾지 못했습니다.");

                clonedPanel.SetRoot(vanilla.ParentRoot);
                clonedPanel.hasControl = true;
                clonedPanel.canCloseControlWithESC = true;
                clonedPanel.worldwideModeObjects = Array.Empty<GameObject>();

                clonedTab.tabButtons = clonedTab.tabButtons.Take(5).ToArray();
                clonedTab.tabContents = clonedTab.tabContents.Take(5).ToArray();

                string[] tabContentNames =
                {
                    "SephiriaExpanded_TabContent_0",
                    "SephiriaExpanded_TabContent_1",
                    "SephiriaExpanded_TabContent_2",
                    "SephiriaExpanded_TabContent_3",
                    "SephiriaExpanded_TabContent_4"
                };

                for (int i = 0; i < clonedTab.tabContents.Length; i++)
                {
                    if (clonedTab.tabContents[i] != null)
                        clonedTab.tabContents[i].gameObject.name = tabContentNames[i];
                }

                UI_TabButton firstButton = clonedTab.tabButtons[0];
                UI_TabContent firstContent = clonedTab.tabContents[0];

                if (firstButton == null || firstContent == null)
                    throw new InvalidOperationException("첫 번째 탭을 찾지 못했습니다.");

                for (int i = 0; i < clonedTab.tabButtons.Length; i++)
                {
                    UI_TabButton button = clonedTab.tabButtons[i];

                    if (button == null) continue;

                    button.gameObject.SetActive(true);
                    SetTabTitle(button, TabTitles[i]);
                }

                for (int i = 0; i < TabContents.Length; i++)
                {
                    UI_TabContent tabContent = clonedTab.tabContents[i];
                    if (tabContent == null)
                        throw new InvalidOperationException("탭 콘텐츠 " + i + "를 찾지 못했습니다.");

                    TabContents[i] = tabContent;
                    tabContent.parent = clonedPanel;
                    tabContent.CloseTab();
                    TabRowParents[i] = FindRowParent(tabContent);
                    TabScrolls[i] = FindScroll(tabContent, TabRowParents[i]);

                    if (TabRowParents[i] == null)
                        throw new InvalidOperationException("탭 콘텐츠 " + i + "의 옵션 부모를 찾지 못했습니다.");

                    HideVanillaRows(tabContent, TabRowParents[i]);
                }

                UI_OptionBox_Common_Integer sourceInteger =
                    firstContent.GetComponentsInChildren<UI_OptionBox_Common_Integer>(true)
                        .FirstOrDefault(IsIntegerTemplate);

                if (sourceInteger == null)
                    throw new InvalidOperationException("UI_OptionBox_Common_Integer 템플릿을 찾지 못했습니다.");

                UI_MuteBackgroundAudioToggle sourceToggle =
                    clonedPanel.GetComponentsInChildren<UI_MuteBackgroundAudioToggle>(true)
                        .FirstOrDefault(x => x != null &&
                                             x.toggle != null &&
                                             !x.gameObject.name.StartsWith("SephiriaExpanded_", StringComparison.Ordinal));

                if (sourceToggle == null)
                    throw new InvalidOperationException("UI_MuteBackgroundAudioToggle 템플릿을 찾지 못했습니다.");

                UI_VolumeSlider sourceSlider =
                    clonedPanel.GetComponentsInChildren<UI_VolumeSlider>(true)
                        .FirstOrDefault(x => x != null &&
                                             x.slider != null &&
                                             x.valueText != null &&
                                             !x.gameObject.name.StartsWith("SephiriaExpanded_", StringComparison.Ordinal));

                if (sourceSlider == null)
                    throw new InvalidOperationException("UI_VolumeSlider 템플릿을 찾지 못했습니다.");

                Transform parent = sourceInteger.transform.parent;

                if (parent == null)
                    throw new InvalidOperationException("옵션 Content 부모를 찾지 못했습니다.");

                Transform headerSource = parent.Find("UILabel");

                if (headerSource == null)
                    headerSource = parent.Cast<Transform>().FirstOrDefault(x => x != null && x.name == "EffectLabel");

                if (headerSource == null)
                    throw new InvalidOperationException("UILabel 템플릿을 찾지 못했습니다.");

                ScrollRect foundScroll = TabScrolls[0] ?? FindScroll(firstContent, parent);

                if (foundScroll == null)
                    throw new InvalidOperationException("Gameplay ScrollRect를 찾지 못했습니다.");

                try
                {
                    ReplaceScrollbarFromKeyboard(clonedTab, foundScroll);
                    ReplaceScrollWithFirstCopy(clonedTab, ScrollCopyIndex, foundScroll, parent);
                    CloneFirstScrollbarToOtherTabs(clonedTab);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[Sephiria Expanded] Keyboard Scrollbar 복제 실패");
                    Debug.LogException(exception);
                }

                GameObject templates = new GameObject("SephiriaExpanded_SettingsTemplates", typeof(RectTransform));
                templates.transform.SetParent(clone.transform, false);
                templates.SetActive(false);

                GameObject integerObject = UnityEngine.Object.Instantiate(sourceInteger.gameObject, templates.transform, false);
                integerObject.name = "Integer";
                integerObject.SetActive(false);

                GameObject toggleObject = UnityEngine.Object.Instantiate(sourceToggle.gameObject, templates.transform, false);
                toggleObject.name = "Toggle";
                toggleObject.SetActive(false);

                GameObject sliderObject = UnityEngine.Object.Instantiate(sourceSlider.gameObject, templates.transform, false);
                sliderObject.name = "Slider";
                sliderObject.SetActive(false);

                GameObject headerObject = UnityEngine.Object.Instantiate(headerSource.gameObject, templates.transform, false);
                headerObject.name = "Header";
                headerObject.SetActive(false);

                GameObject blankObject = CreateBlankTemplate(clonedPanel.transform, templates.transform);

                GameObject rebindObject = CreateRebindTemplate(clonedPanel.transform, templates.transform);

                UI_OptionBox_Common_Integer integerTemplate = integerObject.GetComponent<UI_OptionBox_Common_Integer>();
                UI_MuteBackgroundAudioToggle toggleTemplate = toggleObject.GetComponent<UI_MuteBackgroundAudioToggle>();
                UI_VolumeSlider sliderTemplate = sliderObject.GetComponent<UI_VolumeSlider>();

                if (integerTemplate == null || toggleTemplate == null || sliderTemplate == null)
                    throw new InvalidOperationException("옵션 템플릿 생성 실패");

                firstContent.CloseTab();

                SourcePanel = vanilla;
                Panel = clonedPanel;
                PanelRoot = clone;
                Tab = clonedTab;
                Content = firstContent;
                RowParent = parent;
                Scroll = foundScroll;
                IntegerTemplate = integerTemplate;
                ToggleTemplate = toggleTemplate;
                SliderTemplate = sliderTemplate;
                HeaderTemplate = headerObject;
                BlankTemplate = blankObject;
                RebindTemplate = rebindObject;
                HorayButtonTemplate = clonedPanel.GetComponentsInChildren<UI_HorayButton>(true)
                    .FirstOrDefault(x => x != null &&
                                         x.gameObject != null &&
                                         x.gameObject != clone &&
                                         x.gameObject.name.IndexOf("DisplayApplyButton", StringComparison.OrdinalIgnoreCase) < 0);

                if (HorayButtonTemplate == null)
                {
                    HorayButtonTemplate = Resources.FindObjectsOfTypeAll<UI_HorayButton>()
                        .FirstOrDefault(x => x != null &&
                                             x.gameObject != null &&
                                             x.gameObject != clone &&
                                             x.gameObject.name.IndexOf("DisplayApplyButton", StringComparison.OrdinalIgnoreCase) < 0);
                }

                clonedTab.OnTabSelected += HandleTabSelected;

                BuildRows();
                DistributeRowsToTabs();
                RemoveVanillaContentRows();
                RemoveVanillaDisplayApplyButton();
                RewireCloseButton();
                SelectFirstRow();

                PanelRoot.SetActive(false);

                Debug.Log("[Sephiria Expanded] 설정 패널 생성 완료");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Sephiria Expanded] 설정 패널 생성 실패");
                Debug.LogException(exception);

                if (clone != null)
                {
                    clone.SetActive(false);
                    UnityEngine.Object.Destroy(clone);
                }

                SourcePanel = null;
                Panel = null;
                PanelRoot = null;
                Tab = null;
                Content = null;
                RowParent = null;
                Scroll = null;
                for (int i = 0; i < TabContents.Length; i++)
                {
                    TabContents[i] = null;
                    TabRowParents[i] = null;
                    TabScrolls[i] = null;
                }
                IntegerTemplate = null;
                ToggleTemplate = null;
                SliderTemplate = null;
                HeaderTemplate = null;
                BlankTemplate = null;
                HorayButtonTemplate = null;
                RebindTemplate = null;

                NextEnsure = Time.unscaledTime + 2f;
            }
        }

        private const int ScrollCopyIndex = 2;

        private static void ReplaceScrollWithFirstCopy(UI_Tab tab, int index, ScrollRect source, Transform sourceRowParent)
        {
            ScrollRect old = TabScrolls[index];
            if (old == null || source == null || old == source) return;

            Transform container = old.transform.parent;
            GameObject copy = UnityEngine.Object.Instantiate(source.gameObject, container, false);
            copy.transform.SetSiblingIndex(old.transform.GetSiblingIndex());

            ScrollRect copyScroll = copy.GetComponent<ScrollRect>();
            if (copyScroll == null)
            {
                UnityEngine.Object.Destroy(copy);
                return;
            }

            Scrollbar oldBar = old.verticalScrollbar;
            Scrollbar sourceBar = source.verticalScrollbar;
            if (sourceBar != null && !sourceBar.transform.IsChildOf(source.transform))
            {
                Transform barParent = oldBar != null && oldBar.transform.parent != null ? oldBar.transform.parent : container;
                GameObject barCopy = UnityEngine.Object.Instantiate(sourceBar.gameObject, barParent, false);
                barCopy.name = "SephiriaExpanded_Scrollbar_" + index;

                Scrollbar copyBar = barCopy.GetComponent<Scrollbar>();
                if (copyBar != null)
                {
                    copyScroll.verticalScrollbar = copyBar;
                    barCopy.SetActive(true);
                }
                else
                {
                    UnityEngine.Object.Destroy(barCopy);
                }
            }

            if (oldBar != null && !oldBar.transform.IsChildOf(old.transform))
            {
                oldBar.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(oldBar.gameObject);
            }

            old.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(old.gameObject);

            TabScrolls[index] = copyScroll;
            TabRowParents[index] = MapDescendant(source.transform, sourceRowParent, copy.transform) ?? FindRowParent(tab.tabContents[index]);
        }

        private static Transform MapDescendant(Transform sourceRoot, Transform sourceDescendant, Transform destinationRoot)
        {
            if (sourceDescendant == null || !sourceDescendant.IsChildOf(sourceRoot)) return null;
            if (sourceDescendant == sourceRoot) return destinationRoot;

            List<string> names = new List<string>();
            for (Transform current = sourceDescendant; current != null && current != sourceRoot; current = current.parent)
                names.Insert(0, current.name);

            return destinationRoot.Find(string.Join("/", names));
        }

        private static GameObject CreateBlankTemplate(Transform panel, Transform templates)
        {
            Transform[] all = panel.GetComponentsInChildren<Transform>(true);
            Transform source = null;

            foreach (Transform candidate in all)
            {
                if (candidate == null || candidate.name != "Blank") continue;

                if (source == null) source = candidate;

                if (!HasAncestorNamed(candidate, panel, "Sound")) continue;

                source = candidate;
                break;
            }

            GameObject template;

            if (source != null)
            {
                template = UnityEngine.Object.Instantiate(source.gameObject, templates, false);
            }
            else
            {
                Debug.LogWarning("[Sephiria Expanded] 바닐라 Blank 오브젝트를 찾지 못해 기본 공백을 사용합니다.");
                template = new GameObject("Blank", typeof(RectTransform), typeof(LayoutElement));
                template.transform.SetParent(templates, false);
                LayoutElement layout = template.GetComponent<LayoutElement>();
                layout.minHeight = 30f;
                layout.preferredHeight = 30f;
            }

            template.name = "Blank";
            template.SetActive(false);
            return template;
        }

        private static bool HasAncestorNamed(Transform target, Transform stop, string name)
        {
            for (Transform parent = target.parent; parent != null && parent != stop; parent = parent.parent)
                if (parent.name == name)
                    return true;

            return false;
        }

        private static GameObject CreateRebindTemplate(Transform panel, Transform templates)
        {
            Transform[] all = panel.GetComponentsInChildren<Transform>(true);
            Transform source = all.FirstOrDefault(x => IsRebindRow(x, "KeyboardActionSingle-PrevTab")) ??
                               all.FirstOrDefault(x => IsRebindRow(x, "KeyboardActionSingle"));

            if (source == null)
            {
                Debug.LogWarning("[Sephiria Expanded] 키 설정 템플릿(KeyboardActionSingle-PrevTab)을 찾지 못했습니다.");
                return null;
            }

            GameObject template = UnityEngine.Object.Instantiate(source.gameObject, templates, false);
            template.name = "Rebind";
            template.SetActive(false);
            return template;
        }

        private static bool IsRebindRow(Transform target, string namePrefix)
        {
            return target != null &&
                   target.name.StartsWith(namePrefix, StringComparison.Ordinal) &&
                   target.GetComponentInChildren<RebindActionUI>(true) != null;
        }

        private static void ReplaceScrollbarFromKeyboard(UI_Tab tab, ScrollRect targetScroll)
        {
            if (tab == null || targetScroll == null || tab.tabContents == null || tab.tabContents.Length < 4) return;

            UI_TabContent keyboardContent = tab.tabContents[3];

            if (keyboardContent == null) return;

            ScrollRect[] scrollRects = keyboardContent.GetComponentsInChildren<ScrollRect>(true);
            ScrollRect keyboardScroll = null;

            for (int i = 0; i < scrollRects.Length; i++)
            {
                ScrollRect candidate = scrollRects[i];

                if (candidate == null || candidate.verticalScrollbar == null) continue;

                keyboardScroll = candidate;
                break;
            }

            if (keyboardScroll == null || keyboardScroll.verticalScrollbar == null)
            {
                Debug.LogWarning("[Sephiria Expanded] Keyboard Scrollbar를 찾지 못했습니다.");
                return;
            }

            Scrollbar oldScrollbar = targetScroll.verticalScrollbar;

            Transform targetParent;

            if (oldScrollbar != null && oldScrollbar.transform.parent != null)
                targetParent = oldScrollbar.transform.parent;
            else
                targetParent = targetScroll.transform;

            int siblingIndex = oldScrollbar != null
                ? oldScrollbar.transform.GetSiblingIndex()
                : targetParent.childCount;

            GameObject scrollbarObject =
                UnityEngine.Object.Instantiate(
                    keyboardScroll.verticalScrollbar.gameObject,
                    targetParent,
                    false
                );

            scrollbarObject.name = "SephiriaExpanded_Scrollbar Vertical";
            scrollbarObject.transform.SetSiblingIndex(siblingIndex);

            Scrollbar newScrollbar = scrollbarObject.GetComponent<Scrollbar>();

            if (newScrollbar == null)
            {
                UnityEngine.Object.Destroy(scrollbarObject);
                return;
            }

            if (oldScrollbar != null)
                oldScrollbar.gameObject.SetActive(false);

            targetScroll.verticalScrollbar = newScrollbar;
            targetScroll.vertical = true;

            scrollbarObject.SetActive(true);

            Debug.Log("[Sephiria Expanded] Keyboard Scrollbar 적용 완료");
        }

        private static void CloneFirstScrollbarToOtherTabs(UI_Tab tab)
        {
            ScrollRect sourceScroll = TabScrolls[0];
            Scrollbar sourceScrollbar = sourceScroll == null ? null : sourceScroll.verticalScrollbar;
            if (sourceScrollbar == null || tab == null || tab.tabContents == null)
                return;

            for (int i = 1; i < tab.tabContents.Length; i++)
            {
                if (i == ScrollCopyIndex) continue;

                ScrollRect targetScroll = i < TabScrolls.Length
                    ? TabScrolls[i]
                    : FindScroll(tab.tabContents[i], FindRowParent(tab.tabContents[i]));
                if (targetScroll == null || targetScroll == sourceScroll)
                    continue;

                Scrollbar oldScrollbar = targetScroll.verticalScrollbar;
                Transform targetParent = oldScrollbar != null && oldScrollbar.transform.parent != null
                    ? oldScrollbar.transform.parent
                    : targetScroll.transform;

                GameObject scrollbarObject = UnityEngine.Object.Instantiate(
                    sourceScrollbar.gameObject,
                    targetParent,
                    false
                );
                scrollbarObject.name = "SephiriaExpanded_Scrollbar_" + i;

                Scrollbar clonedScrollbar = scrollbarObject.GetComponent<Scrollbar>();
                if (clonedScrollbar == null)
                {
                    UnityEngine.Object.Destroy(scrollbarObject);
                    continue;
                }

                if (oldScrollbar != null)
                    oldScrollbar.gameObject.SetActive(false);

                targetScroll.verticalScrollbar = clonedScrollbar;
                scrollbarObject.SetActive(true);
            }
        }

        private static void RemoveVanillaContentRows()
        {
            for (int i = 0; i < TabRowParents.Length; i++)
            {
                Transform parent = TabRowParents[i];
                if (parent == null)
                    continue;

                for (int childIndex = parent.childCount - 1; childIndex >= 0; childIndex--)
                {
                    Transform child = parent.GetChild(childIndex);
                    if (child == null)
                        continue;

                    if (child.name.StartsWith("SephiriaExpanded_", StringComparison.Ordinal))
                        continue;

                    child.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }
        }

        private static void RemoveVanillaDisplayApplyButton()
        {
            UI_TabContent hostContent = GetTabContent(2);
            if (hostContent == null)
                return;

            Transform[] transforms = hostContent.GetComponentsInChildren<Transform>(true);
            for (int i = transforms.Length - 1; i >= 0; i--)
            {
                Transform current = transforms[i];
                if (current == null || current.name.IndexOf("DisplayApplyButton", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                current.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(current.gameObject);
            }
        }

        private static void BuildRows()
        {
            if (!Ready) return;

            ExpandedOptionRows.Clear();

            SpawnFloorRows.Clear();
            HpFloorRows.Clear();
            DamageTypeRows.Clear();
            SpawnTypeRows.Clear(); HpTypeRows.Clear(); DamageFloorRows.Clear();
            SpeedFloorRows.Clear(); SpeedTypeRows.Clear(); DifficultyBossRows.Clear();

            DifficultyGlobalRow = null; DifficultyBreakRow = null; DifficultyRecommendedRow = null;
            SpawnGlobalRow = null;
            HpGlobalRow = null;
            SpeedGlobalRow = null;
            FountainCapacityRow = null;
            for (int i = 0; i < TabRows.Length; i++)
                TabRows[i].Clear();
            MoreInventoryLevelIntervalRow = null;
            MoreInventorySlotsRow = null;
            MoreMapRow = null;
            MoreMapInteractable = null;
            CutsceneSkipRow = null;
            CutsceneSkipInteractable = null;
            ChatTimeModeRow = null;
            CursorOutlineColorRow = null;
            FriendlyFireMultiplierRow = null;
            KillLogRow = null;
            DamageMeterKeyRow = null;
            NameColorExtraRows.Clear();
            WalletShareRows.Clear();

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "ChatConvenienceEnabled",
                ModText.T("채팅 편의성 기능", "Chat Convenience"),
                () => ModSettings.ChatConvenienceEnabled,
                value =>
                {
                    ModSettings.ChatConvenienceEnabled = value;
                    RefreshConditionalRows();
                }
            );

            ChatTimeModeRow = ExpandedOptionRows.AddChoice(
                IntegerTemplate,
                RowParent,
                "ChatTimeMode",
                ModText.T("   시간 표현 방식", "   Time Format"),
                new[] { ModText.T("실제 시간", "Real Time"), ModText.T("게임 진행 시간", "Play Time") },
                () => ModSettings.ChatTimeMode,
                value => ModSettings.ChatTimeMode = value,
                activeWhen: () => true
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "ShopFavoriteEnabled",
                ModText.T("상점 즐겨찾기 표시", "Show Favorites in Shop"),
                () => ModSettings.ShopFavoriteEnabled,
                value => ModSettings.ShopFavoriteEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "EnchantMarkEnabled",
                ModText.T("인챈트 횟수 표시", "Show Enchant Count"),
                () => ModSettings.EnchantMarkEnabled,
                value => ModSettings.EnchantMarkEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "MorePresetEnabled",
                ModText.T("프리셋 슬롯 확장", "Preset Slot Expansion"),
                () => ModSettings.MorePresetEnabled,
                value => ModSettings.MorePresetEnabled = value
            );


            ExpandedOptionRows.AddRebind(
                RebindTemplate,
                RowParent,
                "AlwaysRunEnabled",
                ModText.T("자동 달리기", "Auto Run"),
                AlwaysRun.ToggleKey,
                AlwaysRun.ValidateRebind,
                () => AlwaysRun.HasBinding,
                ToggleTemplate.GetComponent<Graphic>()
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "DamageMeterEnabled",
                ModText.T("미터기 활성화", "Damage Meter"),
                () => ModSettings.DamageMeterEnabled,
                value => ModSettings.DamageMeterEnabled = value,
                RefreshConditionalRows
            );

            ExpandedOptionRows.AddChoice(
                IntegerTemplate,
                RowParent,
                "NameColorStart",
                ModText.T("닉네임 색상 변경", "Nickname Color"),
                NameColor.StartLabels(),
                () => ModSettings.NameColorStart,
                value => ModSettings.NameColorStart = value,
                () =>
                {
                    NameColor.Refresh();
                    RefreshConditionalRows();
                },
                NameColor.Colors(null)
            );

            NameColorExtraRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "NameColorA", ModText.T("   그라데이션 색 1", "   Gradient Color 1"), NameColor.EndLabels(), () => ModSettings.NameColorA, value => ModSettings.NameColorA = value, NameColor.Refresh, NameColor.Colors(null)));
            NameColorExtraRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "NameColorB", ModText.T("   그라데이션 색 2", "   Gradient Color 2"), NameColor.EndLabels(), () => ModSettings.NameColorB, value => ModSettings.NameColorB = value, NameColor.Refresh, NameColor.Colors(null)));

            DamageMeterKeyRow = ExpandedOptionRows.AddRebind(
                RebindTemplate,
                RowParent,
                "DamageMeterKey",
                ModText.T("   미터기 단축키", "   Meter Hotkey"),
                DamageMeter.Key.Action,
                DamageMeter.Key.Validate,
                () => DamageMeter.Key.HasBinding,
                ToggleTemplate.GetComponent<Graphic>()
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "HiddenRoomFinderEnabled",
                ModText.T("숨겨진 방 추적", "Hidden Room Tracker"),
                () => ModSettings.HiddenRoomFinderEnabled,
                value => ModSettings.HiddenRoomFinderEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "AnytimeOpenInventoryEnabled",
                ModText.T("전투 중 인벤토리 열기", "Open Inventory in Combat"),
                () => ModSettings.AnytimeOpenInventoryEnabled,
                value => ModSettings.AnytimeOpenInventoryEnabled = value
            );

            ExpandedOptionRows.AddChoice(
                IntegerTemplate,
                RowParent,
                "BossHpTextEnabled",
                ModText.T("보스 체력 수치 표시", "Boss HP Text"),
                new[] { ModText.T("끄기", "Off"), ModText.T("전부 켜기", "All"), ModText.T("남은 체력 / 최대 체력", "Current / Max HP"), ModText.T("남은 체력만", "Current HP Only"), ModText.T("남은 체력 + %", "Current HP + %"), ModText.T("%만", "% Only") },
                () => !ModSettings.BossHpTextEnabled ? 0 : ModSettings.BossHpTextFormat == BossHpFormatAll ? 1 : ModSettings.BossHpTextFormat + 2,
                value =>
                {
                    ModSettings.BossHpTextEnabled = value != 0;
                    if (value != 0) ModSettings.BossHpTextFormat = value == 1 ? BossHpFormatAll : value - 2;
                }
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "BossAggroEnabled",
                ModText.T("보스 어그로 대상 표시", "Show Boss Aggro Target"),
                () => ModSettings.BossAggroEnabled,
                value => ModSettings.BossAggroEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "RightClickHoldEnabled",
                ModText.T("'특수공격', '마법서', '발동형' 자동 사용", "Auto Use Special Attack, Grimoire, Active Artifact"),
                () => ModSettings.RightClickHoldEnabled,
                value => ModSettings.RightClickHoldEnabled = value
            );
            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "CursorOutlineEnabled",
                ModText.T("마우스 커서 가시성 향상", "Cursor Visibility"),
                () => ModSettings.CursorOutlineEnabled,
                value => ModSettings.CursorOutlineEnabled = value,
                RefreshConditionalRows
            );
            CursorOutlineColorRow = ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "CursorOutlineColor", ModText.T("   테두리 색", "   Outline Color"), CursorOutline.ColorNames, () => ModSettings.CursorOutlineColor, value => ModSettings.CursorOutlineColor = value, null, CursorOutline.ColorChoices(), () => true);
            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "MoreInventoryEnabled",
                ModText.T("레벨당 인벤토리 지급", "Inventory per Level"),
                () => ModSettings.MoreInventoryEnabled,
                value =>
                {
                    if (!value && ModSettings.MoreInventoryEnabled)
                        MoreInventory.RemoveGrantedStorage();

                    ModSettings.MoreInventoryEnabled = value;
                    RefreshConditionalRows();
                }
            );

            MoreInventoryLevelIntervalRow = ExpandedOptionRows.AddSlider(
                SliderTemplate,
                RowParent,
                "MoreInventoryLevelInterval",
                ModText.T("   레벨", "   Levels"),
                () => ModSettings.MoreInventoryLevelInterval,
                value => ModSettings.MoreInventoryLevelInterval = value,
                1,
                10,
                null,
                1
            );

            MoreInventorySlotsRow = ExpandedOptionRows.AddSlider(
                SliderTemplate,
                RowParent,
                "MoreInventorySlots",
                ModText.T("   인벤토리", "   Slots"),
                () => ModSettings.MoreInventorySlots,
                value => ModSettings.MoreInventorySlots = value,
                1,
                10,
                null,
                1
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "PlayerLevelCapEnabled",
                ModText.T("플레이어 레벨 최대치 확장", "Player Level Cap Expansion"),
                () => ModSettings.PlayerLevelCapEnabled,
                value =>
                {
                    ModSettings.PlayerLevelCapEnabled = value;
                    if (value)
                        MorePlayerLevel.Refresh();
                    else
                        MorePlayerLevel.Restore();
                }
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "InstantLeafExp",
                ModText.T("잎, 경험치 최적화", "Instant Gold & EXP"),
                () => ModSettings.InstantLeafExpEnabled,
                value => ModSettings.InstantLeafExpEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "LevelUpItemCount",
                ModText.T("남은 보상 횟수 표시", "Show Remaining Rewards"),
                () => ModSettings.LevelUpItemCountEnabled,
                value => ModSettings.LevelUpItemCountEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "SellStartItem",
                ModText.T("시작 아이템 판매 가능", "Sell Starting Items"),
                () => ModSettings.SellStartItemEnabled,
                value => ModSettings.SellStartItemEnabled = value
            );

            ExpandedOptionRows.AddChoice(
                IntegerTemplate,
                RowParent,
                "JournalWeaponOutlineMode",
                ModText.T("뿌리거둠 무기 테두리 효과", "Root's Retreat Weapon Border"),
                new[] { ModText.T("끄기", "Off"), ModText.T("A타입", "Type A"), ModText.T("B타입", "Type B"), ModText.T("B타입(S)", "Type B (S)") },
                () => ModSettings.JournalWeaponOutlineMode,
                value => ModSettings.JournalWeaponOutlineMode = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "WeaponShineEnabled",
                ModText.T("뿌리거둠 무기 반짝임 효과", "Root's Retreat Weapon Shine"),
                () => ModSettings.WeaponShineEnabled,
                value => ModSettings.WeaponShineEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "CostumeFrameWatcherEnabled",
                ModText.T("뿌리거둠 코스튬 애니메이션 효과", "Root's Retreat Costume Animation"),
                () => ModSettings.CostumeFrameWatcherEnabled,
                value =>
                {
                    ModSettings.CostumeFrameWatcherEnabled = value;
                    CostumeFrameWatcher.ForceRefresh();
                }
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "TradeMoneyShareEnabled",
                ModText.T("행운의 메달 공유", "Share Lucky Medal"),
                () => ModSettings.TradeMoneyShareEnabled,
                value => ModSettings.TradeMoneyShareEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "PotionShareEnabled",
                ModText.T("포션 획득 공유", "Share Potions"),
                () => ModSettings.PotionShareEnabled,
                value => ModSettings.PotionShareEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "FriendlyFireEnabled",
                ModText.T("팀킬 허용", "Friendly Fire"),
                () => ModSettings.FriendlyFireEnabled,
                value => ModSettings.FriendlyFireEnabled = value,
                RefreshConditionalRows
            );

            FriendlyFireMultiplierRow = ExpandedOptionRows.AddSlider(
                SliderTemplate,
                RowParent,
                "FriendlyFireMultiplier",
                ModText.T("   팀킬 데미지 배율", "   Friendly Fire Damage"),
                () => ModSettings.FriendlyFireMultiplierStep,
                value => ModSettings.FriendlyFireMultiplierStep = value,
                ModSettings.FriendlyFireStepMin,
                ModSettings.FriendlyFireStepMax,
                RateLabel,
                ModSettings.FriendlyFireStepDefault,
                true
            );

            KillLogRow = ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "KillLogEnabled",
                ModText.T("   킬 로그 표시", "   Kill Log"),
                () => ModSettings.KillLogEnabled,
                value => ModSettings.KillLogEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "MoreMultiplayerInfoEnabled",
                ModText.T("팀원 재화 표시", "Show Teammate Currency"),
                () => ModSettings.MoreMultiplayerInfoEnabled,
                value => ModSettings.MoreMultiplayerInfoEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "WalletShareEnabled",
                ModText.T("팀원과 재화 공유", "Share Wallet with Team"),
                () => ModSettings.WalletShareEnabled,
                value => ModSettings.WalletShareEnabled = value,
                RefreshConditionalRows
            );

            WalletShareRows.Add(ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "WalletShareLeaf",
                ModText.T("   잎 공유", "   Share Gold"),
                () => ModSettings.WalletShareLeaf,
                value => ModSettings.WalletShareLeaf = value
            ));

            WalletShareRows.Add(ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "WalletShareDice",
                ModText.T("   주사위 공유", "   Share Dice"),
                () => ModSettings.WalletShareDice,
                value => ModSettings.WalletShareDice = value
            ));

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "GuestReturnToTownEnabled",
                ModText.T("멀티 퇴장 시 시작마을로 이동", "Return to Town on Leaving"),
                () => ModSettings.GuestReturnToTownEnabled,
                value => ModSettings.GuestReturnToTownEnabled = value
            );

            ExpandedOptionRows.AddChoice(
                IntegerTemplate,
                RowParent,
                "PingSoundMode",
                ModText.T("핑 소리 조절", "Ping Sound"),
                PingSound.Labels,
                () => ModSettings.PingSoundMode,
                value => ModSettings.PingSoundMode = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "PingIgnoreEnabled",
                ModText.T("핑 난사 방지", "Ping Spam Prevention"),
                () => ModSettings.PingIgnoreEnabled,
                value => ModSettings.PingIgnoreEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "FountainArtifactShare",
                ModText.T("소원 분수 아티팩트 타인이 줍기", "Others Can Pick Up Fountain Artifacts"),
                () => ModSettings.FountainArtifactShareEnabled,
                value => ModSettings.FountainArtifactShareEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "Multiplayer16",
                ModText.T("최대 인원 확장", "Max Players Expansion"),
                () => ModSettings.Multiplayer16Enabled,
                value => ModSettings.Multiplayer16Enabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "MysteryPotGuaranteed",
                ModText.T("신비 항아리 확정 등장", "Guaranteed Mystic Pot"),
                () => ModSettings.MysteryPotGuaranteedEnabled,
                value => ModSettings.MysteryPotGuaranteedEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "InventorySell",
                ModText.T("상인 인벤토리 확정 판매", "Guaranteed Merchant Inventory"),
                () => ModSettings.InventorySellEnabled,
                value => ModSettings.InventorySellEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "FirstArtifactLegendary",
                ModText.T("첫 아티팩트 전설 보장", "First Artifact Legendary"),
                () => ModSettings.FirstArtifactLegendaryEnabled,
                value => ModSettings.FirstArtifactLegendaryEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "MinimapOpenEnabled",
                ModText.T("미니맵 밝히기", "Reveal Minimap"),
                () => ModSettings.MinimapOpenEnabled,
                value =>
                {
                    ModSettings.MinimapOpenTeleportEnabled = false;
                    ModSettings.MinimapOpenEnabled = value;
                    RefreshConditionalRows();
                    MinimapOpen.Refresh();
                }
            );

            MinimapOpenTeleportRow = ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "MinimapOpenTeleportEnabled",
                ModText.T("   미니맵 이동 허용", "   Minimap Travel"),
                () => ModSettings.MinimapOpenTeleportEnabled,
                value => ModSettings.MinimapOpenTeleportEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "EnchantTableEnabled",
                ModText.T("인챈트 제단 3회 고정", "Enchant Altar Fixed to 3"),
                () => ModSettings.EnchantTableEnabled,
                value => ModSettings.EnchantTableEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "FirstAnvilEnabled",
                ModText.T("첫 방 모루 보장", "First Room Anvil"),
                () => ModSettings.FirstAnvilEnabled,
                value => ModSettings.FirstAnvilEnabled = value
            );

            MoreMapRow = ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "MoreMapEnabled",
                ModText.T("맵 확장", "Map Expansion"),
                () => ModSettings.MoreMapEnabled,
                value => ModSettings.MoreMapEnabled = value
            );

            ExpandedOptionRows.AddSlider(
                SliderTemplate,
                RowParent,
                "RandomEventMultiplier",
                ModText.T("랜덤 이벤트 등장 가중치", "Random Event Weight"),
                () => ModSettings.RandomEventMultiplier,
                value => ModSettings.RandomEventMultiplier = value,
                1,
                10,
                value => "x" + value,
                1
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "FountainCapacityEnabled",
                ModText.T("소원 분수 확장 사용", "Wishing Fountain Expansion"),
                () => ModSettings.FountainCapacityEnabled,
                value =>
                {
                    ModSettings.FountainCapacityEnabled = value;
                    ModSettings.FountainCapacityBonus = 0;

                    FountainCapacity.Refresh();
                },
                RefreshConditionalRows
            );

            FountainCapacityRow = ExpandedOptionRows.AddSlider(
                SliderTemplate,
                RowParent,
                "FountainCapacityBonus",
                ModText.T("   소원 분수 추가 용량", "   Extra Capacity"),
                () => ModSettings.FountainCapacityBonus,
                value =>
                {
                    ModSettings.FountainCapacityBonus = value;
                    FountainCapacity.Refresh();
                },
                0,
                30,
                null,
                0
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "FountainArtifactUnlockEnabled",
                ModText.T("소원 분수에 모든 아이템 해금", "Unlock All Items in Wishing Fountain"),
                () => ModSettings.FountainArtifactUnlockEnabled,
                value => ModSettings.FountainArtifactUnlockEnabled = value
            );

            string[] levelNames = { ModText.T("끄기", "Off"), ModText.T("노말", "Normal"), ModText.T("하드", "Hard") };
            Color?[] levelColors = { null, Color.yellow, Color.red };
            Color?[] globalLevelColors = { Color.yellow, Color.red };
            ExpandedOptionRows.AddChoice(
                IntegerTemplate,
                RowParent,
                "DifficultyMode",
                ModText.T("패턴 강화", "Pattern Enhancement"),
                new[] { ModText.T("끄기", "Off"), ModText.T("세부 조절", "Detailed"), ModText.T("전체 조절", "Global") },
                () => ModSettings.DifficultyMode,
                value =>
                {
                    ModSettings.DifficultyMode = value;
                    ModSettings.ResetDifficultyDetail();
                },
                () =>
                {
                    ExpandedOptionRows.RefreshValues();
                    RefreshConditionalRows();
                }
            );

            DifficultyBreakRow = ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "DifficultyBossBreak",
                ModText.T("   보스 무력화 게이지 사용", "   Use Boss Break Gauge"),
                () => ModSettings.BossBreakEnabled,
                value => ModSettings.BossBreakEnabled = value
            );

            DifficultyRecommendedRow = ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "DifficultyRecommended",
                ModText.T("   추천 배율 적용", "   Apply Recommended Multipliers"),
                () => ModSettings.DifficultyRecommendedEnabled,
                value => ModSettings.DifficultyRecommendedEnabled = value
            );

            DifficultyGlobalRow = ExpandedOptionRows.AddChoice(
                IntegerTemplate,
                RowParent,
                "DifficultyGlobalLevel",
                ModText.T("   전체 난이도", "   Global Difficulty"),
                new[] { ModText.T("노말", "Normal"), ModText.T("하드", "Hard") },
                () => Mathf.Clamp(ModSettings.DifficultyGlobalLevel, 1, 2) - 1,
                value => ModSettings.DifficultyGlobalLevel = value + 1,
                null,
                globalLevelColors
            );

            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyFloor1", ModText.T("   1층 스테이지", "   Floor 1 Stages"), levelNames, () => ModSettings.DifficultyFloor1Level, value => ModSettings.DifficultyFloor1Level = value, null, levelColors));
            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyBossMoleChieftain", ModText.T("      모그디", "      Mogdi"), levelNames, () => ModSettings.MoleChieftainBossChallengeLevel, value => ModSettings.MoleChieftainBossChallengeLevel = value, null, levelColors));
            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyBossMoleBigBomb", ModText.T("      오드너", "      Odner"), levelNames, () => ModSettings.MoleBigBombBossChallengeLevel, value => ModSettings.MoleBigBombBossChallengeLevel = value, null, levelColors));
            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyBossRootDemon", ModText.T("      하즈", "      Haz"), levelNames, () => ModSettings.RootDemonBossChallengeLevel, value => ModSettings.RootDemonBossChallengeLevel = value, null, levelColors));
            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyFloor2", ModText.T("   2층 스테이지", "   Floor 2 Stages"), levelNames, () => ModSettings.DifficultyFloor2Level, value => ModSettings.DifficultyFloor2Level = value, null, levelColors));
            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyBossOinkKing", ModText.T("      라타카", "      Rataka"), levelNames, () => ModSettings.OinkKingBossChallengeLevel, value => ModSettings.OinkKingBossChallengeLevel = value, null, levelColors));
            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyBossPantherRogueKing", ModText.T("      레리드", "      Lerid"), levelNames, () => ModSettings.PantherRogueKingBossChallengeLevel, value => ModSettings.PantherRogueKingBossChallengeLevel = value, null, levelColors));
            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyFloor3", ModText.T("   3층 스테이지", "   Floor 3 Stages"), levelNames, () => ModSettings.DifficultyFloor3Level, value => ModSettings.DifficultyFloor3Level = value, null, levelColors));
            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyBossMadArmadillo", ModText.T("      에르마", "      Erma"), levelNames, () => ModSettings.MadArmadilloBossChallengeLevel, value => ModSettings.MadArmadilloBossChallengeLevel = value, null, levelColors));
            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyBossLibraryGuard", ModText.T("      팬택시스", "      Pentaxis"), levelNames, () => ModSettings.LibraryGuardBossChallengeLevel, value => ModSettings.LibraryGuardBossChallengeLevel = value, null, levelColors));
            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyFloor4", ModText.T("   4층 스테이지", "   Floor 4 Stages"), levelNames, () => ModSettings.DifficultyFloor4Level, value => ModSettings.DifficultyFloor4Level = value, null, levelColors));
            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyBossAskard", ModText.T("      아스카드", "      Askard"), levelNames, () => ModSettings.AskardBossChallengeLevel, value => ModSettings.AskardBossChallengeLevel = value, null, levelColors));
            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyFloor5", ModText.T("   5층 스테이지", "   Floor 5 Stages"), levelNames, () => ModSettings.DifficultyFloor5Level, value => ModSettings.DifficultyFloor5Level = value, null, levelColors));
            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyBossBirdDemon", ModText.T("      코베스", "      Kovess"), levelNames, () => ModSettings.BirdDemonBossChallengeLevel, value => ModSettings.BirdDemonBossChallengeLevel = value, null, levelColors));
            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyBossLizardDemon", ModText.T("      크라즈", "      Kraz"), levelNames, () => ModSettings.LizardDemonBossChallengeLevel, value => ModSettings.LizardDemonBossChallengeLevel = value, null, levelColors));
            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyBossQBoss", ModText.T("      클리포트", "      Qliphoth"), levelNames, () => ModSettings.QBossChallengeLevel, value => ModSettings.QBossChallengeLevel = value, null, levelColors));
            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyFloor6", ModText.T("   6층 스테이지", "   Floor 6 Stages"), levelNames, () => ModSettings.DifficultyFloor6Level, value => ModSettings.DifficultyFloor6Level = value, null, levelColors));
            DifficultyBossRows.Add(ExpandedOptionRows.AddChoice(IntegerTemplate, RowParent, "DifficultyBossQQBoss", ModText.T("      클리포트", "      Qliphoth"), levelNames, () => ModSettings.QQBossChallengeLevel, value => ModSettings.QQBossChallengeLevel = value, null, levelColors));

            ExpandedOptionRows.AddChoice(
                IntegerTemplate,
                RowParent,
                "MonsterScalerSpawnMode",
                ModText.T("몬스터 생성 배율", "Monster Spawn Multiplier"),
                new[] { ModText.T("전체 생성 배율", "Global Spawn"), ModText.T("층별 생성 배율", "Spawn per Floor"), ModText.T("종류별 생성 배율", "Spawn per Type") },
                () => ModSettings.MonsterScalerSpawnUsePerType ? 2 : (ModSettings.MonsterScalerSpawnUsePerFloor ? 1 : 0),
                value =>
                {
                    ModSettings.ResetMonsterScalerSpawnMultipliers();
                    ModSettings.MonsterScalerSpawnUsePerFloor = value == 1;
                    ModSettings.MonsterScalerSpawnUsePerType = value == 2;
                },
                RefreshConditionalRows,
                null,
                () => ModSettings.MonsterScalerSpawnUsePerType ? AnyRateChanged(ModSettings.MonsterScalerSpawnMultiplierByType)
                    : ModSettings.MonsterScalerSpawnUsePerFloor ? AnyRateChanged(ModSettings.MonsterScalerSpawnMultiplierByFloor)
                    : AnyRateChanged(ModSettings.MonsterScalerSpawnMultiplier)
            );

            SpawnGlobalRow = ExpandedOptionRows.AddSlider(
                SliderTemplate,
                RowParent,
                "MonsterScalerSpawnMultiplier",
                ModText.T("   전체", "   All"),
                () => ModSettings.MonsterScalerSpawnMultiplier,
                value => ModSettings.MonsterScalerSpawnMultiplier = value,
                1,
                19,
                RateLabel,
                10,
                dynamicMin: () => SpawnMinStep(0)
            );

            for (int i = 0; i < 6; i++)
            {
                int index = i;

                SpawnFloorRows.Add(
                    ExpandedOptionRows.AddSlider(
                        SliderTemplate,
                        RowParent,
                        "MonsterScalerSpawnMultiplierByFloor" + (i + 1),
                        ModText.T("   " + (i + 1) + "층", "   Floor " + (i + 1)),
                        () => ModSettings.MonsterScalerSpawnMultiplierByFloor[index],
                        value => ModSettings.MonsterScalerSpawnMultiplierByFloor[index] = value,
                        1,
                        19,
                        RateLabel,
                        10,
                        dynamicMin: () => SpawnMinStep(index + 1)
                    )
                );
            }

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "MonsterScalerMiniBossSpawnEnabled",
                ModText.T("미니보스 생성 배율 적용", "Apply Spawn Multiplier to Mini Bosses"),
                () => ModSettings.MonsterScalerMiniBossSpawnEnabled,
                value => ModSettings.MonsterScalerMiniBossSpawnEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "MonsterScalerSpawnExpBalanceEnabled",
                ModText.T("경험치, 잎 획득량을 배율만큼 감소", "Reduce EXP & Gold by Multiplier"),
                () => ModSettings.MonsterScalerSpawnExpBalanceEnabled,
                value => ModSettings.MonsterScalerSpawnExpBalanceEnabled = value
            );

            string[] typeNames = { ModText.T("일반 몬스터", "Normal Monsters"), ModText.T("미니보스", "Mini Bosses"), ModText.T("보스", "Bosses") };
            for (int i = 0; i < 3; i++) { int index = i; SpawnTypeRows.Add(ExpandedOptionRows.AddSlider(SliderTemplate, RowParent, "MonsterScalerSpawnType" + i, "   " + typeNames[i], () => ModSettings.MonsterScalerSpawnMultiplierByType[index], value => ModSettings.MonsterScalerSpawnMultiplierByType[index] = value, index == 1 ? ModSettings.MiniBossSpawnByTypeMin : index == 2 ? ModSettings.BossSpawnByTypeFixed : 1, index == 2 ? ModSettings.BossSpawnByTypeFixed : 19, RateLabel, 10, dynamicMin: () => index == 0 ? SpawnTypeMinStep() : 0)); }

            ExpandedOptionRows.AddChoice(
                IntegerTemplate,
                RowParent,
                "MonsterScalerHpMode",
                ModText.T("몬스터 체력 배율", "Monster HP Multiplier"),
                new[] { ModText.T("전체 체력 배율", "Global HP"), ModText.T("층별 체력 배율", "HP per Floor"), ModText.T("종류별 체력 배율", "HP per Type") },
                () => ModSettings.MonsterScalerHpUsePerType ? 2 : (ModSettings.MonsterScalerHpUsePerFloor ? 1 : 0),
                value =>
                {
                    ModSettings.ResetMonsterScalerHpMultipliers();
                    ModSettings.MonsterScalerHpUsePerFloor = value == 1;
                    ModSettings.MonsterScalerHpUsePerType = value == 2;
                },
                RefreshConditionalRows,
                null,
                () => ModSettings.MonsterScalerHpUsePerType ? AnyRateChanged(ModSettings.MonsterScalerHpMultiplierByType)
                    : ModSettings.MonsterScalerHpUsePerFloor ? AnyRateChanged(ModSettings.MonsterScalerHpMultiplierByFloor)
                    : AnyRateChanged(ModSettings.MonsterScalerHpMultiplier)
            );

            HpGlobalRow = ExpandedOptionRows.AddSlider(
                SliderTemplate,
                RowParent,
                "MonsterScalerHpMultiplier",
                ModText.T("   전체", "   All"),
                () => ModSettings.MonsterScalerHpMultiplier,
                value => ModSettings.MonsterScalerHpMultiplier = value,
                1,
                19,
                RateLabel,
                10,
                dynamicMin: () => HpMinStep(0)
            );

            for (int i = 0; i < 6; i++)
            {
                int index = i;

                HpFloorRows.Add(
                    ExpandedOptionRows.AddSlider(
                        SliderTemplate,
                        RowParent,
                        "MonsterScalerHpMultiplierByFloor" + (i + 1),
                        ModText.T("   " + (i + 1) + "층", "   Floor " + (i + 1)),
                        () => ModSettings.MonsterScalerHpMultiplierByFloor[index],
                        value => ModSettings.MonsterScalerHpMultiplierByFloor[index] = value,
                        1,
                        19,
                        RateLabel,
                        10,
                        dynamicMin: () => HpMinStep(index + 1)
                    )
                );
            }

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "BloodFestivalEnabled",
                ModText.T("피의 축제 완화", "Festival of Blood Mitigation"),
                () => ModSettings.BloodFestivalEnabled,
                value => ModSettings.BloodFestivalEnabled = value
            );

            for (int i = 0; i < 3; i++) { int index = i; HpTypeRows.Add(ExpandedOptionRows.AddSlider(SliderTemplate, RowParent, "MonsterScalerHpType" + i, "   " + typeNames[i], () => ModSettings.MonsterScalerHpMultiplierByType[index], value => ModSettings.MonsterScalerHpMultiplierByType[index] = value, 1, 19, RateLabel, 10, dynamicMin: () => HpTypeMinStep(index))); }

            ExpandedOptionRows.AddChoice(
                IntegerTemplate,
                RowParent,
                "MonsterScalerDamageMode",
                ModText.T("몬스터 데미지 배율", "Monster Damage Multiplier"),
                new[] { ModText.T("전체 데미지 배율", "Global Damage"), ModText.T("층별 데미지 배율", "Damage per Floor"), ModText.T("종류별 데미지 배율", "Damage per Type") },
                () => ModSettings.MonsterScalerDamageUsePerType ? 2 : (ModSettings.MonsterScalerDamageUsePerFloor ? 1 : 0),
                value =>
                {
                    ModSettings.ResetMonsterScalerDamageMultipliers();
                    ModSettings.MonsterScalerDamageUsePerFloor = value == 1;
                    ModSettings.MonsterScalerDamageUsePerType = value == 2;
                },
                RefreshConditionalRows,
                null,
                () => ModSettings.MonsterScalerDamageUsePerType ? AnyRateChanged(ModSettings.MonsterScalerNormalDamageMultiplier, ModSettings.MonsterScalerMiniBossDamageMultiplier, ModSettings.MonsterScalerBossDamageMultiplier)
                    : ModSettings.MonsterScalerDamageUsePerFloor ? AnyRateChanged(ModSettings.MonsterScalerDamageMultiplierByFloor)
                    : AnyRateChanged(ModSettings.MonsterScalerDamageMultiplier)
            );

            DamageGlobalRow = ExpandedOptionRows.AddSlider(
                SliderTemplate, RowParent, "MonsterScalerDamageMultiplier", ModText.T("   전체", "   All"),
                () => ModSettings.MonsterScalerDamageMultiplier,
                value => ModSettings.MonsterScalerDamageMultiplier = value, 1, 19, RateLabel, 10
            );

            DamageTypeRows.Add(ExpandedOptionRows.AddSlider(
                SliderTemplate, RowParent, "MonsterScalerNormalDamageMultiplier", ModText.T("   일반 몬스터", "   Normal Monsters"),
                () => ModSettings.MonsterScalerNormalDamageMultiplier,
                value => ModSettings.MonsterScalerNormalDamageMultiplier = value, 1, 19, RateLabel, 10
            ));
            DamageTypeRows.Add(ExpandedOptionRows.AddSlider(
                SliderTemplate, RowParent, "MonsterScalerMiniBossDamageMultiplier", ModText.T("   미니보스", "   Mini Bosses"),
                () => ModSettings.MonsterScalerMiniBossDamageMultiplier,
                value => ModSettings.MonsterScalerMiniBossDamageMultiplier = value, 1, 19, RateLabel, 10
            ));
            DamageTypeRows.Add(ExpandedOptionRows.AddSlider(
                SliderTemplate, RowParent, "MonsterScalerBossDamageMultiplier", ModText.T("   보스", "   Bosses"),
                () => ModSettings.MonsterScalerBossDamageMultiplier,
                value => ModSettings.MonsterScalerBossDamageMultiplier = value, 1, 19, RateLabel, 10, dynamicMin: BossDamageMinStep
            ));
            for (int i = 0; i < 6; i++) { int index = i; DamageFloorRows.Add(ExpandedOptionRows.AddSlider(SliderTemplate, RowParent, "MonsterScalerDamageFloor" + (i + 1), ModText.T("   " + (i + 1) + "층", "   Floor " + (i + 1)), () => ModSettings.MonsterScalerDamageMultiplierByFloor[index], value => ModSettings.MonsterScalerDamageMultiplierByFloor[index] = value, 1, 19, RateLabel, 10)); }

            ExpandedOptionRows.AddChoice(
                IntegerTemplate,
                RowParent,
                "MonsterScalerSpeedMode",
                ModText.T("몬스터 속도 배율", "Monster Speed Multiplier"),
                new[] { ModText.T("전체 속도 배율", "Global Speed"), ModText.T("층별 속도 배율", "Speed per Floor"), ModText.T("종류별 속도 배율", "Speed per Type") },
                () => ModSettings.MonsterScalerSpeedUsePerType ? 2 : (ModSettings.MonsterScalerSpeedUsePerFloor ? 1 : 0),
                value =>
                {
                    ModSettings.ResetMonsterScalerSpeedMultipliers();
                    ModSettings.MonsterScalerSpeedUsePerFloor = value == 1;
                    ModSettings.MonsterScalerSpeedUsePerType = value == 2;
                },
                RefreshConditionalRows,
                null,
                () => ModSettings.MonsterScalerSpeedUsePerType ? AnyRateChanged(ModSettings.MonsterScalerSpeedMultiplierByType)
                    : ModSettings.MonsterScalerSpeedUsePerFloor ? AnyRateChanged(ModSettings.MonsterScalerSpeedMultiplierByFloor)
                    : AnyRateChanged(ModSettings.MonsterScalerSpeedMultiplier)
            );

            SpeedGlobalRow = ExpandedOptionRows.AddSlider(
                SliderTemplate, RowParent, "MonsterScalerSpeedMultiplier", ModText.T("   전체", "   All"),
                () => ModSettings.MonsterScalerSpeedMultiplier,
                value => ModSettings.MonsterScalerSpeedMultiplier = value, 1, 19, RateLabel, 10
            );

            for (int i = 0; i < 6; i++) { int index = i; SpeedFloorRows.Add(ExpandedOptionRows.AddSlider(SliderTemplate, RowParent, "MonsterScalerSpeedFloor" + (i + 1), ModText.T("   " + (i + 1) + "층", "   Floor " + (i + 1)), () => ModSettings.MonsterScalerSpeedMultiplierByFloor[index], value => ModSettings.MonsterScalerSpeedMultiplierByFloor[index] = value, 1, 19, RateLabel, 10)); }
            for (int i = 0; i < 3; i++) { int index = i; SpeedTypeRows.Add(ExpandedOptionRows.AddSlider(SliderTemplate, RowParent, "MonsterScalerSpeedType" + i, "   " + typeNames[i], () => ModSettings.MonsterScalerSpeedMultiplierByType[index], value => ModSettings.MonsterScalerSpeedMultiplierByType[index] = value, 1, 19, RateLabel, 10, dynamicMin: () => index < 2 ? SpeedTypeMinStep() : 0)); }

            CutsceneSkipRow = ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "CutsceneSkipEnabled",
                ModText.T("클리포트 연출 스킵", "Skip Qliphoth Cutscenes"),
                () => ModSettings.CutsceneSkipEnabled,
                value => ModSettings.CutsceneSkipEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "WhichIsThisModFromEnabled",
                ModText.T("모드 아이템 툴팁 추가", "Mod Item Tooltip"),
                () => ModSettings.WhichIsThisModFromEnabled,
                value => ModSettings.WhichIsThisModFromEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "FPSMeterEnabled",
                ModText.T("FPS 표시", "Show FPS"),
                () => ModSettings.FPSMeterEnabled,
                value => ModSettings.FPSMeterEnabled = value
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "MoreDummyHpEnabled",
                ModText.T("허수아비 체력 증가", "Scarecrow HP Boost"),
                () => ModSettings.MoreDummyHpEnabled,
                value =>
                {
                    ModSettings.MoreDummyHpEnabled = value;
                    MoreDummyHp.Refresh();
                }
            );

            ExpandedOptionRows.AddChoice(
                IntegerTemplate,
                RowParent,
                "Language",
                ModText.T("언어", "Language"),
                new[] { ModText.T("한글", "Korean"), ModText.T("영어", "English") },
                () => ModSettings.Language,
                value => ModSettings.Language = value,
                RequestLanguageRefresh,
                null,
                () => true
            );

            ExpandedOptionRows.AddBool(
                ToggleTemplate,
                RowParent,
                "UpdateHistory",
                ModText.T("업데이트 내역", "Update History"),
                () => false,
                value =>
                {
                    if (value) AutoUpdate.OpenHistory();
                },
                ExpandedOptionRows.RefreshValues
            );

            ExpandedOptionRows.AddHorayButton(
                HorayButtonTemplate,
                RowParent,
                "ResetAllSettings",
                ModText.T("모든 설정 초기화", "Reset All Settings"),
                ResetAllSettings
            );

            Color versionColor = Color.white;
            try
            {
                versionColor = ItemDatabase.GetColorViaItemRarity(EItemRarity.Eternal);
            }
            catch
            {
            }

            ExpandedOptionRows.AddFooterHeader(
                HeaderTemplate,
                RowParent,
                "ModVersion",
                ModText.T("─ 모드 버전 : ", "─ Mod Version : ") + ModInfo.Version + " ─",
                versionColor
            );

            UpdateInstalledRow = ExpandedOptionRows.AddFooterHeader(
                HeaderTemplate,
                RowParent,
                "UpdateInstalled",
                string.Empty,
                UpdateInstalledColor
            );

            ExpandedOptionRows.MoveRowsAfter("MonsterScalerSpawnMode", SpawnTypeRows.ToArray());
            ExpandedOptionRows.MoveRowsAfter("MonsterScalerHpMode", HpTypeRows.ToArray());
            ExpandedOptionRows.MoveRowsAfter("MonsterScalerDamageMode", DamageTypeRows.ToArray());
            ExpandedOptionRows.MoveRowsAfter("MonsterScalerSpeedMode", SpeedTypeRows.ToArray());
            ExpandedOptionRows.ApplySectionOrder();
            RefreshConditionalRows();
        }

        private sealed class TabRowRecord
        {
            internal GameObject Row;
            internal int Tab;
            internal int Group;
            internal int Index;
            internal int Order;
        }

        private const int GroupBlankCount = 3;

        private static readonly string[][] TabHeadersKorean =
        {
            new[] { "자신에게만 적용됩니다.", "게스트로도 사용 가능합니다. (미니맵 이동 제외)" },
            new[] { "호스트 전용 옵션입니다.", "모드가 없는 게스트도 적용됩니다." },
            new[] { "호스트 전용 옵션입니다.", "몬스터의 각종 배율을 조절합니다." },
            new[] { "호스트 전용 옵션입니다.", "모드가 없는 게스트도 적용됩니다." },
            new string[0]
        };

        private static readonly string[][] TabHeadersEnglish =
        {
            new[] { "Applies only to you.", "Also works as a guest. (Except minimap travel)" },
            new[] { "Host-only options.", "Also applies to guests without the mod." },
            new[] { "Host-only options.", "Adjusts monster multipliers." },
            new[] { "Host-only options.", "Also applies to guests without the mod." },
            new string[0]
        };

        private static string[][] TabHeaders
        {
            get { return ModText.IsEnglish ? TabHeadersEnglish : TabHeadersKorean; }
        }

        private static readonly string[][] LocalGroups =
        {
            new[] { "ChatConvenienceEnabled", "ChatTimeMode", "ShopFavoriteEnabled", "EnchantMarkEnabled", "LevelUpItemCount", "FPSMeterEnabled", "WhichIsThisModFromEnabled" },
            new[] { "AnytimeOpenInventoryEnabled", "BossHpTextEnabled", "BossAggroEnabled", "RightClickHoldEnabled", "CursorOutlineEnabled", "CursorOutlineColor" },
            new[] { "AlwaysRunEnabled", "HiddenRoomFinderEnabled", "MinimapOpenEnabled", "MinimapOpenTeleportEnabled" },
            new[] { "MorePresetEnabled", "FountainArtifactUnlockEnabled" },
            new[] { "JournalWeaponOutlineMode", "WeaponShineEnabled", "CostumeFrameWatcherEnabled" },
            new[] { "DamageMeterEnabled", "DamageMeterKey" },
            new[] { "NameColorStart", "NameColorA", "NameColorB" },
            new[] { "GuestReturnToTownEnabled", "PingSoundMode" }
        };

        private static readonly string[][] MultiGroups =
        {
            new[] { "MoreInventoryEnabled", "MoreInventoryLevelInterval", "MoreInventorySlots", "PlayerLevelCapEnabled", "InstantLeafExp", "SellStartItem" },
            new[] { "Multiplayer16", "TradeMoneyShareEnabled", "PotionShareEnabled", "MoreMultiplayerInfoEnabled", "WalletShareEnabled", "WalletShareLeaf", "WalletShareDice", "PingIgnoreEnabled", "FriendlyFireEnabled", "FriendlyFireMultiplier", "KillLogEnabled" },
            new[] { "FountainCapacityEnabled", "FountainCapacityBonus", "FountainArtifactShare" },
            new[] { "FirstArtifactLegendary", "EnchantTableEnabled", "FirstAnvilEnabled", "MysteryPotGuaranteed", "InventorySell" },
            new[] { "MoreMapEnabled", "RandomEventMultiplier", "CutsceneSkipEnabled" },
            new[] { "MoreDummyHpEnabled" }
        };

        private static void FindGroup(string[][] groups, string key, out int group, out int index)
        {
            for (int g = 0; g < groups.Length; g++)
            {
                int found = Array.IndexOf(groups[g], key);
                if (found < 0) continue;

                group = g;
                index = found;
                return;
            }

            group = groups.Length;
            index = 0;
        }

        private static int ScalerGroup(string key)
        {
            if (key.Contains("Spawn")) return 0;
            if (key.Contains("Hp") || key == "BloodFestivalEnabled") return 1;
            if (key.Contains("Damage")) return 2;
            return 3;
        }

        private static void GetRowPlacement(int tab, string key, out int group, out int index)
        {
            group = 0;
            index = 0;

            if (tab == 0) FindGroup(LocalGroups, key, out group, out index);
            else if (tab == 1) FindGroup(MultiGroups, key, out group, out index);
            else if (tab == 2) group = ScalerGroup(key);
        }

        private static void DistributeRowsToTabs()
        {
            List<GameObject> snapshot = ExpandedOptionRows.SnapshotRows();
            var records = new List<TabRowRecord>();
            var headers = new List<GameObject>();

            foreach (GameObject row in snapshot)
            {
                if (row == null) continue;

                if (row.name.StartsWith("SephiriaExpanded_Header_", StringComparison.Ordinal))
                {
                    headers.Add(row);
                    continue;
                }

                int tab = GetRowTab(row.name);
                int group;
                int index;
                GetRowPlacement(tab, RowKey(row.name), out group, out index);

                records.Add(new TabRowRecord
                {
                    Row = row,
                    Tab = tab,
                    Group = group,
                    Index = index,
                    Order = records.Count
                });
            }

            foreach (GameObject header in headers)
                ExpandedOptionRows.RemoveRow(header);

            for (int i = 0; i < TabRows.Length; i++)
                TabRows[i].Clear();

            for (int tab = 0; tab < TabRows.Length; tab++)
            {
                Transform targetParent = GetTabRowParent(tab);
                if (targetParent == null)
                    targetParent = RowParent;

                foreach (string headerLine in TabHeaders[tab])
                    TabRows[tab].Add(ExpandedOptionRows.AddHeader(HeaderTemplate, targetParent, headerLine));

                int currentTab = tab;
                List<TabRowRecord> tabRecords = records
                    .Where(record => record.Tab == currentTab)
                    .OrderBy(record => record.Group)
                    .ThenBy(record => record.Index)
                    .ThenBy(record => record.Order)
                    .ToList();

                for (int i = 0; i < tabRecords.Count; i++)
                {
                    if (i > 0 && tabRecords[i].Group != tabRecords[i - 1].Group)
                    {
                        for (int blank = 0; blank < GroupBlankCount; blank++)
                            TabRows[tab].Add(ExpandedOptionRows.AddBlank(BlankTemplate, targetParent));
                    }

                    tabRecords[i].Row.transform.SetParent(targetParent, false);
                    TabRows[tab].Add(tabRecords[i].Row);
                }
            }

            ShowTab(0);
        }

        private static void ReorderActiveTabRows()
        {
            List<GameObject> rows = TabRows[ActiveTab];
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null)
                    rows[i].transform.SetSiblingIndex(i);
            }
        }

        private static void HandleTabSelected(int tab)
        {
            if (tab < 0 || tab >= TabRows.Length)
                return;

            ShowTab(tab);
        }

        private static bool IsRowAvailable(GameObject row)
        {
            return true;
        }

        private static void ShowTab(int tab)
        {
            ActiveTab = Mathf.Clamp(tab, 0, TabRows.Length - 1);

            for (int i = 0; i < TabRows.Length; i++)
            {
                bool visible = i == ActiveTab;
                foreach (GameObject row in TabRows[i])
                {
                    if (row != null)
                        row.SetActive(visible && IsRowAvailable(row));
                }

                if (TabContents[i] != null)
                {
                    if (visible)
                        TabContents[i].OpenTab();
                    else
                        TabContents[i].CloseTab();
                }
            }

            RefreshConditionalRows();
            SyncTabHighlight();
            ScrollRect activeScroll = GetTabScroll(ActiveTab);
            if (activeScroll != null)
            {
                activeScroll.StopMovement();
                activeScroll.verticalNormalizedPosition = 1f;
            }

            SelectFirstRow();
        }

        private static void SyncTabHighlight()
        {
            if (Tab == null || Tab.tabButtons == null)
                return;

            for (int i = 0; i < Tab.tabButtons.Length; i++)
            {
                UI_TabButton button = Tab.tabButtons[i];
                if (button != null)
                    button.SetTabButtonSprite(i == ActiveTab);
            }
        }

        private static readonly string[] RowKeyPrefixes =
        {
            "SephiriaExpanded_Bool_",
            "SephiriaExpanded_Slider_",
            "SephiriaExpanded_Integer_",
            "SephiriaExpanded_Choice_",
            "SephiriaExpanded_Button_",
            "SephiriaExpanded_Rebind_"
        };

        private static string RowKey(string name)
        {
            foreach (string prefix in RowKeyPrefixes)
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                    return name.Substring(prefix.Length);

            return name;
        }

        private static int GetRowTab(string name)
        {
            string key = RowKey(name);

            if (key == "Language" || key == "ResetAllSettings" || key == "UpdateHistory" || name.StartsWith("SephiriaExpanded_FooterHeader_", StringComparison.Ordinal))
                return 4;

            if (key.StartsWith("Difficulty", StringComparison.Ordinal))
                return 3;

            if (key.StartsWith("MonsterScaler", StringComparison.Ordinal) || key == "BloodFestivalEnabled")
                return 2;

            switch (key)
            {
                case "ChatConvenienceEnabled":
                case "ChatTimeMode":
                case "ShopFavoriteEnabled":
                case "EnchantMarkEnabled":
                case "JournalWeaponOutlineMode":
                case "WeaponShineEnabled":
                case "MorePresetEnabled":
                case "RightClickHoldEnabled":
                case "CursorOutlineEnabled":
                case "CursorOutlineColor":
                case "AlwaysRunEnabled":
                case "HiddenRoomFinderEnabled":
                case "DamageMeterEnabled":
                case "DamageMeterKey":
                case "NameColorStart":
                case "NameColorA":
                case "NameColorB":
                case "GuestReturnToTownEnabled":
                case "PingSoundMode":
                case "LevelUpItemCount":
                case "CostumeFrameWatcherEnabled":
                case "AnytimeOpenInventoryEnabled":
                case "BossHpTextEnabled":
                case "BossAggroEnabled":
                case "MinimapOpenEnabled":
                case "MinimapOpenTeleportEnabled":
                case "FountainArtifactUnlockEnabled":
                case "FPSMeterEnabled":
                case "WhichIsThisModFromEnabled":
                    return 0;
                default:
                    return 1;
            }
        }

        private static void ResetAllSettings()
        {
            MoreInventory.RemoveGrantedStorage();
            ModSettings.ResetAll();
            NameColor.Refresh();
            MoreDummyHp.Refresh();
            MinimapOpen.Cleanup();
            FountainCapacity.Refresh();
            CostumeFrameWatcher.ForceRefresh();
            ExpandedOptionRows.RefreshValues();
            ExpandedOptionRows.MoveRowsAfter("MonsterScalerSpawnMode", SpawnTypeRows.ToArray());
            ExpandedOptionRows.MoveRowsAfter("MonsterScalerHpMode", HpTypeRows.ToArray());
            ExpandedOptionRows.MoveRowsAfter("MonsterScalerDamageMode", DamageTypeRows.ToArray());
            ExpandedOptionRows.MoveRowsAfter("MonsterScalerSpeedMode", SpeedTypeRows.ToArray());
            ExpandedOptionRows.ApplySectionOrder();
            ShowTab(ActiveTab);
        }

        [HarmonyPatch(typeof(UI_HorayButton), "OnSubmit")]
        private static class ResetButtonPatch
        {
            private static bool Prefix(UI_HorayButton __instance)
            {
                if (__instance == null || __instance.gameObject == null)
                    return true;

                Transform root = __instance.transform;
                bool isResetButton = false;
                for (int i = 0; i < 8 && root != null; i++, root = root.parent)
                {
                    if (root.gameObject.name == "SephiriaExpanded_Button_ResetAllSettings")
                    {
                        isResetButton = true;
                        break;
                    }
                }

                if (!isResetButton)
                    return true;

                ResetAllSettings();
                return false;
            }
        }

        private static bool AnyRateChanged(params int[] values)
        {
            foreach (int value in values)
                if (value != ModSettings.RateDefault)
                    return true;

            return false;
        }

        private static int MinStep(float rate)
        {
            if (rate <= 1f) return 0;

            int best = 0;
            for (int step = 1; step <= 19; step++)
                if (ModSettings.StepToRate(step) <= rate + 0.001f) best = step;

            return best;
        }

        private static int HpMinStep(int floor)
        {
            return MinStep(Mathf.Min(Difficulty.RecommendedFloorRate(floor, Difficulty.HpRate), Difficulty.RecommendedBossRate(Difficulty.HpRate)));
        }

        private static int HpTypeMinStep(int index)
        {
            return MinStep(index == 2 ? Difficulty.RecommendedBossRate(Difficulty.HpRate) : Difficulty.RecommendedFloorRate(0, Difficulty.HpRate));
        }

        private static int SpawnMinStep(int floor)
        {
            return ModSettings.MonsterScalerMiniBossSpawnEnabled ? 0 : MinStep(Difficulty.RecommendedFloorRate(floor, Difficulty.SpawnRate));
        }

        private static int SpawnTypeMinStep()
        {
            return MinStep(Difficulty.RecommendedFloorRate(0, Difficulty.SpawnRate));
        }

        private static int SpeedTypeMinStep()
        {
            return MinStep(Difficulty.RecommendedFloorRate(0, Difficulty.SpeedRate));
        }

        private static int BossDamageMinStep()
        {
            return MinStep(Difficulty.RecommendedBossRate(BossChallengeRules.DamageRate));
        }

        private static string RateLabel(int step)
        {
            return "x" + (step <= 9 ? (step / 10f).ToString("0.0", CultureInfo.InvariantCulture) : (step - 9).ToString(CultureInfo.InvariantCulture));
        }

        private static GameObject UpdateInstalledRow;
        private static readonly Color UpdateInstalledColor = new Color32(0x7C, 0xE0, 0x8A, 0xFF);

        private static void RefreshUpdateInstalledRow()
        {
            if (UpdateInstalledRow == null) return;

            string tag = AutoUpdate.InstalledTag;
            ExpandedOptionRows.SetVisible(UpdateInstalledRow, tag != null);
            if (tag == null) return;

            string text = ModText.T("모드 버전 : " + tag + " 설치 완료. 재시작 시 적용됩니다.", "Mod Version : " + tag + " installed. Applied after restart.");
            foreach (TextMeshProUGUI label in UpdateInstalledRow.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (label != null) label.text = text;
            }
        }

        private static void RefreshConditionalRows()
        {
            RefreshUpdateInstalledRow();
            ExpandedOptionRows.SetVisible(DifficultyGlobalRow, ModSettings.DifficultyMode == Difficulty.ModeGlobal);
            ExpandedOptionRows.SetVisible(DifficultyBreakRow, ModSettings.DifficultyMode != 0);
            ExpandedOptionRows.SetVisible(DifficultyRecommendedRow, ModSettings.DifficultyMode != 0);
            foreach (GameObject row in DifficultyBossRows) ExpandedOptionRows.SetVisible(row, ModSettings.DifficultyMode == Difficulty.ModeDetail);

            ExpandedOptionRows.SetVisible(SpawnGlobalRow, !ModSettings.MonsterScalerSpawnUsePerFloor && !ModSettings.MonsterScalerSpawnUsePerType);

            for (int i = 0; i < SpawnFloorRows.Count; i++)
                ExpandedOptionRows.SetVisible(SpawnFloorRows[i], ModSettings.MonsterScalerSpawnUsePerFloor && !ModSettings.MonsterScalerSpawnUsePerType);

            ExpandedOptionRows.SetVisible(HpGlobalRow, !ModSettings.MonsterScalerHpUsePerFloor && !ModSettings.MonsterScalerHpUsePerType);

            for (int i = 0; i < HpFloorRows.Count; i++)
                ExpandedOptionRows.SetVisible(HpFloorRows[i], ModSettings.MonsterScalerHpUsePerFloor && !ModSettings.MonsterScalerHpUsePerType);
            ExpandedOptionRows.SetVisible(DamageGlobalRow, !ModSettings.MonsterScalerDamageUsePerType && !ModSettings.MonsterScalerDamageUsePerFloor);
            foreach (GameObject row in DamageTypeRows)
                ExpandedOptionRows.SetVisible(row, ModSettings.MonsterScalerDamageUsePerType);
            foreach (GameObject row in SpawnTypeRows) ExpandedOptionRows.SetVisible(row, ModSettings.MonsterScalerSpawnUsePerType);
            foreach (GameObject row in HpTypeRows) ExpandedOptionRows.SetVisible(row, ModSettings.MonsterScalerHpUsePerType);
            foreach (GameObject row in DamageFloorRows) ExpandedOptionRows.SetVisible(row, ModSettings.MonsterScalerDamageUsePerFloor);

            ExpandedOptionRows.SetVisible(SpeedGlobalRow, !ModSettings.MonsterScalerSpeedUsePerFloor && !ModSettings.MonsterScalerSpeedUsePerType);
            foreach (GameObject row in SpeedFloorRows) ExpandedOptionRows.SetVisible(row, ModSettings.MonsterScalerSpeedUsePerFloor && !ModSettings.MonsterScalerSpeedUsePerType);
            foreach (GameObject row in SpeedTypeRows) ExpandedOptionRows.SetVisible(row, ModSettings.MonsterScalerSpeedUsePerType);

            ExpandedOptionRows.SetVisible(FountainCapacityRow, ModSettings.FountainCapacityEnabled);

            ExpandedOptionRows.SetVisible(FriendlyFireMultiplierRow, ModSettings.FriendlyFireEnabled);
            ExpandedOptionRows.SetVisible(KillLogRow, ModSettings.FriendlyFireEnabled);
            ExpandedOptionRows.SetVisible(DamageMeterKeyRow, ModSettings.DamageMeterEnabled);
            foreach (GameObject row in NameColorExtraRows)
                ExpandedOptionRows.SetVisible(row, ModSettings.NameColorStart > 0);
            foreach (GameObject row in WalletShareRows)
                ExpandedOptionRows.SetVisible(row, ModSettings.WalletShareEnabled);
            ExpandedOptionRows.SetVisible(ChatTimeModeRow, ModSettings.ChatConvenienceEnabled);
            ExpandedOptionRows.SetVisible(CursorOutlineColorRow, ModSettings.CursorOutlineEnabled);
            ExpandedOptionRows.SetVisible(MinimapOpenTeleportRow, ModSettings.MinimapOpenEnabled);
            ExpandedOptionRows.SetVisible(MoreInventoryLevelIntervalRow, ModSettings.MoreInventoryEnabled);
            ExpandedOptionRows.SetVisible(MoreInventorySlotsRow, ModSettings.MoreInventoryEnabled);

            for (int i = 0; i < TabRows.Length; i++)
            {
                if (i == ActiveTab) continue;
                foreach (GameObject row in TabRows[i])
                    if (row != null)
                        row.SetActive(false);
            }

            if (TabRows[ActiveTab].Count > 0)
                ReorderActiveTabRows();

            RefreshLayout();
            SelectFirstRow();
        }

        private static void RefreshLayout()
        {
            Transform activeParent = GetTabRowParent(ActiveTab);
            if (activeParent == null) return;

            RectTransform rect = activeParent as RectTransform;

            if (rect != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(rect);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }

            UnityEngine.Canvas.ForceUpdateCanvases();

            ScrollRect activeScroll = GetTabScroll(ActiveTab);
            if (activeScroll != null)
            {
                if (activeScroll.content != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(activeScroll.content);

                activeScroll.Rebuild(CanvasUpdate.PostLayout);
            }

            UnityEngine.Canvas.ForceUpdateCanvases();
        }

        private static void SelectFirstRow()
        {
            if (!Ready) return;

            Transform activeParent = GetTabRowParent(ActiveTab);
            UI_TabContent activeContent = GetTabContent(ActiveTab);
            if (activeParent == null || activeContent == null) return;

            Selectable[] selectables = activeParent.GetComponentsInChildren<Selectable>(true);
            Selectable first = null;

            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable selectable = selectables[i];

                if (selectable == null || !selectable.interactable || !selectable.gameObject.activeInHierarchy)
                    continue;

                first = selectable;
                break;
            }

            activeContent.selectionOnOpened = first != null ? first.gameObject : null;
            Panel.defaultSelectable = activeContent.selectionOnOpened;
        }

        private static void EnsurePauseButton()
        {
            if (PauseButtonRoot != null && PausePanel != null && PauseButton != null && VanillaOptionButton != null)
                return;

            UI_PausePanel pause = null;

            try
            {
                if (UIManager.Instance != null)
                    pause = UIManager.Instance.GetElement<UI_PausePanel>();
            }
            catch
            {
            }

            if (pause == null) return;

            UI_LocalizationStringText optionLocalization =
                pause.GetComponentsInChildren<UI_LocalizationStringText>(true)
                    .FirstOrDefault(x => x != null &&
                                         x.valueString != null &&
                                         x.valueString.key == "UI_PausePanel_OptionsButton");

            Button optionButton =
                AncestorButton(
                    optionLocalization != null ? optionLocalization.transform : null,
                    pause.transform
                );

            if (optionButton == null || optionButton.transform.parent == null) return;

            RemoveNamedChild(optionButton.transform.parent, PauseButtonName);

            GameObject clone = null;

            try
            {
                clone = UnityEngine.Object.Instantiate(optionButton.gameObject, optionButton.transform.parent);
                clone.name = PauseButtonName;
                clone.SetActive(false);

                clone.transform.SetSiblingIndex(optionButton.transform.GetSiblingIndex() + 1);

                Button button = clone.GetComponent<Button>() ?? clone.GetComponentInChildren<Button>(true);

                if (button == null)
                    throw new InvalidOperationException("ESC 메뉴 버튼 복제 실패");

                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(new UnityAction(Open));

                SetPauseButtonText(clone, PauseButtonTitle(), EternalColor());

                PausePanel = pause;
                PauseButtonRoot = clone;
                PauseButton = button;
                VanillaOptionButton = optionButton;

                clone.SetActive(true);

                CreateDevPauseButton(optionButton);

                Debug.Log("[Sephiria Expanded] ESC 설정 버튼 생성 완료");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                if (clone != null)
                {
                    clone.SetActive(false);
                    UnityEngine.Object.Destroy(clone);
                }

                PausePanel = null;
                PauseButtonRoot = null;
                PauseButton = null;
                VanillaOptionButton = null;
            }
        }

        private static string PauseButtonTitle()
        {
            return "SE " + ModInfo.Version + ModText.T(" 설정", " Settings");
        }

        private static string DevPauseButtonTitle()
        {
            return "SE " + ModInfo.Version + ModText.T(" 개발자 설정", " Developer Settings");
        }

        private static bool LanguageRefreshPending;

        private static void RequestLanguageRefresh()
        {
            LanguageRefreshPending = true;
        }

        private static void ApplyLanguageRefresh()
        {
            LanguageRefreshPending = false;
            ModSettings.Save();
            DamageMeter.RefreshLanguage();

            if (PauseButtonRoot != null) SetPauseButtonText(PauseButtonRoot, PauseButtonTitle(), EternalColor());
            if (DevPauseButtonRoot != null) SetPauseButtonText(DevPauseButtonRoot, DevPauseButtonTitle(), DevPauseButtonColor);

            bool reopen = Panel != null && Panel.IsOpened;
            int tab = ActiveTab;
            CleanupPanelOnly();
            TryCreatePanel();
            if (!reopen || !Ready) return;

            OpenPanel(false);
            ShowTab(tab);
        }

        private static Color EternalColor()
        {
            try
            {
                return ItemDatabase.GetColorViaItemRarity(EItemRarity.Eternal);
            }
            catch
            {
                return Color.white;
            }
        }

        private static void CreateDevPauseButton(Button optionButton)
        {
            RemoveNamedChild(optionButton.transform.parent, DevPauseButtonName);

            GameObject clone = UnityEngine.Object.Instantiate(optionButton.gameObject, optionButton.transform.parent);
            clone.name = DevPauseButtonName;
            clone.SetActive(false);
            clone.transform.SetSiblingIndex(PauseButtonRoot.transform.GetSiblingIndex() + 1);

            Button button = clone.GetComponent<Button>() ?? clone.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                UnityEngine.Object.Destroy(clone);
                return;
            }

            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(new UnityAction(OpenDevPanel));
            SetPauseButtonText(clone, DevPauseButtonTitle(), DevPauseButtonColor);

            DevPauseButtonRoot = clone;
            clone.SetActive(DeveloperMode.Activated);
        }

        private static void OpenDevPanel()
        {
            DevPanel.Open();
            FMODUnity.RuntimeManager.PlayOneShot(OpenSoundEvent);
        }

        private static void SyncDevPauseButton()
        {
            if (DevPauseButtonRoot != null && DevPauseButtonRoot.activeSelf != DeveloperMode.Activated)
                DevPauseButtonRoot.SetActive(DeveloperMode.Activated);
        }

        private static void SetPauseButtonText(GameObject root, string title, Color color)
        {
            UI_LocalizationStringText localization =
                root.GetComponentsInChildren<UI_LocalizationStringText>(true).FirstOrDefault();

            if (localization != null && localization.text != null)
            {
                localization.enabled = false;
                localization.text.text = title;
                localization.text.color = color;
                return;
            }

            TextMeshProUGUI text = root.GetComponentInChildren<TextMeshProUGUI>(true);

            if (text != null)
            {
                text.text = title;
                text.color = color;
            }
        }

        private static void SetTabTitle(UI_TabButton button, string title)
        {
            if (button == null) return;

            CanvasGroup group = button.GetComponent<CanvasGroup>();

            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }

            UI_LocalizationStringText localization =
                button.GetComponentsInChildren<UI_LocalizationStringText>(true).FirstOrDefault();

            if (localization != null && localization.text != null)
            {
                localization.enabled = false;
                localization.text.text = title;
                return;
            }

            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);

            if (text != null)
                text.text = title;
        }

        internal static void Open()
        {
            OpenPanel(true);
        }

        private static void OpenPanel(bool playSound)
        {
            if (!Ready)
            {
                TryCreatePanel();

                if (!Ready)
                {
                    Debug.LogError("[Sephiria Expanded] 설정 패널을 열 수 없습니다.");
                    return;
                }
            }

            if (Panel.IsOpened) return;

            RefreshConditionalRows();
            SelectFirstRow();

            try
            {
                Panel.Open();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return;
            }

            if (playSound) FMODUnity.RuntimeManager.PlayOneShot(OpenSoundEvent);
            RefreshLayout();

            if (Scroll != null)
            {
                UnityEngine.Canvas.ForceUpdateCanvases();
                Scroll.StopMovement();
                Scroll.verticalNormalizedPosition = 1f;
            }
        }

        private static void HandleReturnToPause()
        {
            bool opened = Panel != null && Panel.IsOpened;

            if (WasOpened && !opened)
            {
                PendingPauseSelection = true;
                ModSettings.Save();
            }

            WasOpened = opened;

            if (!PendingPauseSelection ||
                PausePanel == null ||
                !PausePanel.IsOpened ||
                !PausePanel.IsControlEnabled ||
                PauseButton == null)
            {
                return;
            }

            PendingPauseSelection = false;

            try
            {
                PausePanel.DoControlSelection(PauseButton.gameObject);
            }
            catch
            {
            }
        }

        private static bool IsIntegerTemplate(UI_OptionBox_Common_Integer row)
        {
            return row != null &&
                   row.box != null &&
                   row.valueText != null &&
                   row.transform.parent != null &&
                   !row.gameObject.name.StartsWith("SephiriaExpanded_", StringComparison.Ordinal);
        }

        private static Transform FindRowParent(UI_TabContent content)
        {
            if (content == null) return null;

            UI_OptionBox_Common_Integer integer = content.GetComponentsInChildren<UI_OptionBox_Common_Integer>(true)
                .FirstOrDefault(IsIntegerTemplate);

            if (integer != null && integer.transform.parent != null)
                return integer.transform.parent;

            ScrollRect scroll = FindScroll(content, null);
            return scroll != null && scroll.content != null ? scroll.content : content.transform;
        }

        private static UI_TabContent GetTabContent(int tab)
        {
            if (tab >= 0 && tab < TabContents.Length && TabContents[tab] != null)
                return TabContents[tab];
            return Content;
        }

        private static Transform GetTabRowParent(int tab)
        {
            if (tab >= 0 && tab < TabRowParents.Length && TabRowParents[tab] != null)
                return TabRowParents[tab];
            return RowParent;
        }

        private static ScrollRect GetTabScroll(int tab)
        {
            if (tab >= 0 && tab < TabScrolls.Length && TabScrolls[tab] != null)
                return TabScrolls[tab];
            return Scroll;
        }

        private static ScrollRect FindScroll(UI_TabContent content, Transform parent)
        {
            if (content == null) return null;

            ScrollRect[] values = content.GetComponentsInChildren<ScrollRect>(true);

            for (int i = 0; i < values.Length; i++)
            {
                ScrollRect candidate = values[i];

                if (candidate == null || candidate.content == null) continue;

                if (parent != null && (parent == candidate.content || parent.IsChildOf(candidate.content)))
                    return candidate;
            }

            return values.FirstOrDefault(x => x != null && x.content != null);
        }

        private static void HideVanillaRows(UI_TabContent content, Transform parent)
        {
            if (parent == null) return;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                if (child == null) continue;

                if (child.name.StartsWith("SephiriaExpanded_", StringComparison.Ordinal))
                    continue;

                child.gameObject.SetActive(false);
            }

            if (content == null) return;

            UI_FontBoxActivator[] activators =
                content.GetComponentsInChildren<UI_FontBoxActivator>(true);

            for (int i = 0; i < activators.Length; i++)
            {
                if (activators[i] != null)
                    activators[i].enabled = false;
            }

            UI_FontBox[] fonts =
                content.GetComponentsInChildren<UI_FontBox>(true);

            for (int i = 0; i < fonts.Length; i++)
            {
                if (fonts[i] == null) continue;

                fonts[i].enabled = false;
                fonts[i].gameObject.SetActive(false);
            }
        }

        private static void RewireCloseButton()
        {
            if (Panel == null) return;

            UI_LocalizationStringText closeLocalization =
                Panel.GetComponentsInChildren<UI_LocalizationStringText>(true)
                    .FirstOrDefault(x => x != null &&
                                         x.valueString != null &&
                                         x.valueString.key == "UI_Close" &&
                                         !IsInsideContent(x.transform));

            Button closeButton =
                AncestorButton(
                    closeLocalization != null ? closeLocalization.transform : null,
                    Panel.transform
                );

            if (closeButton == null)
            {
                Button[] buttons = Panel.GetComponentsInChildren<Button>(true);

                for (int i = 0; i < buttons.Length; i++)
                {
                    Button candidate = buttons[i];

                    if (candidate == null || IsInsideContent(candidate.transform))
                        continue;

                    string lower = candidate.name.ToLowerInvariant();

                    if (lower.Contains("close") || lower.Contains("back"))
                    {
                        closeButton = candidate;
                        break;
                    }
                }
            }

            if (closeButton == null) return;

            closeButton.onClick = new Button.ButtonClickedEvent();

            closeButton.onClick.AddListener(delegate
            {
                ModSettings.Save();
                Panel.Close();
            });
        }

        private static bool IsInsideContent(Transform transform)
        {
            if (transform == null) return false;

            for (int i = 0; i < TabContents.Length; i++)
            {
                if (TabContents[i] != null && transform.IsChildOf(TabContents[i].transform))
                    return true;
            }

            return Content != null && transform.IsChildOf(Content.transform);
        }

        private static Button AncestorButton(Transform current, Transform stop)
        {
            Transform value = current;

            while (value != null)
            {
                Button button = value.GetComponent<Button>();

                if (button != null)
                    return button;

                if (value == stop)
                    break;

                value = value.parent;
            }

            return null;
        }

        private static void RemoveNamedChild(Transform parent, string name)
        {
            if (parent == null) return;

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);

                if (child == null || !string.Equals(child.name, name, StringComparison.Ordinal))
                    continue;

                if (child.gameObject == PanelRoot || child.gameObject == PauseButtonRoot)
                    continue;

                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private static void CleanupPanelOnly()
        {
            ExpandedOptionRows.Clear();

            SpawnFloorRows.Clear();
            HpFloorRows.Clear();
            DamageTypeRows.Clear();
            SpawnTypeRows.Clear(); HpTypeRows.Clear(); DamageFloorRows.Clear();
            SpeedFloorRows.Clear(); SpeedTypeRows.Clear(); DifficultyBossRows.Clear();

            DifficultyGlobalRow = null; DifficultyBreakRow = null; DifficultyRecommendedRow = null;
            SpawnGlobalRow = null;
            HpGlobalRow = null;
            SpeedGlobalRow = null;
            DamageGlobalRow = null;
            FountainCapacityRow = null;
            MinimapOpenTeleportRow = null;
            MoreInventoryLevelIntervalRow = null;
            MoreInventorySlotsRow = null;
            MoreMapRow = null;
            MoreMapInteractable = null;
            CutsceneSkipRow = null;
            CutsceneSkipInteractable = null;
            ChatTimeModeRow = null;
            CursorOutlineColorRow = null;
            FriendlyFireMultiplierRow = null;
            KillLogRow = null;
            DamageMeterKeyRow = null;
            NameColorExtraRows.Clear();
            WalletShareRows.Clear();

            for (int i = 0; i < TabRows.Length; i++)
                TabRows[i].Clear();
            ActiveTab = 0;

            if (Panel != null && Panel.IsOpened)
            {
                try
                {
                    Panel.Close();
                }
                catch
                {
                }
            }

            if (PanelRoot != null)
            {
                PanelRoot.SetActive(false);
                UnityEngine.Object.Destroy(PanelRoot);
            }

            SourcePanel = null;
            Panel = null;
            PanelRoot = null;
            Tab = null;
            Content = null;
            RowParent = null;
            Scroll = null;
            for (int i = 0; i < TabContents.Length; i++)
            {
                TabContents[i] = null;
                TabRowParents[i] = null;
                TabScrolls[i] = null;
            }

            IntegerTemplate = null;
            ToggleTemplate = null;
            SliderTemplate = null;
            HeaderTemplate = null;
            BlankTemplate = null;
            RebindTemplate = null;

            WasOpened = false;
            PendingPauseSelection = false;
        }

        internal static void Cleanup()
        {
            ChatConvenience.Cleanup();

            if (PauseButtonRoot != null)
            {
                PauseButtonRoot.SetActive(false);
                UnityEngine.Object.Destroy(PauseButtonRoot);
            }

            if (DevPauseButtonRoot != null)
            {
                DevPauseButtonRoot.SetActive(false);
                UnityEngine.Object.Destroy(DevPauseButtonRoot);
            }

            DevPauseButtonRoot = null;
            PausePanel = null;
            PauseButtonRoot = null;
            PauseButton = null;
            VanillaOptionButton = null;

            CleanupPanelOnly();

            NextEnsure = 0f;
        }
    }
}


