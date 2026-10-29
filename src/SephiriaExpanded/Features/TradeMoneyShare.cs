using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using HarmonyLib;

namespace SephiriaExpanded
{
    public sealed class SharedStat
    {
        public readonly ECustomStat Stat;
        public readonly string Key;

        public SharedStat(ECustomStat stat, string key)
        {
            Stat = stat;
            Key = key;
        }
    }

    public class TradeMoneyShare : MonoBehaviour
    {
        private const string LogPrefix = "[TradeMoneyShare] ";
        private const string SharedSavePrefix = "TradeMoneyShare/HaveTraded/";
        private const float BindInterval = 1f;
        private const float StatSyncInterval = 1f;

        private static readonly SharedStat MoneyDropShare =
            new SharedStat(ECustomStat.MoneyDrop, "MONEYDROP");

        private static readonly SharedStat ExpDropShare =
            new SharedStat(ECustomStat.EXPDrop, "EXPDROP");

        private static readonly SharedStat NegotiationShare =
            new SharedStat(ECustomStat.Negotiation, "NEGOTIATION");

        private readonly Dictionary<PlayerAvatar, Action<string>> tradeHooks =
            new Dictionary<PlayerAvatar, Action<string>>();

        private readonly Dictionary<PlayerAvatar, Dictionary<SharedStat, int>> sharedGrants =
            new Dictionary<PlayerAvatar, Dictionary<SharedStat, int>>();

        private float bindTimer;
        private float statSyncTimer;

        private void Update()
        {
            if (!NetworkServer.active)
                return;

            if (!ModSettings.TradeMoneyShareEnabled) { Cleanup(); return; }

            bindTimer += Time.deltaTime;
            if (bindTimer >= BindInterval)
            {
                bindTimer = 0f;
                BindPartyTradeEvents();
            }

            statSyncTimer += Time.deltaTime;
            if (statSyncTimer >= StatSyncInterval)
            {
                statSyncTimer = 0f;
                SyncSharedStats();
            }
        }

        private void BindPartyTradeEvents()
        {
            foreach (PlayerAvatar player in GetParty())
            {
                if (tradeHooks.ContainsKey(player))
                    continue;

                PlayerAvatar capturedPlayer = player;
                Action<string> handler = merchantId => OnTradeBegin(capturedPlayer, merchantId);

                try
                {
                    player.OnTradeBeginServerside += handler;
                    tradeHooks.Add(player, handler);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(LogPrefix + exception.Message);
                }
            }
        }

        private static void OnTradeBegin(PlayerAvatar initiator, string merchantId)
        {
            if (!ModSettings.TradeMoneyShareEnabled || initiator == null || string.IsNullOrEmpty(merchantId))
                return;

            List<PlayerAvatar> party = GetParty();
            if (party.Count == 0)
                return;

            string sharedKey = SharedSavePrefix + merchantId;
            Dictionary<PlayerAvatar, int> ownBase = new Dictionary<PlayerAvatar, int>();
            int bestBase = 0;

            foreach (PlayerAvatar player in party)
            {
                int value = GetOwnTradeMoneyBase(player);
                ownBase[player] = value;
                if (value > bestBase)
                    bestBase = value;
            }

            if (bestBase <= 0)
                return;

            foreach (PlayerAvatar player in party)
            {
                if (player.savableParameters.Contains(sharedKey) || player.savableParameters.Contains("HaveTraded/" + merchantId))
                    continue;

                player.savableParameters.Add(sharedKey);
                player.savableParameters.Add("HaveTraded/" + merchantId);

                int alreadyReceived = 0;
                int baseToAdd = bestBase - alreadyReceived;

                if (baseToAdd <= 0)
                    continue;

                int finalAmount = baseToAdd +
                    (int)((float)baseToAdd * player.GetCustomStat(ECustomStat.MoneyDrop) / 100f);

                player.AddMoney(finalAmount);
                Debug.Log($"{LogPrefix} + {player.Name}���� {finalAmount} ���� ���� �޾ҽ��ϴ�.");
            }
        }

        private static int GetOwnTradeMoneyBase(PlayerAvatar player)
        {
            int best = 0;

            Charm_TradeMoney[] charms = UnityEngine.Object.FindObjectsByType<Charm_TradeMoney>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (Charm_TradeMoney charm in charms)
            {
                if (charm == null || !charm.isActiveAndEnabled || charm.Avatar != player || charm.addMoneyByLevel == null)
                    continue;

                if (charm.addMoneyByLevel.Length == 0)
                    continue;

                int index = Mathf.Clamp(
                    charm.CurrentLevelToIdx(),
                    0,
                    charm.addMoneyByLevel.Length - 1);

                best = Mathf.Max(best, charm.addMoneyByLevel[index]);
            }

            return best;
        }

        private void SyncSharedStats()
        {
            List<PlayerAvatar> party = GetParty();
            RemoveGrantsFromPlayersWhoLeft(party);

            if (party.Count == 0)
                return;

            SyncOneSharedStat(party, MoneyDropShare);
            SyncOneSharedStat(party, ExpDropShare);
            SyncOneSharedStat(party, NegotiationShare);
        }

        private void SyncOneSharedStat(List<PlayerAvatar> party, SharedStat stat)
        {
            foreach (PlayerAvatar player in party)
                RemoveGrant(player, stat);

            int partyBestEffectiveValue = Int32.MinValue;
            foreach (PlayerAvatar player in party)
                partyBestEffectiveValue = Mathf.Max(
                    partyBestEffectiveValue,
                    player.GetCustomStat(stat.Stat));

            foreach (PlayerAvatar player in party)
            {
                int currentEffectiveValue = player.GetCustomStat(stat.Stat);
                if (currentEffectiveValue >= partyBestEffectiveValue)
                    continue;

                int grant = GetMinimumRawGrantToReach(
                    player,
                    stat,
                    partyBestEffectiveValue);

                if (grant <= 0)
                    continue;

                player.AddCustomStat(stat.Stat, grant);
                SetGrant(player, stat, grant);
            }
        }

        private static int GetMinimumRawGrantToReach(
            PlayerAvatar player,
            SharedStat stat,
            int targetEffectiveValue)
        {
            int rawValue = GetRawStatBeforeAmplification(player, stat.Key);
            int multiplier = 100 + player.GetCustomStatAmp(stat.Key);

            if (multiplier <= 0)
                return 0;

            int requiredRawValue = (int)Math.Ceiling(
                (double)targetEffectiveValue * 100.0 / multiplier);

            return Math.Max(0, requiredRawValue - rawValue);
        }

        private static int GetRawStatBeforeAmplification(PlayerAvatar player, string key)
        {
            int customValue;
            int calculatedValue;

            if (!player.customStats.TryGetValue(key, out customValue))
                customValue = 0;

            if (!player.calculatedBonusStats.TryGetValue(key, out calculatedValue))
                calculatedValue = 0;

            return customValue + calculatedValue;
        }

        private void RemoveGrantsFromPlayersWhoLeft(List<PlayerAvatar> party)
        {
            List<PlayerAvatar> stalePlayers = new List<PlayerAvatar>();

            foreach (KeyValuePair<PlayerAvatar, Dictionary<SharedStat, int>> pair in sharedGrants)
            {
                if (pair.Key != null && !party.Contains(pair.Key))
                {
                    foreach (KeyValuePair<SharedStat, int> grant in pair.Value)
                        pair.Key.AddCustomStat(grant.Key.Stat, -grant.Value);

                    stalePlayers.Add(pair.Key);
                }
            }

            foreach (PlayerAvatar player in stalePlayers)
                sharedGrants.Remove(player);
        }

        private void RemoveGrant(PlayerAvatar player, SharedStat stat)
        {
            Dictionary<SharedStat, int> grants;
            int amount;

            if (!sharedGrants.TryGetValue(player, out grants) ||
                !grants.TryGetValue(stat, out amount))
                return;

            player.AddCustomStat(stat.Stat, -amount);
            grants.Remove(stat);

            if (grants.Count == 0)
                sharedGrants.Remove(player);
        }

        private void SetGrant(PlayerAvatar player, SharedStat stat, int amount)
        {
            Dictionary<SharedStat, int> grants;
            if (!sharedGrants.TryGetValue(player, out grants))
            {
                grants = new Dictionary<SharedStat, int>();
                sharedGrants.Add(player, grants);
            }

            grants[stat] = amount;
        }

        private static List<PlayerAvatar> GetParty()
        {
            List<PlayerAvatar> party = new List<PlayerAvatar>();
            List<PlayerSpawner> spawners = PlayerSpawner.MultiplayerList;

            if (spawners == null)
                return party;

            foreach (PlayerSpawner spawner in spawners)
            {
                if (spawner != null && spawner.PlayerAvatar != null)
                    party.Add(spawner.PlayerAvatar);
            }

            return party;
        }

        private void OnDestroy() { Cleanup(); }

        [HarmonyPatch(typeof(Charm_TradeMoney), "HandleTradeBegin")]
        private static class NativeReward
        {
            [HarmonyPrefix]
            private static bool Before(Charm_TradeMoney __instance, string socialID)
            {
                if (!NetworkServer.active || !ModSettings.TradeMoneyShareEnabled) return true;
                OnTradeBegin(__instance.Avatar as PlayerAvatar, socialID);
                return false;
            }
        }

        private void Cleanup()
        {
            foreach (KeyValuePair<PlayerAvatar, Dictionary<SharedStat, int>> pair in sharedGrants)
            {
                if (pair.Key == null)
                    continue;

                foreach (KeyValuePair<SharedStat, int> grant in pair.Value)
                    pair.Key.AddCustomStat(grant.Key.Stat, -grant.Value);
            }
            sharedGrants.Clear();

            foreach (KeyValuePair<PlayerAvatar, Action<string>> pair in tradeHooks)
            {
                if (pair.Key != null)
                    pair.Key.OnTradeBeginServerside -= pair.Value;
            }
            tradeHooks.Clear();
        }
    }
}
