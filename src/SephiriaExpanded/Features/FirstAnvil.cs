using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class FirstAnvil
    {
        private static bool Done;
        private static uint KnownAnvilAsset;

        internal static void Tick()
        {
            DungeonManager manager = DungeonManager.Instance;
            if (manager == null || !NetworkServer.active || !ModSettings.FirstAnvilEnabled)
            {
                Done = false;
                return;
            }

            if (!manager.NetworkisRunStarted)
            {
                Done = false;
                return;
            }

            if (Done || manager.generatedFloors == null || manager.generatedFloors.Count == 0)
                return;

            RememberAnvilAsset(manager.generatedFloors);

            KeyValuePair<string, FloorData> first = manager.generatedFloors
                .FirstOrDefault(pair => pair.Value != null && pair.Value.nodeProgress == 1 &&
                                        IsPlayable(pair.Value.name));
            if (first.Value == null)
                return;

            Done = true;

            if (manager.generatedFloors.Any(pair => pair.Value != null &&
                                                    pair.Value.stageName == first.Value.stageName &&
                                                    pair.Value.nodeProgress == 1 &&
                                                    pair.Value.mainEventType == EFloorMainEventType.Anvil))
                return;

            KeyValuePair<string, FloorData> anvil = manager.generatedFloors.FirstOrDefault(pair =>
                pair.Value != null && pair.Value.stageName == first.Value.stageName &&
                pair.Value.nodeProgress > 1 && pair.Value.mainEventType == EFloorMainEventType.Anvil);

            if (anvil.Value != null)
            {
                Swap(first.Value, anvil.Value);
                manager.generatedFloors[first.Key] = first.Value;
                manager.generatedFloors[anvil.Key] = anvil.Value;
                return;
            }

            if (KnownAnvilAsset == 0)
                return;

            first.Value.mainEventType = EFloorMainEventType.Anvil;
            first.Value.threatType = EFloorThreatType.Peace;
            first.Value.prefabAssetId = KnownAnvilAsset;
            first.Value.isAnvilFirstReward = true;
            manager.generatedFloors[first.Key] = first.Value;
        }

        private static void RememberAnvilAsset(SyncDictionary<string, FloorData> floors)
        {
            if (KnownAnvilAsset != 0)
                return;

            foreach (KeyValuePair<string, FloorData> pair in floors)
            {
                if (pair.Value != null && pair.Value.mainEventType == EFloorMainEventType.Anvil &&
                    pair.Value.prefabAssetId != 0)
                {
                    KnownAnvilAsset = pair.Value.prefabAssetId;
                    return;
                }
            }
        }

        private static void Swap(FloorData first, FloorData anvil)
        {
            EFloorMainEventType eventType = first.mainEventType;
            EFloorThreatType threatType = first.threatType;
            uint prefabAssetId = first.prefabAssetId;
            bool isAnvilFirstReward = first.isAnvilFirstReward;

            first.mainEventType = anvil.mainEventType;
            first.threatType = anvil.threatType;
            first.prefabAssetId = anvil.prefabAssetId;
            first.isAnvilFirstReward = true;

            anvil.mainEventType = eventType;
            anvil.threatType = threatType;
            anvil.prefabAssetId = prefabAssetId;
            anvil.isAnvilFirstReward = isAnvilFirstReward;
        }

        private static bool IsPlayable(string floorName)
        {
            string name = (floorName ?? string.Empty).ToLowerInvariant();
            return !name.Contains("entrance") && !name.Contains("town") && !name.Contains("station");
        }

        [HarmonyPatch(typeof(Anvil), "HandleInteraction")]
        private static class AnvilChoices
        {
            [HarmonyPrefix]
            private static void Before(Anvil __instance, GameObject actor, out int __state)
            {
                __state = -1;
                if (!ModSettings.FirstAnvilEnabled || __instance == null || actor == null)
                    return;

                WeaponControllerSimple controller = actor.GetComponent<WeaponControllerSimple>();
                if (controller == null || controller.currentWeapon == null)
                    return;

                List<EnhancementMetadata> enhancements =
                    WeaponDatabase.GetWeaponEnhancements(controller.currentWeapon.entityId);
                if (enhancements == null || enhancements.Count == 0)
                    return;

                __state = __instance.enhanceSlotCount;
                __instance.enhanceSlotCount = enhancements.Count;
            }

            [HarmonyFinalizer]
            private static System.Exception After(Anvil __instance, System.Exception __exception, int __state)
            {
                if (__instance != null && __state >= 0)
                    __instance.enhanceSlotCount = __state;

                return __exception;
            }
        }

        [HarmonyPatch(typeof(UI_WeaponEnhancementPanel), nameof(UI_WeaponEnhancementPanel.Open),
            new[] { typeof(Anvil), typeof(IAnvilSound) })]
        private static class AnvilPanel
        {
            [HarmonyPostfix]
            private static void After(UI_WeaponEnhancementPanel __instance)
            {
                if (__instance == null || __instance.rerollButtonGroup == null)
                    return;

                __instance.rerollButtonGroup.gameObject.SetActive(!ModSettings.FirstAnvilEnabled);
            }
        }

        [HarmonyPatch(typeof(Anvil), nameof(Anvil.Reroll))]
        private static class AnvilReroll
        {
            [HarmonyPrefix]
            private static bool Before()
            {
                return !ModSettings.FirstAnvilEnabled;
            }
        }
    }
}
