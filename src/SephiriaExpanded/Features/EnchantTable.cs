using System;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class EnchantTable
    {
        [ThreadStatic]
        private static int spawningThreeUseAltar;

        private sealed class SpawnState
        {
            internal AltarOfEnchant_Spawner.AltarSpawnInfo[] OriginalTypes;
        }

        [HarmonyPatch(typeof(AltarOfEnchant_Spawner), "Spawn")]
        private static class SpawnPatch
        {
            private static void Prefix(
                AltarOfEnchant_Spawner __instance,
                ref SpawnState __state)
            {
                if (!NetworkServer.active || !ModSettings.EnchantTableEnabled)
                    return;

                __state = new SpawnState { OriginalTypes = __instance.altarTypes };
                spawningThreeUseAltar++;

                var candidates = __instance.altarTypes;
                if (candidates == null) return;

                foreach (var candidate in candidates)
                {
                    if (candidate == null || candidate.prefab == null ||
                        candidate.prefab.localUseCount != 3)
                        continue;

                    __instance.altarTypes = new[]
                    {
                        new AltarOfEnchant_Spawner.AltarSpawnInfo
                        {
                            prefab = candidate.prefab,
                            spawnChance = 1f
                        }
                    };
                    return;
                }

            }

            private static Exception Finalizer(
                AltarOfEnchant_Spawner __instance,
                SpawnState __state,
                Exception __exception)
            {
                if (__state != null)
                {
                    __instance.altarTypes = __state.OriginalTypes;
                    spawningThreeUseAltar--;
                }

                return __exception;
            }
        }

        [HarmonyPatch(typeof(AltarOfEnchant), "Awake")]
        private static class InitializePatch
        {
            private static void Prefix(AltarOfEnchant __instance)
            {
                if (spawningThreeUseAltar > 0 && NetworkServer.active)
                    __instance.localUseCount = 3;
            }
        }
    }
}
