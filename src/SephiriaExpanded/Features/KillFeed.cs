using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class KillFeed
    {
        private const float PendingLifetime = 5f;

        private struct KillMessage : NetworkMessage
        {
            public uint VictimId;
            public uint KillerId;
        }

        private static readonly Dictionary<uint, KeyValuePair<uint, float>> Pending = new Dictionary<uint, KeyValuePair<uint, float>>();

        internal static void RegisterSerializers()
        {
            Writer<KillMessage>.write = (writer, message) =>
            {
                writer.WriteUInt(message.VictimId);
                writer.WriteUInt(message.KillerId);
            };
            Reader<KillMessage>.read = reader => new KillMessage { VictimId = reader.ReadUInt(), KillerId = reader.ReadUInt() };
        }

        internal static void RegisterClientHandlers()
        {
            NetworkClient.ReplaceHandler<KillMessage>(message => Remember(message.VictimId, message.KillerId), false);
        }

        private static void Remember(uint victimId, uint killerId)
        {
            Pending[victimId] = new KeyValuePair<uint, float>(killerId, Time.unscaledTime);
        }

        private static bool TryTake(uint victimId, out uint killerId)
        {
            killerId = 0;
            KeyValuePair<uint, float> entry;
            if (!Pending.TryGetValue(victimId, out entry)) return false;

            Pending.Remove(victimId);
            killerId = entry.Key;
            return Time.unscaledTime - entry.Value <= PendingLifetime;
        }

        private static string Plain(UnitAvatar unit)
        {
            string name = unit.Name;
            return unit is PlayerAvatar ? name : KeywordDatabase.Convert(name, useColor: false, useSprite: false);
        }

        private static void Write(PlayerAvatar victim, uint killerId)
        {
            if (GameLogWriter.Instance == null) return;

            NetworkIdentity identity;
            UnitAvatar killer = NetworkClient.spawned.TryGetValue(killerId, out identity) && identity != null ? identity.GetComponent<UnitAvatar>() : null;
            string victimName = Plain(victim);
            if (killer == null)
            {
                GameLogWriter.Instance.WriteLog(victimName + "님이 사망했습니다.", Color.red);
                return;
            }

            string killerName = Plain(killer);
            string line = killer is PlayerAvatar
                ? killerName + "님이 " + victimName + "님을 처치했습니다. (팀킬)"
                : victimName + "님이 " + killerName + "에게 처치당했습니다.";
            GameLogWriter.Instance.WriteLog(line, Color.red);
        }

        [HarmonyPatch(typeof(UnitAvatar), nameof(UnitAvatar.Die))]
        private static class Announce
        {
            [HarmonyPrefix]
            private static void Before(UnitAvatar __instance, DamageInstance diedFrom)
            {
                if (!NetworkServer.active || !ModSettings.FriendlyFireEnabled || !ModSettings.KillLogEnabled || !(__instance is PlayerAvatar) || __instance.IsDead || diedFrom == null) return;

                UnitAvatar killer = diedFrom.origin as UnitAvatar;
                if (killer == null || killer == __instance) return;

                if (NetworkClient.active) Remember(__instance.netId, killer.netId);
                MoreMapCompatibility.SendToModClients(new KillMessage { VictimId = __instance.netId, KillerId = killer.netId });
            }
        }

        [HarmonyPatch(typeof(PlayerSpawner), "HandleDieClientside")]
        private static class Replace
        {
            [HarmonyPrefix]
            private static bool Before(PlayerSpawner __instance)
            {
                PlayerAvatar victim = __instance.PlayerAvatar;
                uint killerId;
                if (victim == null || !TryTake(victim.netId, out killerId)) return true;

                Write(victim, killerId);
                return victim.isLocalPlayer;
            }
        }
    }
}
