using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class MinimapOpen
    {
        private static readonly Dictionary<UI_Map_Room, string> RevealedRooms =
            new Dictionary<UI_Map_Room, string>();

        private static readonly System.Reflection.FieldInfo CurrentMapField =
            AccessTools.Field(typeof(UI_MapPanel), "currentMap");
        private static readonly System.Reflection.FieldInfo FloorGuidField =
            AccessTools.Field(typeof(UI_MapPanel), "currentMapFloorGuid");
        private static readonly System.Reflection.FieldInfo RealignTimerField =
            AccessTools.Field(typeof(UI_MapPanel), "mapElementRealignTimer");

        private static void Reveal(UI_Map map, string floorGuid)
        {
            if (!ModSettings.MinimapOpenEnabled || map == null || map.rooms == null)
                return;

            foreach (var room in map.rooms)
            {
                if (room == null) continue;
                if (!room.gameObject.activeSelf)
                {
                    if (!RevealedRooms.ContainsKey(room)) RevealedRooms.Add(room, floorGuid);
                    room.gameObject.SetActive(true);
                }
                SetDim(room, floorGuid);
            }
        }

        private static void SetDim(UI_Map_Room room, string floorGuid)
        {
            if (room == null) return;
            CanvasGroup group = room.GetComponent<CanvasGroup>();
            if (group == null) group = room.gameObject.AddComponent<CanvasGroup>();
            bool visited = IsNormallyVisible(room, floorGuid);
            group.alpha = visited ? 1f : 0.35f;
            group.interactable = visited || ModSettings.MinimapOpenTeleportEnabled;

            foreach (var nested in room.GetComponentsInChildren<CanvasGroup>(true))
            {
                if (nested == null || nested == group) continue;
                nested.alpha = visited ? 1f : 0.35f;
                nested.interactable = visited || ModSettings.MinimapOpenTeleportEnabled;
            }

            foreach (var icon in room.GetComponentsInChildren<UI_MinimapElement>(true))
            {
                if (icon == null) continue;
                CanvasGroup iconGroup = icon.GetComponent<CanvasGroup>();
                if (iconGroup == null) iconGroup = icon.gameObject.AddComponent<CanvasGroup>();
                iconGroup.alpha = visited ? 1f : 0.35f;
                iconGroup.interactable = visited || ModSettings.MinimapOpenTeleportEnabled;
            }
        }

        private static bool IsNormallyVisible(UI_Map_Room room, string floorGuid)
        {
            if (room == null) return false;
            if (ScreenFader.Instance != null && ScreenFader.Instance.IsTestMode)
                return true;

            foreach (var floor in FloorGenerator.FloorGenerators)
            {
                if (floor == null || floor.guid != floorGuid) continue;

                foreach (var area in floor.revealedAreas)
                {
                    Vector2 point = area.Key;
                    if (area.Value && point.x >= room.bottomLeft.x && point.x <= room.topRight.x &&
                        point.y >= room.bottomLeft.y && point.y <= room.topRight.y)
                        return true;
                }
                if (room.parent != null && room.parent.player != null)
                {
                    Vector3 position = room.parent.player.transform.position;
                    if (position.x >= room.bottomLeft.x && position.x <= room.topRight.x &&
                        position.y >= room.bottomLeft.y && position.y <= room.topRight.y)
                        return true;
                }
                return false;
            }
            return false;
        }

        private static bool IsBossFloor(string floorGuid)
        {
            foreach (var floor in FloorGenerator.FloorGenerators)
            {
                if (floor == null || floor.guid != floorGuid || floor.DataOnServer == null) continue;
                return floor.DataOnServer.threatType == EFloorThreatType.Boss;
            }
            return false;
        }

        private static void Restore()
        {
            foreach (var entry in RevealedRooms)
            {
                if (entry.Key != null)
                {
                    entry.Key.gameObject.SetActive(IsNormallyVisible(entry.Key, entry.Value));
                    SetDim(entry.Key, entry.Value);
                }
            }
            RevealedRooms.Clear();
        }

        private static void RemoveDestroyedRooms()
        {
            var removed = new List<UI_Map_Room>();
            foreach (var entry in RevealedRooms)
                if (entry.Key == null) removed.Add(entry.Key);
            foreach (var room in removed) RevealedRooms.Remove(room);
        }

        internal static void Refresh()
        {
            if (!ModSettings.MinimapOpenEnabled) Restore();
            RemoveDestroyedRooms();

            foreach (var panel in UnityEngine.Object.FindObjectsByType<UI_MapPanel>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (panel == null) continue;
                Reveal(CurrentMapField.GetValue(panel) as UI_Map,
                    FloorGuidField.GetValue(panel) as string);
                var timer = RealignTimerField.GetValue(panel) as Timer;
                if (timer != null) timer.Ratio = 1f;
            }
        }

        internal static void Cleanup()
        {
            Restore();
        }

        [HarmonyPatch(typeof(UI_MapPanel), "Show")]
        private static class ShowPatch
        {
            private static void Postfix(UI_Map ___currentMap,
                string ___currentMapFloorGuid, Timer ___mapElementRealignTimer)
            {
                RemoveDestroyedRooms();
                if (!ModSettings.MinimapOpenEnabled) Restore();
                Reveal(___currentMap, ___currentMapFloorGuid);
                if (ModSettings.MinimapOpenEnabled && ___mapElementRealignTimer != null)
                    ___mapElementRealignTimer.Ratio = 1f;
            }
        }

        [HarmonyPatch(typeof(UI_MapPanel), "Update")]
        private static class UpdatePatch
        {
            private static void Prefix(UI_Map ___currentMap, string ___currentMapFloorGuid)
            {
                if (ModSettings.MinimapOpenEnabled)
                    Reveal(___currentMap, ___currentMapFloorGuid);
                else if (RevealedRooms.Count != 0)
                    Restore();
            }
        }

        [HarmonyPatch(typeof(UI_MapPanel), "Disconnect")]
        private static class DisconnectPatch
        {
            private static void Prefix()
            {
                Restore();
            }
        }

        [HarmonyPatch(typeof(UI_Map_Room), "TeleportToRoom")]
        private static class TeleportPatch
        {
            private static bool Prefix(UI_Map_Room __instance)
            {
                string floorGuid;
                if (!RevealedRooms.TryGetValue(__instance, out floorGuid)) return true;
                bool visited = IsNormallyVisible(__instance, floorGuid);

                if (IsBossFloor(floorGuid) && !visited) return false;
                if (!ModSettings.MinimapOpenTeleportEnabled && !visited) return false;

                if (ModSettings.MinimapOpenTeleportEnabled && __instance.parent != null &&
                    __instance.parent.player != null)
                {
                    PlayerAvatar player = __instance.parent.player;
                    Vector3 destination = __instance.clickToTeleportPosition;

                    if (ScreenFader.Instance != null)
                    {
                        ScreenFader.Instance.FadeOut(
                            (ScreenFader.ScreenType)0,
                            () => player.ReqSetPosition(destination, true),
                            true,
                            0.33f,
                            5f,
                            0f
                        );
                    }
                    else
                    {
                        player.ReqSetPosition(destination, true);
                    }
                    return false;
                }
                return true;
            }
        }

    }
}
