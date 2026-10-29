using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SephiriaExpanded
{
    internal static class ChatConvenience
    {
        private const string Command = "/clear";
        private const string HarmonyId = "SephiriaExpanded.ChatConvenience";
        private const string TimeColor = "#7CE08A";
        private const int HistoryLimit = 500;
        private const float TrimInterval = 0.2f;

        private static readonly FieldInfo PlayedRealtime = AccessTools.Field(typeof(DungeonManager), "playedRealtimeClientside");

        private static Harmony harmony;
        private static UI_HUDLogViewer maskedViewer;
        private static float nextTrim;
        private static bool patched;
        private static readonly List<ItemName> items = new List<ItemName>();
        private static bool itemCacheReady;
        private static readonly Dictionary<UI_HUDLog, string> formatted =
            new Dictionary<UI_HUDLog, string>();
        private static readonly List<ChatEntry> history = new List<ChatEntry>();
        private static readonly List<UI_HUDLog> visibleLogs = new List<UI_HUDLog>();
        private static UI_HUDLogViewer scrollViewer;
        private static bool chatOpen;
        private static UI_HUDLogViewer syncedViewer;
        private static int syncedCapacity = -1;
        private static int syncedCount = -1;
        private static UI_HUDLogViewer seededViewer;
        private static int scrollSteps;
        private static bool dragging;
        private static float dragLastY;
        private static float dragDistance;

        [ThreadStatic] private static bool receivingChat;

        private struct ItemName
        {
            internal string Name;
            internal string Color;
        }

        private struct ChatEntry
        {
            internal string Text;
            internal Color Color;
        }

        internal static void Tick()
        {
            EnsurePatches();
            ProcessScroll();
            SyncWhileOpen();
            LimitVisibleLogs();
        }

        private static void SyncWhileOpen()
        {
            if (!Enabled || !chatOpen || scrollSteps != 0) return;

            UI_HUDLogViewer viewer = FindViewer();
            if (viewer != null && viewer.logPool.Count > 0) SyncOpenViewer(viewer);
        }

        private static UI_HUDLogViewer FindViewer()
        {
            if (UIManager.Instance == null) return null;

            UI_ChatInput chat = UIManager.Instance.GetElement<UI_ChatInput>();
            return chat == null ? null : chat.logViewer;
        }

        private static int GetCapacity(UI_HUDLogViewer viewer, List<UI_HUDLog> pool)
        {
            int capacity = pool.Count;
            if (viewer.panelImage == null) return capacity;

            VerticalLayoutGroup layout = viewer.contentsZone == null ? null : viewer.contentsZone.GetComponent<VerticalLayoutGroup>();
            float pitch = GetLineHeight(viewer) + (layout == null ? 0f : layout.spacing);
            float height = viewer.panelImage.rectTransform.rect.height;
            if (pitch <= 0f || height <= 0f) return capacity;

            return Mathf.Min(capacity, Mathf.Max(1, Mathf.FloorToInt(height / pitch + 0.05f)));
        }

        private static void LimitVisibleLogs()
        {
            if (Time.unscaledTime < nextTrim) return;

            nextTrim = Time.unscaledTime + TrimInterval;
            TrimVisibleLogs(Enabled && chatOpen ? FindViewer() : null);
        }

        private static void SyncOpenViewer(UI_HUDLogViewer viewer)
        {
            int capacity = GetCapacity(viewer, viewer.logPool);
            if (viewer == syncedViewer && capacity == syncedCapacity && history.Count == syncedCount) return;

            syncedViewer = viewer;
            syncedCapacity = capacity;
            syncedCount = history.Count;
            RenderHistory(viewer);
        }

        private static void TrimVisibleLogs(UI_HUDLogViewer viewer)
        {
            if (viewer == null || viewer.logPool.Count == 0) return;

            EnsureChatMask(viewer);

            int capacity = GetCapacity(viewer, viewer.logPool);
            visibleLogs.Clear();
            foreach (UI_HUDLog log in viewer.logPool)
                if (log != null && log.gameObject.activeSelf) visibleLogs.Add(log);

            if (visibleLogs.Count > capacity)
            {
                visibleLogs.Sort((left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
                for (int i = 0; i < visibleLogs.Count - capacity; i++)
                    visibleLogs[i].gameObject.SetActive(false);
            }

            FitHeight(viewer);
        }

        private static float LogHeight(UI_HUDLog log)
        {
            RectTransform rect = log.transform as RectTransform;
            float height = rect == null ? 0f : rect.rect.height;
            return log.text == null ? height : Mathf.Max(height, log.text.preferredHeight);
        }

        private static float nextFitLog;

        private static void FitHeight(UI_HUDLogViewer viewer)
        {
            if (viewer.panelImage == null || viewer.contentsZone == null) return;

            float limit = viewer.panelImage.rectTransform.rect.height;
            if (limit <= 0f) return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(viewer.contentsZone);
            VerticalLayoutGroup layout = viewer.contentsZone.GetComponent<VerticalLayoutGroup>();
            float spacing = layout == null ? 0f : layout.spacing;
            visibleLogs.Clear();
            foreach (UI_HUDLog log in viewer.logPool)
                if (log != null && log.gameObject.activeSelf) visibleLogs.Add(log);

            visibleLogs.Sort((left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
            float total = spacing * Mathf.Max(0, visibleLogs.Count - 1);
            foreach (UI_HUDLog log in visibleLogs)
                total += LogHeight(log);

            if (Time.unscaledTime >= nextFitLog)
            {
                nextFitLog = Time.unscaledTime + 1f;
                Debug.Log("[Sephiria Expanded][Chat] 높이 맞춤: 한계 " + limit + ", 합계 " + total + ", 줄 수 " + visibleLogs.Count + ", 첫 줄 높이 " + (visibleLogs.Count > 0 ? LogHeight(visibleLogs[visibleLogs.Count - 1]) : 0f) + ", 간격 " + spacing);
            }

            for (int i = 0; i < visibleLogs.Count - 1 && total > limit + 0.5f; i++)
            {
                total -= LogHeight(visibleLogs[i]) + spacing;
                visibleLogs[i].gameObject.SetActive(false);
            }
        }

        private static void EnsurePatches()
        {
            if (patched) return;
            patched = true;
            try
            {
                harmony = new Harmony(HarmonyId);
                Patch(typeof(UI_ChatInput), "OnSubmitCommand", nameof(SubmitPrefix));
                Patch(typeof(UI_ChatInput), "OnOpened", nameof(ChatOpenedPrefix), nameof(ChatOpenedPostfix));
                Patch(typeof(UI_ChatInput), "OnClosed", nameof(ChatClosedPrefix), nameof(ChatClosedPostfix));
                Patch(typeof(UI_HUDLogViewer), "SpawnLog", nameof(SpawnPrefix), nameof(SpawnPostfix));
                Patch(typeof(UI_HUDLogViewer), "ShowAllLogs", nameof(ShowAllPrefix));
                Patch(typeof(UI_HUDLog), "SetText", nameof(SetTextPrefix), nameof(SetTextPostfix));
                Patch(typeof(UI_HUDLog), "Show", nameof(ShowPrefix));
                Patch(typeof(UI_HUDLog), "Update", nameof(UpdatePrefix));
                Patch(typeof(DungeonManager), "Chat", nameof(NetworkPrefix));
                Patch(typeof(DungeonManager), "CmdChat", nameof(NetworkPrefix));
                Patch(typeof(DungeonManager), "RpcChat", nameof(NetworkPrefix));
                Patch(typeof(DungeonManager), "UserCode_CmdChat__PlayerAvatar__String__String", nameof(NetworkPrefix));
                Patch(typeof(DungeonManager), "UserCode_RpcChat__PlayerAvatar__String__String", nameof(ReceivePrefix), nameof(ReceivePostfix));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Sephiria Expanded] ChatConvenience patch failed: " + ex);
                UnpatchOwnedPatches();
                harmony = null;
            }
        }

        private static void Patch(Type type, string target, string prefix, string postfix = null)
        {
            var method = AccessTools.Method(type, target);
            if (method == null) throw new MissingMethodException(type.Name, target);
            var pre = AccessTools.Method(typeof(ChatConvenience), prefix);
            var post = postfix == null ? null : AccessTools.Method(typeof(ChatConvenience), postfix);
            harmony.Patch(method, pre == null ? null : new HarmonyMethod(pre),
                post == null ? null : new HarmonyMethod(post));
        }

        private static bool Enabled { get { return ModSettings.ChatConvenienceEnabled; } }

        private static bool IsClear(string value)
        {
            return value != null && value.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SubmitPrefix(UI_ChatInput __instance)
        {
            if (!Enabled || __instance == null || __instance.inputField == null ||
                !IsClear(__instance.inputField.text)) return true;
            Clear(__instance.logViewer);
            __instance.inputField.SetTextWithoutNotify(string.Empty);
            __instance.Close();
            return false;
        }

        private static void ChatOpenedPrefix()
        {
            chatOpen = true;
            syncedViewer = null;
            ResetScroll();
        }

        private static void ChatOpenedPostfix(UI_ChatInput __instance)
        {
            UI_HUDLogViewer viewer = __instance == null ? null : __instance.logViewer;
            if (!Enabled || viewer == null || viewer.logPool.Count == 0) return;

            SeedHistory(viewer);
            SyncOpenViewer(viewer);
        }

        private static void ChatClosedPrefix(UI_ChatInput __instance)
        {
            chatOpen = false;
            if (__instance != null)
                ResetScroll(__instance.logViewer);
            else
                ResetScroll();
        }

        private static void ChatClosedPostfix(UI_ChatInput __instance)
        {
            if (__instance != null)
                ResetScroll(__instance.logViewer);
            else
                ResetScroll();
        }

        private static bool NetworkPrefix(object[] __args)
        {
            if (!Enabled || __args == null) return true;
            foreach (object arg in __args)
            {
                if (!IsClear(arg as string)) continue;
                Clear(null);
                return false;
            }
            return true;
        }

        private static bool ReceivePrefix(object[] __args)
        {
            if (!NetworkPrefix(__args)) return false;
            receivingChat = Enabled;
            return true;
        }

        private static void ReceivePostfix()
        {
            receivingChat = false;
        }

        private static void Clear(UI_HUDLogViewer directViewer)
        {
            if (directViewer != null) ClearViewer(directViewer);
            foreach (var viewer in UnityEngine.Object.FindObjectsByType<UI_HUDLogViewer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (viewer != null && viewer != directViewer) ClearViewer(viewer);
            }
            formatted.Clear();
            history.Clear();
            seededViewer = null;
            ResetScroll(directViewer);
        }

        private static void ProcessScroll()
        {
            if (!Enabled || UIManager.Instance == null)
            {
                ResetScroll();
                return;
            }

            UI_ChatInput chat = UIManager.Instance.GetElement<UI_ChatInput>();
            if (chat == null || !chat.IsOpened || chat.logViewer == null)
            {
                ResetScroll();
                return;
            }

            UI_HUDLogViewer viewer = chat.logViewer;
            SeedHistory(viewer);
            EnsureChatMask(viewer);
            if (scrollViewer != viewer)
            {
                scrollViewer = viewer;
                scrollSteps = 0;
                dragging = false;
                dragDistance = 0f;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 mousePosition = mouse.position.ReadValue();
            RectTransform viewerRect = viewer.transform as RectTransform;
            bool inside = viewerRect != null &&
                          RectTransformUtility.RectangleContainsScreenPoint(viewerRect, mousePosition);

            if (inside && mouse.leftButton.wasPressedThisFrame)
            {
                dragging = true;
                dragLastY = mousePosition.y;
                dragDistance = 0f;
            }
            if (!mouse.leftButton.isPressed)
                dragging = false;

            int direction = 0;
            int amount = 0;
            float wheel = mouse.scroll.ReadValue().y;
            if (!Mathf.Approximately(wheel, 0f))
            {
                direction = wheel > 0f ? 1 : -1;
                amount = 1;
            }
            else if (dragging)
            {
                float deltaY = mousePosition.y - dragLastY;
                dragLastY = mousePosition.y;
                dragDistance += deltaY;
                float lineHeight = GetLineHeight(viewer);
                if (lineHeight > 0f && Mathf.Abs(dragDistance) >= lineHeight)
                {
                    direction = dragDistance < 0f ? 1 : -1;
                    amount = Mathf.Max(1, Mathf.FloorToInt(Mathf.Abs(dragDistance) / lineHeight));
                    dragDistance -= Mathf.Sign(dragDistance) * amount * lineHeight;
                }
            }

            if (amount == 0) return;

            List<UI_HUDLog> pool = GetPool(viewer);
            if (pool == null) return;
            int maxSteps = Mathf.Max(0, history.Count - GetCapacity(viewer, pool));
            scrollSteps = Mathf.Clamp(scrollSteps + direction * amount, 0, maxSteps);
            RenderHistory(viewer);
        }

        private static void EnsureChatMask(UI_HUDLogViewer viewer)
        {
            if (maskedViewer == viewer) return;

            RectTransform contents = viewer.contentsZone;
            if (contents == null) return;

            RectTransform clipRect = viewer.panelImage != null
                ? viewer.panelImage.rectTransform
                : contents.parent as RectTransform;
            if (clipRect != null && clipRect.GetComponent<RectMask2D>() == null)
                clipRect.gameObject.AddComponent<RectMask2D>();

            maskedViewer = viewer;
        }

        private static List<UI_HUDLog> GetPool(UI_HUDLogViewer viewer)
        {
            return viewer.logPool;
        }

        private static float GetLineHeight(UI_HUDLogViewer viewer)
        {
            List<UI_HUDLog> pool = GetPool(viewer);
            if (pool != null)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i] == null) continue;
                    RectTransform rect = pool[i].transform as RectTransform;
                    if (rect != null && rect.rect.height > 0f) return rect.rect.height;
                }
            }
            return 20f;
        }

        private static void SeedHistory(UI_HUDLogViewer viewer)
        {
            if (seededViewer == viewer) return;
            seededViewer = viewer;

            if (history.Count > 0) return;

            List<UI_HUDLog> pool = GetPool(viewer);
            if (pool == null) return;

            for (int i = 0; i < pool.Count; i++)
            {
                UI_HUDLog log = pool[i];
                if (log == null || log.text == null || string.IsNullOrEmpty(log.text.text)) continue;
                history.Add(new ChatEntry
                {
                    Text = log.text.text,
                    Color = log.text.color
                });
            }
        }

        private static void RenderHistory(UI_HUDLogViewer viewer)
        {
            List<UI_HUDLog> pool = GetPool(viewer);
            if (pool == null) return;

            int end = Mathf.Max(0, history.Count - scrollSteps);
            int start = Mathf.Max(0, end - GetCapacity(viewer, pool));
            for (int i = 0; i < pool.Count; i++)
            {
                UI_HUDLog log = pool[i];
                if (log == null) continue;

                int index = start + i;
                if (index >= end)
                {
                    if (log.text != null) log.text.SetText(string.Empty);
                    log.gameObject.SetActive(false);
                    continue;
                }

                ChatEntry entry = history[index];
                log.gameObject.SetActive(true);
                log.SetText(entry.Text, entry.Color);
                log.transform.SetAsLastSibling();
            }

            FitHeight(viewer);
        }

        private static void ResetScroll()
        {
            if (scrollViewer != null)
                ResetScroll(scrollViewer);
            else
                scrollSteps = 0;
        }

        private static void ResetScroll(UI_HUDLogViewer viewer)
        {
            if (viewer == null)
            {
                scrollSteps = 0;
                scrollViewer = null;
                return;
            }

            scrollSteps = 0;
            if (scrollViewer == viewer)
                RenderHistory(viewer);

            scrollViewer = null;
            dragging = false;
            dragDistance = 0f;
        }

        private static void ClearViewer(UI_HUDLogViewer viewer)
        {
            var pool = GetPool(viewer);
            if (pool == null) return;
            foreach (var log in pool)
            {
                if (log == null) continue;
                if (log.text != null) log.text.SetText(string.Empty);
                log.gameObject.SetActive(false);
            }
        }

        private static bool ShowAllPrefix(UI_HUDLogViewer __instance)
        {
            if (!Enabled) return true;
            var pool = __instance.logPool;
            if (pool == null) return true;
            foreach (var log in pool)
                if (log != null && log.text != null && !string.IsNullOrEmpty(log.text.text)) log.Show();
            TrimVisibleLogs(__instance);
            return false;
        }

        private static bool ShowPrefix(UI_HUDLog __instance)
        {
            return !Enabled || __instance == null || __instance.text == null ||
                   !string.IsNullOrEmpty(__instance.text.text);
        }

        private static bool UpdatePrefix()
        {
            return !Enabled || !chatOpen;
        }

        private static bool SpawnPrefix(ref string log, Color color)
        {
            if (IsLevelCapNotice(log))
                return false;

            if (!Enabled || string.IsNullOrEmpty(log)) return true;
            if (IsClear(log)) { log = string.Empty; return false; }
            if (scrollSteps > 0)
                ResetScroll();
            string stamp = "<color=" + TimeColor + ">[" + RunTime() + "]</color> ";
            log = receivingChat && color == Color.cyan ? stamp + log : stamp + ColorItem(log);
            history.Add(new ChatEntry
            {
                Text = log,
                Color = color
            });
            if (history.Count > HistoryLimit) history.RemoveAt(0);
            return true;
        }

        private static void SpawnPostfix(UI_HUDLogViewer __instance)
        {
            if (Enabled) TrimVisibleLogs(__instance);
        }

        private static bool IsLevelCapNotice(string log)
        {
            return !string.IsNullOrEmpty(log) &&
                   log.IndexOf("레벨 상한", StringComparison.Ordinal) >= 0 &&
                   log.IndexOf("30", StringComparison.Ordinal) >= 0;
        }

        private static string RunTime()
        {
            if (ModSettings.ChatTimeMode == 0) return DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);

            var manager = DungeonManager.Instance;
            float seconds = 0f;
            if (manager != null && PlayedRealtime != null) seconds = (float)PlayedRealtime.GetValue(manager);
            int whole = Math.Max(0, (int)seconds);
            return (whole / 60).ToString("00", CultureInfo.InvariantCulture) + ":" +
                   (whole % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private static void BuildItemCache()
        {
            if (itemCacheReady) return;
            items.Clear();
            try
            {
                foreach (int id in ItemDatabase.GetAllItemID())
                {
                    var item = ItemDatabase.FindItemById(id);
                    if (item == null || string.IsNullOrEmpty(item.Name)) continue;
                    var color = ItemDatabase.GetColorViaItemRarity(item.rarity);
                    items.Add(new ItemName
                    {
                        Name = item.Name,
                        Color = "#" + ColorUtility.ToHtmlStringRGB(color)
                    });
                }
                items.Sort((a, b) => b.Name.Length.CompareTo(a.Name.Length));
                itemCacheReady = true;
            }
            catch { items.Clear(); }
        }

        private static string ColorItem(string log)
        {
            int end = log.IndexOf(" 획득", StringComparison.Ordinal);
            if (end < 0) return log;
            BuildItemCache();
            foreach (var item in items)
            {
                int start = log.LastIndexOf(item.Name, end, StringComparison.Ordinal);
                if (start < 0) continue;
                int afterName = start + item.Name.Length;
                string suffix = log.Substring(end).Trim();
                bool own = afterName == end && suffix == "획득";
                bool other = afterName + 1 == end &&
                    (log[afterName] == '을' || log[afterName] == '를');
                bool completedOther = suffix == "획득했습니다" || suffix == "획득했습니다.";
                if (!own && (!other || !completedOther)) continue;
                return log.Substring(0, start) + "<color=" + item.Color + ">" +
                       item.Name + "</color>" + log.Substring(end);
            }
            return log;
        }

        private static void SetTextPrefix(UI_HUDLog __instance, string log)
        {
            if (!Enabled || __instance == null) return;
            if (log != null && log.Contains("<color=")) formatted[__instance] = log;
            else formatted.Remove(__instance);
        }

        private static void SetTextPostfix(UI_HUDLog __instance)
        {
            if (!Enabled || __instance == null || __instance.text == null) return;
            string value;
            if (!formatted.TryGetValue(__instance, out value)) return;
            __instance.text.richText = true;
            __instance.text.parseCtrlCharacters = false;
            __instance.text.SetText(value);
        }

        private static void UnpatchOwnedPatches()
        {
            if (harmony == null) return;

            var self = typeof(Harmony).GetMethod("UnpatchSelf", Type.EmptyTypes);
            if (self != null && !self.IsStatic)
            {
                self.Invoke(harmony, null);
                return;
            }

            var byId = typeof(Harmony).GetMethod("UnpatchAll", new[] { typeof(string) });
            if (byId != null && !byId.IsStatic)
            {
                byId.Invoke(harmony, new object[] { HarmonyId });
                return;
            }

            throw new MissingMethodException(typeof(Harmony).FullName,
                "UnpatchSelf() / UnpatchAll(string)");
        }

        internal static void Cleanup()
        {
            UnpatchOwnedPatches();
            harmony = null;
            patched = false;
            formatted.Clear();
            history.Clear();
            seededViewer = null;
            maskedViewer = null;
            visibleLogs.Clear();
            items.Clear();
            itemCacheReady = false;
            receivingChat = false;
            chatOpen = false;
        }
    }
}
