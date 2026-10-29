using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class FirstArtifactLegendary
    {
        private static readonly HashSet<uint> AcquiredNetIds = new HashSet<uint>();
        private static bool activeForCurrentGeneration;

        internal static void Tick()
        {
            DungeonManager manager = DungeonManager.Instance;
            if (manager == null || !NetworkServer.active || !manager.NetworkisRunStarted)
                AcquiredNetIds.Clear();
        }

        [HarmonyPatch(typeof(Sephirite), nameof(Sephirite.GenerateItems))]
        private static class GenerateItemsPatch
        {
            [HarmonyPrefix]
            private static void Before(GameObject actor)
            {
                NetworkIdentity identity = actor != null ? actor.GetComponent<NetworkIdentity>() : null;
                uint netId = identity != null ? identity.netId : 0;

                activeForCurrentGeneration = NetworkServer.active
                    && ModSettings.FirstArtifactLegendaryEnabled
                    && !AcquiredNetIds.Contains(netId);
            }
        }

        [HarmonyPatch(typeof(Sephirite), nameof(Sephirite.RarityDice))]
        private static class RarityDicePatch
        {
            [HarmonyPrefix]
            private static bool Before(ref EItemRarity __result)
            {
                if (!activeForCurrentGeneration) return true;

                __result = EItemRarity.Legend;
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayerAvatar), "LocalSelectSephiriteReward")]
        private static class MarkAcquired
        {
            [HarmonyPrefix]
            private static void Before(PlayerAvatar __instance, int idx)
            {
                if (idx >= 0 && __instance != null) AcquiredNetIds.Add(__instance.netId);
            }
        }

        [HarmonyPatch(typeof(PlayerAvatar), "LocalSelectSephiriteRewardToSubBag")]
        private static class MarkAcquiredSubBag
        {
            [HarmonyPrefix]
            private static void Before(PlayerAvatar __instance, int idx)
            {
                if (idx >= 0 && __instance != null) AcquiredNetIds.Add(__instance.netId);
            }
        }
    }
}
