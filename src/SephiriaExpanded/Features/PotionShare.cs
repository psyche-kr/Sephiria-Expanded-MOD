using System;
using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    [HarmonyPatch(typeof(GridInventory), "LocalAddItem", new[]
    {
        typeof(int), typeof(int), typeof(sbyte), typeof(int), typeof(bool), typeof(bool)
    })]
    internal static class PotionShare
    {
        private static readonly System.Random InstanceRandom = new System.Random();
        private static bool sharing;

        [HarmonyPostfix]
        private static void AfterAdd(GridInventory __instance, int entityID, sbyte quantity, bool __result)
        {
            if (!ModSettings.PotionShareEnabled || !NetworkServer.active || sharing ||
                !__result || quantity <= 0 || __instance == null)
                return;

            PlayerAvatar acquirer = __instance.UnitAvatar as PlayerAvatar;
            if (acquirer == null)
                return;

            ItemEntity item = ItemDatabase.FindItemById(entityID);
            if (item == null || item.type != EItemType.Potion)
                return;

            List<PlayerSpawner> party = PlayerSpawner.MultiplayerList;
            if (party == null || party.Count < 2)
                return;

            sharing = true;
            try
            {
                HashSet<PlayerAvatar> recipients = new HashSet<PlayerAvatar>();
                foreach (PlayerSpawner spawner in party)
                {
                    PlayerAvatar recipient = spawner == null ? null : spawner.PlayerAvatar;
                    if (recipient == null || recipient == acquirer || recipient.Inventory == null ||
                        !recipients.Add(recipient))
                        continue;

                    try
                    {
                        ItemMetadata copy = new ItemMetadata(
                            ItemDatabase.GenerateInstanceID(InstanceRandom), entityID, quantity);
                        recipient.Inventory.AddItem(copy, 0, true, false);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
            finally
            {
                sharing = false;
            }
        }
    }
}
