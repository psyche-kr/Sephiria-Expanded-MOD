using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class MoreInventory
    {
        private static readonly FieldInfo InventoryBonus =
            AccessTools.Field(typeof(LevelController), "inventoryBonus");
        private static readonly FieldInfo CurrentLevel =
            AccessTools.Field(typeof(LevelController), "currentLevel");
        private static readonly Dictionary<GridInventory, StorageGrant> GrantedStorage =
            new Dictionary<GridInventory, StorageGrant>();

        private sealed class StorageGrant
        {
            internal PlayerAvatar Avatar;
            internal int Amount;
        }

        internal static void RemoveGrantedStorage()
        {
            if (!NetworkServer.active || GrantedStorage.Count == 0)
                return;

            var grants = new List<KeyValuePair<GridInventory, StorageGrant>>(GrantedStorage);
            GrantedStorage.Clear();

            foreach (KeyValuePair<GridInventory, StorageGrant> grant in grants)
            {
                if (grant.Key == null || grant.Value == null || grant.Value.Amount <= 0)
                    continue;

                try
                {
                    int targetStorage = grant.Key.CurrentInventoryStorage - grant.Value.Amount;
                    if (DropOverflowItems(grant.Key, grant.Value.Avatar, targetStorage))
                        grant.Key.AddStorage((short)-grant.Value.Amount);
                }
                catch
                {
                }
            }
        }

        [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.RestartGame))]
        private static class RunRestart
        {
            [HarmonyPrefix]
            private static void Before()
            {
                RemoveGrantedStorage();
            }
        }

        [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStartServer))]
        private static class ServerStart
        {
            [HarmonyPostfix]
            private static void After()
            {
                GrantedStorage.Clear();
            }
        }

        [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStopServer))]
        private static class ServerStop
        {
            [HarmonyPostfix]
            private static void After()
            {
                GrantedStorage.Clear();
            }
        }

        private static bool IsMilestone(int level, int interval)
        {
            return interval > 0 && (level == 16 || level % interval == 0);
        }

        private static void RecordGrantedStorage(GridInventory inventory, PlayerAvatar avatar, int amount)
        {
            if (inventory == null || amount <= 0)
                return;

            StorageGrant grant;
            if (!GrantedStorage.TryGetValue(inventory, out grant))
            {
                grant = new StorageGrant();
                GrantedStorage.Add(inventory, grant);
            }

            grant.Avatar = avatar ?? grant.Avatar;
            grant.Amount += amount;
        }

        private static bool DropOverflowItems(GridInventory inventory, PlayerAvatar avatar, int targetStorage)
        {
            if (inventory == null || avatar == null || targetStorage < 1)
                return false;

            int currentStorage = inventory.CurrentInventoryStorage;
            int dropped = 0;

            for (int index = currentStorage - 1; index >= targetStorage; index--)
            {
                ItemPosition position = inventory.IdxToPos(index);
                NewItemOwnInstance item = inventory.FindItem(position);
                if (item == null)
                    continue;

                Vector3 offset = new Vector3((dropped % 3 - 1) * 0.35f, (dropped / 3) * 0.35f, 0f);
                inventory.ThrowItem(item.XIdx, item.YIdx, avatar.transform.position + offset);
                dropped++;

                if (inventory.FindItem(position) != null)
                    return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(LevelController), "LevelUpOnServer")]
        private static class LevelUp
        {
            [HarmonyPrefix]
            private static void Before(LevelController __instance, ref int __state)
            {
                __state = InventoryBonus == null ? 0 : (int)InventoryBonus.GetValue(__instance);

                if (ModSettings.MoreInventoryEnabled &&
                    ModSettings.MoreInventoryLevelInterval > 0 &&
                    InventoryBonus != null)
                {
                    InventoryBonus.SetValue(__instance, ModSettings.MoreInventoryLevelInterval);
                }
            }

            [HarmonyPostfix]
            private static void After(LevelController __instance, int __state)
            {
                try
                {
                    int level = CurrentLevel == null ? 0 : (int)CurrentLevel.GetValue(__instance);
                    int interval = ModSettings.MoreInventoryLevelInterval;
                    int slots = ModSettings.MoreInventorySlots;

                    if (!ModSettings.MoreInventoryEnabled || slots <= 0 || !IsMilestone(level, interval))
                        return;

                    bool originalAdded = level != 16 && level % interval == 0;
                    int extra = slots - (originalAdded ? 1 : 0);
                    GridInventory inventory = __instance.Avatar == null ? null : __instance.Avatar.Inventory;

                    if (extra > 0 && inventory != null)
                    {
                        inventory.AddStorage((short)extra);
                        RecordGrantedStorage(inventory, __instance.Avatar, extra);
                    }
                }
                finally
                {
                    if (InventoryBonus != null)
                        InventoryBonus.SetValue(__instance, __state);
                }
            }
        }

        [HarmonyPatch(typeof(LevelController), "set_NetworkcurrentLevel")]
        private static class LevelNotice
        {
            [HarmonyPrefix]
            private static void Before(LevelController __instance, ref int __state)
            {
                __state = CurrentLevel == null ? 0 : (int)CurrentLevel.GetValue(__instance);
            }

            [HarmonyPostfix]
            private static void After(LevelController __instance, int __state)
            {
                if (!ModSettings.MoreInventoryEnabled || !NetworkClient.active || __instance == null)
                    return;

                int level = CurrentLevel == null ? 0 : (int)CurrentLevel.GetValue(__instance);
                int slots = ModSettings.MoreInventorySlots;

                if (level <= __state || slots <= 0 || !IsMilestone(level, ModSettings.MoreInventoryLevelInterval))
                    return;

                PlayerAvatar avatar = __instance.Avatar;

                if (avatar == null || NetworkClient.localPlayer == null || avatar.netId != NetworkClient.localPlayer.netId)
                    return;

                string message = level + "레벨 달성하여 인벤토리가 " + slots + "칸 확장되었습니다.";

                foreach (UI_HUDLogViewer viewer in UnityEngine.Object.FindObjectsByType<UI_HUDLogViewer>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None))
                {
                    if (viewer != null)
                        viewer.SpawnLog(message, Color.green);
                }
            }
        }
    }
}
