using System;
using System.Collections.Generic;
using HarmonyLib;
using Mirror;

namespace SephiriaExpanded
{
    internal static class SellStartItem
    {
        private static readonly System.Reflection.FieldInfo StartingIds =
            AccessTools.Field(typeof(PlayerAvatar), "costumeStartingItemInstanceIDs");
        private static readonly HashSet<int> Released = new HashSet<int>();

        internal static void Tick()
        {
            if (!NetworkServer.active)
            {
                Released.Clear();
                return;
            }

            if (!ModSettings.SellStartItemEnabled)
            {
                Released.Clear();
                return;
            }

            List<PlayerSpawner> party = PlayerSpawner.MultiplayerList;
            if (party == null) return;

            for (int i = 0; i < party.Count; i++)
            {
                PlayerSpawner spawner = party[i];
                Release(spawner == null ? null : spawner.PlayerAvatar);
            }
        }

        private static void Release(PlayerAvatar player)
        {
            if (player == null || StartingIds == null) return;
            DungeonManager dungeon = DungeonManager.Instance;
            if (dungeon == null) return;

            try
            {
                IEnumerable<int> ids = StartingIds.GetValue(player) as IEnumerable<int>;
                if (ids == null) return;

                foreach (int instanceId in ids)
                {
                    if (instanceId <= 0 || Released.Contains(instanceId)) continue;
                    try
                    {
                        dungeon.OwnRestrictionOnServer(instanceId, ERestrictedOwnType.None);
                        Released.Add(instanceId);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        [HarmonyPatch(typeof(PlayerAvatar), "UpdateCostumeData", new[] { typeof(string), typeof(bool) })]
        private static class CostumeUpdated
        {
            [HarmonyPostfix]
            private static void After(PlayerAvatar __instance)
            {
                if (ModSettings.SellStartItemEnabled && NetworkServer.active)
                    Release(__instance);
            }
        }
    }
}
