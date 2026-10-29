using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class PingIgnore
    {
        private const float PortalRadius = 0.75f;

        private const int PortalKind = 1;
        private const int LogKind = 2;

        private static readonly Dictionary<uint, int> Used = new Dictionary<uint, int>();
        private static bool inNextStageNotice;
        private static bool portalAllowed = true;

        [HarmonyPatch(typeof(PlayerAvatar), "UserCode_RpcBroadcastStageMovePing__Boolean__Int32")]
        private static class StageMovePingPatch
        {
            [HarmonyPrefix]
            private static void Before()
            {
                inNextStageNotice = true;
            }

            [HarmonyFinalizer]
            private static void After()
            {
                inNextStageNotice = false;
            }
        }

        [HarmonyPatch(typeof(PlayerAvatar), "UserCode_RpcWriteWantsToNextLog")]
        private static class WantsToNextPatch
        {
            [HarmonyPrefix]
            private static void Before()
            {
                inNextStageNotice = true;
            }

            [HarmonyFinalizer]
            private static void After()
            {
                inNextStageNotice = false;
            }
        }

        [HarmonyPatch(typeof(GoToNextPlaceTogether), "UserCode_CmdMoveRequest__PlayerAvatar")]
        private static class HostPortalCommandPatch
        {
            [HarmonyPrefix]
            private static void Before(PlayerAvatar requested)
            {
                portalAllowed = !HostLimit || requested == null || Allow(requested.netId, PortalKind);
            }

            [HarmonyFinalizer]
            private static void After()
            {
                portalAllowed = true;
            }
        }

        [HarmonyPatch(typeof(PlayerSpawner), nameof(PlayerSpawner.CreateMultiplayPing), new[] { typeof(bool), typeof(Vector2) })]
        private static class HostPortalPingPatch
        {
            [HarmonyPrefix]
            private static bool Before()
            {
                return !HostLimit || portalAllowed;
            }
        }

        [HarmonyPatch(typeof(GoToNextPlaceTogether), "RpcBroadcastMessage")]
        private static class HostPortalMessagePatch
        {
            [HarmonyPrefix]
            private static bool Before()
            {
                return !HostLimit || portalAllowed;
            }
        }

        [HarmonyPatch(typeof(PlayerAvatar), "RpcWriteWantsToNextLog")]
        private static class HostWantsToNextPatch
        {
            [HarmonyPrefix]
            private static bool Before(PlayerAvatar __instance)
            {
                return !HostLimit || Allow(__instance.netId, LogKind);
            }
        }

        [HarmonyPatch(typeof(PlayerAvatar), "RpcBroadcastStageMovePing")]
        private static class HostStageMovePatch
        {
            [HarmonyPrefix]
            private static bool Before(PlayerAvatar __instance)
            {
                return !HostLimit || Allow(__instance.netId, LogKind);
            }
        }

        [HarmonyPatch(typeof(DungeonManager), nameof(DungeonManager.MoveFloor))]
        private static class FloorChangePatch
        {
            [HarmonyPrefix]
            private static void Before(PlayerAvatar avatar)
            {
                if (avatar != null) Used.Remove(avatar.netId);
            }
        }

        private static bool HostLimit
        {
            get { return ModSettings.PingIgnoreEnabled && NetworkServer.active; }
        }

        private static bool Allow(uint playerId, int kind)
        {
            int used;
            Used.TryGetValue(playerId, out used);
            if ((used & kind) != 0) return false;

            Used[playerId] = used | kind;
            return true;
        }

        [HarmonyPatch(typeof(PlayerSpawner), nameof(PlayerSpawner.ClientCreateMultiplayPing))]
        private static class CreatePingPatch
        {
            [HarmonyPrefix]
            private static bool Before(Vector2 createPos)
            {
                if (!ModSettings.PingIgnoreEnabled || NetworkServer.active) return true;

                return !inNextStageNotice && !IsNextStagePortal(createPos);
            }
        }

        private static bool IsNextStagePortal(Vector2 position)
        {
            foreach (GoToNextPlaceTogether portal in Object.FindObjectsByType<GoToNextPlaceTogether>(FindObjectsSortMode.None))
                if (portal != null && Vector2.Distance(position, portal.transform.position) <= PortalRadius) return true;

            return false;
        }
    }
}
