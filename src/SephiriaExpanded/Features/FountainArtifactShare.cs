using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Mirror;

namespace SephiriaExpanded
{
    internal static class FountainArtifactShare
    {
        private static readonly FieldInfoAccessor Matrix = new FieldInfoAccessor(typeof(GridInventory), "inventoryMatrix");

        private sealed class FieldInfoAccessor
        {
            private readonly System.Reflection.FieldInfo field;
            internal FieldInfoAccessor(Type type, string name) { field = AccessTools.Field(type, name); }
            internal bool Available { get { return field != null; } }
            internal object Get(object target) { return field == null || target == null ? null : field.GetValue(target); }
        }

        [HarmonyPatch(typeof(PlayerSpawner), nameof(PlayerSpawner.AddDimensionPocketItemsOnServer))]
        private static class AddItems
        {
            [HarmonyPrefix]
            private static void Before(PlayerSpawner __instance, out HashSet<int> __state)
            {
                __state = null;
                if (!ModSettings.FountainArtifactShareEnabled || !NetworkServer.active || !Matrix.Available || __instance == null || __instance.PlayerAvatar == null || __instance.PlayerAvatar.Inventory == null)
                    return;

                __state = ReadInstanceIds(__instance.PlayerAvatar.Inventory);
            }

            [HarmonyPostfix]
            private static void After(PlayerSpawner __instance, HashSet<int> __state)
            {
                if (__state == null || !ModSettings.FountainArtifactShareEnabled || !NetworkServer.active || __instance == null)
                    return;

                HashSet<int> current = ReadInstanceIds(__instance.PlayerAvatar == null ? null : __instance.PlayerAvatar.Inventory);
                DungeonManager dungeon = DungeonManager.Instance;
                if (dungeon == null) return;

                foreach (int instanceId in current)
                {
                    if (__state.Contains(instanceId)) continue;
                    try
                    {
                        dungeon.UnboundOnServer(instanceId);
                        dungeon.OwnRestrictionOnServer(instanceId, ERestrictedOwnType.None);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static HashSet<int> ReadInstanceIds(GridInventory inventory)
        {
            HashSet<int> result = new HashSet<int>();
            if (inventory == null) return result;

            try
            {
                IDictionary dictionary = Matrix.Get(inventory) as IDictionary;
                if (dictionary == null) return result;

                foreach (DictionaryEntry entry in dictionary)
                {
                    object item = entry.Value;
                    if (item == null) continue;
                    System.Reflection.FieldInfo field = AccessTools.Field(item.GetType(), "InstanceID") ?? AccessTools.Field(item.GetType(), "instanceID");
                    object value = field == null ? null : field.GetValue(item);
                    if (field == null)
                    {
                        System.Reflection.PropertyInfo property = AccessTools.Property(item.GetType(), "InstanceID") ?? AccessTools.Property(item.GetType(), "instanceID");
                        if (property != null && property.GetIndexParameters().Length == 0)
                            value = property.GetValue(item, null);
                    }
                    if (value is int) result.Add((int)value);
                }
            }
            catch
            {
            }

            return result;
        }
    }
}
