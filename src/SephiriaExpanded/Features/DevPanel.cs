using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaExpanded
{
    internal static class DevPanel
    {
        private const string PanelName = "SephiriaExpanded_DevPanel";
        private static readonly string[] TabTitles = { "개발자", "이동" };
        private const string ResetName = "DevReset";
        private const int SectionGap = 3;
        private const int ChapterGap = 1;

        private static readonly string[] RowPrefixes =
        {
            "SephiriaExpanded_Header_",
            "SephiriaExpanded_Bool_",
            "SephiriaExpanded_Slider_",
            "SephiriaExpanded_Integer_",
            "SephiriaExpanded_Choice_",
            "SephiriaExpanded_Button_"
        };


        private static GameObject PanelRoot;
        private static UI_OptionsPanel Panel;
        private static UI_Tab Tabs;
        private static readonly UI_TabButton[] TabButtons = new UI_TabButton[TabTitles.Length];
        private static readonly UI_TabContent[] Contents = new UI_TabContent[TabTitles.Length];
        private static readonly Transform[] RowParents = new Transform[TabTitles.Length];
        private static readonly ScrollRect[] Scrolls = new ScrollRect[TabTitles.Length];
        private static ExpandedOptionRows.RowScope Rows;
        private static int ActiveTab;

        private static DeveloperMode Dev => DeveloperMode.Instance;

        internal static void Tick()
        {
            if (Panel == null || !Panel.IsOpened) return;

            for (int i = 0; i < TabButtons.Length; i++)
                if (TabButtons[i] != null)
                    TabButtons[i].SetTabButtonSprite(i == ActiveTab);
        }

        internal static void Open()
        {
            if (!EnsureCreated()) return;
            if (Panel.IsOpened) return;

            ExpandedOptionRows.RefreshValues(Rows);
            ActiveTab = 0;
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

            Tabs.SelectTab(0);
            RefreshLayout();
        }

        internal static void Close()
        {
            if (Panel == null || !Panel.IsOpened) return;

            try
            {
                Panel.Close();
            }
            catch
            {
            }
        }

        private static bool EnsureCreated()
        {
            if (PanelRoot != null && Panel != null && RowParents[0] != null && Rows != null) return true;

            Cleanup();

            ExpandedSettingsPanel.Kit kit = ExpandedSettingsPanel.GetKit();
            if (kit == null)
            {
                DeveloperMode.ShowMessage("설정 패널이 아직 준비되지 않았습니다. 잠시 후 다시 시도하세요.");
                return false;
            }

            GameObject clone = null;
            ExpandedOptionRows.RowScope settingsScope = null;

            try
            {
                clone = UnityEngine.Object.Instantiate(kit.PanelRoot, kit.PanelRoot.transform.parent);
                clone.name = PanelName;
                clone.SetActive(false);

                UI_OptionsPanel panel = clone.GetComponent<UI_OptionsPanel>();
                if (panel == null || panel.tab == null)
                    throw new InvalidOperationException("개발자 패널 복제 실패");

                UI_Tab tab = panel.tab;
                if (tab.tabButtons == null || tab.tabButtons.Length == 0 || tab.tabContents == null || tab.tabContents.Length == 0)
                    throw new InvalidOperationException("개발자 패널 탭을 찾지 못했습니다.");

                panel.SetRoot(kit.Panel.ParentRoot);
                panel.hasControl = true;
                panel.canCloseControlWithESC = true;

                if (tab.tabButtons.Length < TabTitles.Length || tab.tabContents.Length < TabTitles.Length)
                    throw new InvalidOperationException("개발자 패널 탭이 부족합니다.");

                for (int i = TabTitles.Length; i < tab.tabButtons.Length; i++)
                    DisableTabButton(tab.tabButtons[i]);

                for (int i = TabTitles.Length; i < tab.tabContents.Length; i++)
                    ClearTabContent(tab.tabContents[i], panel);

                tab.OnTabSelected += index =>
                {
                    if (index >= TabTitles.Length)
                    {
                        tab.SelectTab(0);
                        return;
                    }

                    ActiveTab = index;
                    SelectFirstRow();
                    RefreshLayout();
                };

                for (int i = 0; i < TabTitles.Length; i++)
                {
                    UI_TabButton button = tab.tabButtons[i];
                    UI_TabContent content = tab.tabContents[i];
                    if (button == null || content == null)
                        throw new InvalidOperationException("개발자 패널 탭 " + i + "를 찾지 못했습니다.");

                    content.parent = panel;
                    button.gameObject.SetActive(true);
                    SetTabTitle(button, TabTitles[i]);

                    Transform rowParent = FindRowParent(content);
                    if (rowParent == null)
                        throw new InvalidOperationException("개발자 패널 옵션 부모를 찾지 못했습니다.");

                    for (int j = rowParent.childCount - 1; j >= 0; j--)
                        Discard(rowParent.GetChild(j).gameObject);

                    TabButtons[i] = button;
                    Contents[i] = content;
                    RowParents[i] = rowParent;
                    Scrolls[i] = FindScroll(content, rowParent);
                }

                PanelRoot = clone;
                Panel = panel;
                Tabs = tab;
                ActiveTab = 0;

                settingsScope = ExpandedOptionRows.Detach();
                BuildRows(kit);
                Rows = ExpandedOptionRows.Detach();

                RewireCloseButton(panel, Contents[0]);
                tab.SelectTab(0);
                clone.SetActive(false);

                Debug.Log("[Sephiria Expanded] 개발자 패널 생성 완료");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[Sephiria Expanded] 개발자 패널 생성 실패");
                Debug.LogException(exception);

                if (clone != null)
                {
                    clone.SetActive(false);
                    UnityEngine.Object.Destroy(clone);
                }

                PanelRoot = null;
                Panel = null;
                Tabs = null;
                ClearTabs();
                Rows = null;
                return false;
            }
            finally
            {
                if (settingsScope != null) ExpandedOptionRows.Attach(settingsScope);
            }
        }

        private static void BuildRows(ExpandedSettingsPanel.Kit kit)
        {
            BuildMoveRows(kit, RowParents[1]);
            BuildCheatRows(kit, RowParents[0]);
        }

        private static void BuildMoveRows(ExpandedSettingsPanel.Kit kit, Transform parent)
        {
            ExpandedOptionRows.AddHeader(kit.Header, parent, "체크하면 해당 위치로 이동합니다.");
            Gap(kit, parent, SectionGap);

            AddMove(kit, parent, "DevMoveBase", "시작마을", DeveloperMode.MoveToBase);
            Gap(kit, parent, ChapterGap);

            for (int chapter = 1; chapter <= 5; chapter++)
            {
                int stage = chapter;
                AddMove(kit, parent, "DevMoveFloor" + stage, stage + "층 스테이지", () => Dev.MoveToFloor(stage));
                AddMove(kit, parent, "DevMoveMiniBoss" + stage, SubItem(stage + "층 미니보스"), () => Dev.MoveToMiniBoss(stage));
                AddBossMoves(kit, parent, stage, stage);
                if (stage == 5) AddMove(kit, parent, "DevMoveKliphoth", SubItem("5층 보스 - 클리포트"), () => Dev.MoveToFloor(6));
                Gap(kit, parent, ChapterGap);
            }

            AddMove(kit, parent, "DevMoveFloor6", "6층 스테이지", () => Dev.MoveToFloor(7));
            AddBossMoves(kit, parent, 6, 7);
        }

        private static void BuildCheatRows(ExpandedSettingsPanel.Kit kit, Transform parent)
        {
            ExpandedOptionRows.AddBool(kit.Toggle, parent, "DevGod", "무적", () => Dev != null && Dev.God, value => Dev?.SetGod(value), RefreshAll);
            ExpandedOptionRows.AddBool(kit.Toggle, parent, "DevPreset", "프리셋", () => Dev != null && Dev.Preset, value => Dev?.SetPreset(value), RefreshAll);
            ExpandedOptionRows.AddBool(kit.Toggle, parent, "DevKill", "전투방 즉시 클리어", () => DeveloperKill.Enabled, value => Dev?.SetKill(value), RefreshAll);
            Gap(kit, parent, SectionGap);

            ExpandedOptionRows.AddSlider(kit.Slider, parent, "DevSpeed", "이동 속도", () => Dev == null ? 1 : Dev.Speed, value => Dev?.SetSpeed(value), 1, 10, value => "x" + value, 1);
            ExpandedOptionRows.AddSlider(kit.Slider, parent, "DevFixedDamage", "고정 피해", () => Dev == null ? 0 : Dev.FixedDamageStepValue, value => Dev?.SetFixedDamageStep(value), 0, DeveloperMode.FixedDamageStepMax, FixedDamageLabel, 0);
            ExpandedOptionRows.AddSlider(kit.Slider, parent, "DevDefense", "방어력", () => Dev == null ? 0 : Dev.DefenseStep, value => Dev?.SetDefenseStep(value), 0, DeveloperMode.DefenseStepMax, StatLabel, 0);
            ExpandedOptionRows.AddSlider(kit.Slider, parent, "DevToughness", "강인함", () => Dev == null ? 0 : Dev.ToughnessStep, value => Dev?.SetToughnessStep(value), 0, DeveloperMode.ToughnessStepMax, null, 0);
            ExpandedOptionRows.AddSlider(kit.Slider, parent, "DevLevel", "레벨", DeveloperMode.GetLevel, DeveloperMode.SetLevel, 1, DeveloperMode.MaxLevel(), null, 1);
            Gap(kit, parent, SectionGap);

            AddSteal(kit, parent, "DevHostHpSteal", "호스트 HP 흡수", () => ModSettings.DevHostHpSteal, value => ModSettings.DevHostHpSteal = value);
            AddSteal(kit, parent, "DevHostMpSteal", "호스트 MP 흡수", () => ModSettings.DevHostMpSteal, value => ModSettings.DevHostMpSteal = value);
            ExpandedOptionRows.AddBool(kit.Toggle, parent, "MoreDiceHostEnabled", "호스트 무한 주사위", () => ModSettings.MoreDiceHostEnabled, value => ModSettings.MoreDiceHostEnabled = value);
            Gap(kit, parent, ChapterGap);

            AddSteal(kit, parent, "DevGuestHpSteal", "게스트 HP 흡수", () => ModSettings.DevGuestHpSteal, value => ModSettings.DevGuestHpSteal = value);
            AddSteal(kit, parent, "DevGuestMpSteal", "게스트 MP 흡수", () => ModSettings.DevGuestMpSteal, value => ModSettings.DevGuestMpSteal = value);
            ExpandedOptionRows.AddBool(kit.Toggle, parent, "MoreDiceGuestEnabled", "게스트 무한 주사위", () => ModSettings.MoreDiceGuestEnabled, value => ModSettings.MoreDiceGuestEnabled = value);
            Gap(kit, parent, SectionGap);

            ExpandedOptionRows.AddSapphireInput(kit.Integer, parent);
            Gap(kit, parent, SectionGap);

            ExpandedOptionRows.AddHorayButton(kit.HorayButton, parent, ResetName, "개발자 탭 초기화", ResetDev);
        }

        private static string FixedDamageLabel(int step)
        {
            return (step * DeveloperMode.FixedDamageStep).ToString("N0", CultureInfo.InvariantCulture);
        }

        private static string StatLabel(int step)
        {
            return (step * DeveloperMode.StatStep).ToString();
        }

        private static string SubItem(string title)
        {
            return "   " + title;
        }

        private static void Gap(ExpandedSettingsPanel.Kit kit, Transform parent, int count)
        {
            for (int i = 0; i < count; i++)
                ExpandedOptionRows.AddBlank(kit.Blank, parent);
        }

        private static void AddBossMoves(ExpandedSettingsPanel.Kit kit, Transform parent, int label, int chapter)
        {
            string[] names = DeveloperMode.BossNamesByStage[chapter - 1];
            for (int i = 0; i < names.Length; i++)
            {
                string bossName = names[i];
                AddMove(kit, parent, "DevMoveBoss" + label + "_" + i, SubItem(label + "층 보스 - " + bossName), () => Dev.MoveToBoss(chapter, bossName));
            }
        }

        private static void AddMove(ExpandedSettingsPanel.Kit kit, Transform parent, string key, string title, Action move)
        {
            ExpandedOptionRows.AddBool(kit.Toggle, parent, key, title, () => false, value =>
            {
                if (value) Run(move);
            }, RefreshAll);
        }

        private static void AddSteal(ExpandedSettingsPanel.Kit kit, Transform parent, string key, string title, Func<int> getter, Action<int> setter)
        {
            ExpandedOptionRows.AddSlider(kit.Slider, parent, key, title, getter, setter, 0, ModSettings.StealMax, null, 0);
        }

        private static void Run(Action move)
        {
            if (Dev != null) Dev.StartCoroutine(CloseThen(move));
        }

        private static IEnumerator CloseThen(Action move)
        {
            if (Panel != null && Panel.IsOpened) Panel.Close();
            UI_PausePanel pause = UIManager.Instance == null ? null : UIManager.Instance.GetElement<UI_PausePanel>();
            if (pause != null && pause.IsOpened) pause.Close();
            yield return null;
            yield return null;
            if (GameTimeManager.Instance != null) GameTimeManager.Instance.UpdatePauseState();
            move();
        }

        private static void RefreshAll()
        {
            ExpandedOptionRows.RefreshValues(Rows);
        }

        private static void ResetDev()
        {
            if (Dev != null) Dev.ResetDev();
            RefreshAll();
        }

        [HarmonyPatch(typeof(UI_HorayButton), "OnSubmit")]
        private static class ResetButtonPatch
        {
            private static bool Prefix(UI_HorayButton __instance)
            {
                if (__instance == null || __instance.gameObject == null)
                    return true;

                Transform root = __instance.transform;
                for (int i = 0; i < 8 && root != null; i++, root = root.parent)
                {
                    if (root.gameObject.name != "SephiriaExpanded_Button_" + ResetName) continue;

                    ResetDev();
                    return false;
                }

                return true;
            }
        }

        private static void SelectFirstRow()
        {
            if (Panel == null || Contents[ActiveTab] == null || RowParents[ActiveTab] == null) return;

            Selectable first = RowParents[ActiveTab].GetComponentsInChildren<Selectable>(true)
                .FirstOrDefault(selectable => selectable != null && selectable.interactable && selectable.gameObject.activeSelf);

            Contents[ActiveTab].selectionOnOpened = first != null ? first.gameObject : null;
            Panel.defaultSelectable = Contents[ActiveTab].selectionOnOpened;
        }

        private static void RefreshLayout()
        {
            RectTransform rect = RowParents[ActiveTab] as RectTransform;
            if (rect != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(rect);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }

            UnityEngine.Canvas.ForceUpdateCanvases();

            ScrollRect scroll = Scrolls[ActiveTab];
            if (scroll == null) return;

            scroll.StopMovement();
            if (scroll.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);

            scroll.Rebuild(CanvasUpdate.PostLayout);
            UnityEngine.Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
        }

        private static void ClearTabs()
        {
            for (int i = 0; i < TabTitles.Length; i++)
            {
                TabButtons[i] = null;
                Contents[i] = null;
                RowParents[i] = null;
                Scrolls[i] = null;
            }

            ActiveTab = 0;
        }

        private static Transform FindRowParent(UI_TabContent content)
        {
            foreach (Transform child in content.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || child.parent == null || !IsRow(child.name)) continue;

                return child.parent;
            }

            ScrollRect scroll = FindScroll(content, null);
            return scroll != null && scroll.content != null ? scroll.content : null;
        }

        private static bool IsRow(string name)
        {
            foreach (string prefix in RowPrefixes)
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                    return true;

            return false;
        }

        private static ScrollRect FindScroll(UI_TabContent content, Transform parent)
        {
            ScrollRect[] values = content.GetComponentsInChildren<ScrollRect>(true);

            foreach (ScrollRect candidate in values)
            {
                if (candidate == null || candidate.content == null) continue;

                if (parent != null && (parent == candidate.content || parent.IsChildOf(candidate.content)))
                    return candidate;
            }

            return values.FirstOrDefault(candidate => candidate != null && candidate.content != null);
        }

        private static void Discard(GameObject target)
        {
            if (target == null) return;

            target.SetActive(false);
            UnityEngine.Object.Destroy(target);
        }

        private static void DisableTabButton(UI_TabButton button)
        {
            if (button == null) return;

            SetTabTitle(button, string.Empty);

            foreach (Selectable selectable in button.GetComponentsInChildren<Selectable>(true))
                selectable.interactable = false;

            CanvasGroup group = button.GetComponent<CanvasGroup>();
            if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private static void ClearTabContent(UI_TabContent content, UI_OptionsPanel panel)
        {
            if (content == null) return;

            content.parent = panel;

            Transform rows = FindRowParent(content);
            if (rows != null)
            {
                for (int i = rows.childCount - 1; i >= 0; i--)
                    Discard(rows.GetChild(i).gameObject);
            }

            content.CloseTab();
        }

        private static void SetTabTitle(UI_TabButton button, string title)
        {
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
            if (text != null) text.text = title;
        }

        private static void RewireCloseButton(UI_OptionsPanel panel, UI_TabContent content)
        {
            UI_LocalizationStringText closeLocalization =
                panel.GetComponentsInChildren<UI_LocalizationStringText>(true)
                    .FirstOrDefault(x => x != null &&
                                         x.valueString != null &&
                                         x.valueString.key == "UI_Close" &&
                                         !x.transform.IsChildOf(content.transform));

            Button closeButton = AncestorButton(closeLocalization != null ? closeLocalization.transform : null, panel.transform);

            if (closeButton == null)
            {
                foreach (Button candidate in panel.GetComponentsInChildren<Button>(true))
                {
                    if (candidate == null || candidate.transform.IsChildOf(content.transform)) continue;

                    string lower = candidate.name.ToLowerInvariant();
                    if (!lower.Contains("close") && !lower.Contains("back")) continue;

                    closeButton = candidate;
                    break;
                }
            }

            if (closeButton == null) return;

            closeButton.onClick = new Button.ButtonClickedEvent();
            closeButton.onClick.AddListener(delegate
            {
                ModSettings.Save();
                panel.Close();
            });
        }

        private static Button AncestorButton(Transform current, Transform stop)
        {
            Transform value = current;

            while (value != null)
            {
                Button button = value.GetComponent<Button>();
                if (button != null) return button;

                if (value == stop) break;

                value = value.parent;
            }

            return null;
        }

        internal static void Cleanup()
        {
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

            ExpandedOptionRows.Destroy(Rows);
            Rows = null;

            if (PanelRoot != null)
            {
                PanelRoot.SetActive(false);
                UnityEngine.Object.Destroy(PanelRoot);
            }

            PanelRoot = null;
            Panel = null;
            Tabs = null;
            ClearTabs();
        }
    }
}
