using System;
using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;
namespace SephiriaExpanded
{
    internal static class FountainCapacity
    {
        private const string Marker = "SephiriaExpanded/Capacity/";
        private sealed class Grant { internal GridInventory Inventory; internal int Amount; }
        private static readonly Dictionary<PlayerAvatar, Grant> grants = new Dictionary<PlayerAvatar, Grant>();
        internal static void Refresh()
        {
            if (!NetworkServer.active) { grants.Clear(); return; }
            var party = PlayerSpawner.MultiplayerList;
            if (party == null) return;
            foreach (var spawner in party)
            {
                var player = spawner == null ? null : spawner.PlayerAvatar;
                if (player == null || player.Inventory == null) continue;
                int desired = ModSettings.FountainCapacityEnabled ? Mathf.Clamp(ModSettings.FountainCapacityBonus, 0, 30) : 0;
                Grant old;
                if (!grants.TryGetValue(player, out old))
                    grants[player] = old = new Grant { Inventory = player.Inventory };
                if (old.Inventory != player.Inventory)
                {
                    if (old.Inventory != null) old.Inventory.NetworkdimensionPocket -= old.Amount;
                    old.Inventory = player.Inventory; old.Amount = 0;
                }
                if (old.Amount == desired) continue;
                player.Inventory.NetworkdimensionPocket = Mathf.Max(0, player.Inventory.dimensionPocket + desired - old.Amount);
                old.Amount = desired;
                var remove = new List<string>();
                foreach (string key in player.savableParameters)
                    if (key.StartsWith(Marker, StringComparison.Ordinal)) remove.Add(key);
                foreach (string key in remove) player.savableParameters.Remove(key);
                player.savableParameters.Add(Marker + desired);
            }
            var stale = new List<PlayerAvatar>();
            foreach (var pair in grants) if (pair.Key == null) stale.Add(pair.Key);
            foreach (var key in stale) grants.Remove(key);
        }
        internal static int Bonus(PlayerAvatar player)
        {
            if (player == null) return 0;
            foreach (string key in player.savableParameters)
                if (key.StartsWith(Marker, StringComparison.Ordinal))
                {
                    int value;
                    if (int.TryParse(key.Substring(Marker.Length), out value)) return Mathf.Clamp(value, 0, 32);
                }
            return 0;
        }
        internal static void Cleanup()
        {
            if (NetworkServer.active)
                foreach (var pair in grants)
                {
                    if (pair.Value.Inventory != null) pair.Value.Inventory.NetworkdimensionPocket -= pair.Value.Amount;
                    if (pair.Key == null) continue;
                    var remove = new List<string>();
                    foreach (string key in pair.Key.savableParameters) if (key.StartsWith(Marker, StringComparison.Ordinal)) remove.Add(key);
                    foreach (string key in remove) pair.Key.savableParameters.Remove(key);
                }
            grants.Clear();
        }
        [HarmonyPatch(typeof(UI_DimensionPocketPanel), "Open")]
        private static class Ui
        {
            [HarmonyPrefix]
            private static void Open(PlayerAvatar playerAvatar, ref int cachedDimensionPocketStorage)
            {
                if (NetworkServer.active) Refresh();
                if (playerAvatar == null || playerAvatar.Inventory == null) return;
                cachedDimensionPocketStorage = Math.Min(playerAvatar.Inventory.dimensionPocket,
                    KeywordDatabase.GetConstValue("DIMENSIONPOCKETLIMIT", true) + Bonus(playerAvatar));
            }
        }
        [HarmonyPatch(typeof(PlayerSpawner), nameof(PlayerSpawner.AddDimensionPocketItemsOnServer))]
        private static class Limit
        {
            [HarmonyPrefix]
            private static bool Add(PlayerSpawner __instance, int[] items)
            {
                if (!NetworkServer.active || __instance == null || __instance.PlayerAvatar == null) return true;
                Refresh();
                var player = __instance.PlayerAvatar;
                int bonus = Bonus(player);
                if (bonus == 0) return true;
                int left = Math.Min(player.Inventory.dimensionPocket, KeywordDatabase.GetConstValue("DIMENSIONPOCKETLIMIT", true) + bonus);
                var list = new List<ItemMetadata>();
                var random = new System.Random();
                if (items != null) foreach (int id in items)
                {
                    var entity = ItemDatabase.FindItemById(id);
                    if (entity == null) continue;
                    int cost = UI_DimensionPocketPanel.GetCapacity(entity.rarity);
                    if (cost < 0 || cost > left) break;
                    left -= cost;
                    list.Add(new ItemMetadata(ItemDatabase.GenerateInstanceID(random), id, 1));
                }
                if (list.Count != 0)
                {
                    player.Inventory.AddItems(list.ToArray(), false, false);
                    foreach (var item in list) DungeonManager.Instance.BoundOnServer(item.instanceID, __instance.currentPlayerIdx);
                }
                return false;
            }
        }
    }
}
