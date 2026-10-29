using System;
using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class WalletShare
    {
        private sealed class Ledger
        {
            internal readonly bool IsLeaf;
            internal readonly Dictionary<PlayerAvatar, int> Personal = new Dictionary<PlayerAvatar, int>();
            internal readonly Dictionary<PlayerAvatar, int> Batch = new Dictionary<PlayerAvatar, int>();
            internal bool Active;

            internal Ledger(bool isLeaf)
            {
                IsLeaf = isLeaf;
            }

            internal int Pool
            {
                get
                {
                    int sum = 0;
                    foreach (int value in Personal.Values)
                        sum += value;

                    return sum;
                }
            }
        }

        private static readonly Ledger Leaf = new Ledger(true);
        private static readonly Ledger Dice = new Ledger(false);
        private static readonly HashSet<PlayerSpawner> Initializing = new HashSet<PlayerSpawner>();
        private static readonly List<PlayerAvatar> Gone = new List<PlayerAvatar>();
        private static bool writing;
        private static int absoluteDepth;
        private static int refillDepth;
        private static int batchDepth;
        private static PlayerAvatar batchTarget;

        internal static void Tick()
        {
            Sync(Leaf);
            Sync(Dice);
        }

        private static bool Wanted(Ledger ledger)
        {
            return NetworkServer.active && ModSettings.WalletShareEnabled && (ledger.IsLeaf ? ModSettings.WalletShareLeaf : ModSettings.WalletShareDice);
        }

        private static int Read(Ledger ledger, PlayerAvatar player)
        {
            return ledger.IsLeaf ? player.currentMoney : player.rerollDice;
        }

        private static void Write(Ledger ledger, PlayerAvatar player, int value)
        {
            writing = true;
            try
            {
                if (ledger.IsLeaf) player.NetworkcurrentMoney = value;
                else player.NetworkrerollDice = Mathf.Max(0, value);
            }
            finally
            {
                writing = false;
            }
        }

        private static void Sync(Ledger ledger)
        {
            if (!Wanted(ledger))
            {
                if (ledger.Active) Release(ledger);
                return;
            }

            ledger.Active = true;
            bool changed = Prune(ledger);
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                PlayerAvatar player = spawner != null ? spawner.PlayerAvatar : null;
                if (player == null || ledger.Personal.ContainsKey(player) || Initializing.Contains(spawner)) continue;

                ledger.Personal[player] = Read(ledger, player);
                changed = true;
            }

            if (changed) Broadcast(ledger);
        }

        private static bool Prune(Ledger ledger)
        {
            Gone.Clear();
            int orphan = 0;
            foreach (KeyValuePair<PlayerAvatar, int> pair in ledger.Personal)
            {
                if (pair.Key != null) continue;

                Gone.Add(pair.Key);
                orphan += pair.Value;
            }

            foreach (PlayerAvatar player in Gone)
                ledger.Personal.Remove(player);

            if (Gone.Count == 0) return false;

            Distribute(ledger, orphan, null);
            return true;
        }

        private static void Release(Ledger ledger)
        {
            foreach (KeyValuePair<PlayerAvatar, int> pair in ledger.Personal)
            {
                if (pair.Key != null) Write(ledger, pair.Key, Mathf.Max(0, pair.Value));
            }

            ledger.Personal.Clear();
            ledger.Batch.Clear();
            ledger.Active = false;
        }

        private static void Broadcast(Ledger ledger)
        {
            int pool = ledger.Pool;
            foreach (PlayerAvatar player in new List<PlayerAvatar>(ledger.Personal.Keys))
            {
                if (player != null && Read(ledger, player) != pool) Write(ledger, player, pool);
            }
        }

        private static void Distribute(Ledger ledger, int amount, PlayerAvatar first)
        {
            int count = ledger.Personal.Count;
            if (count == 0 || amount == 0) return;

            int share = amount / count;
            int remainder = amount - share * count;
            foreach (PlayerAvatar player in new List<PlayerAvatar>(ledger.Personal.Keys))
                ledger.Personal[player] += share;

            if (remainder == 0) return;

            PlayerAvatar receiver = first != null && ledger.Personal.ContainsKey(first) ? first : null;
            if (receiver == null)
            {
                foreach (PlayerAvatar player in ledger.Personal.Keys)
                {
                    receiver = player;
                    break;
                }
            }

            ledger.Personal[receiver] += remainder;
        }

        private static void Apply(Ledger ledger, PlayerAvatar player, int delta)
        {
            if (delta >= 0)
            {
                ledger.Personal[player] += delta;
                return;
            }

            int need = -delta;
            int own = Mathf.Max(0, Mathf.Min(ledger.Personal[player], need));
            ledger.Personal[player] -= own;
            need -= own;
            while (need > 0)
            {
                PlayerAvatar richest = null;
                foreach (KeyValuePair<PlayerAvatar, int> pair in ledger.Personal)
                {
                    if (pair.Key != player && pair.Value > 0 && (richest == null || pair.Value > ledger.Personal[richest])) richest = pair.Key;
                }

                if (richest == null) break;

                int take = Mathf.Min(ledger.Personal[richest], need);
                ledger.Personal[richest] -= take;
                need -= take;
            }

            ledger.Personal[player] -= need;
        }

        private static bool Intercept(Ledger ledger, PlayerAvatar player, int value)
        {
            if (writing || player == null || !ledger.Active || !NetworkServer.active || !ledger.Personal.ContainsKey(player)) return true;

            if (absoluteDepth > 0 && ledger.IsLeaf)
            {
                ledger.Personal[player] = value;
                Broadcast(ledger);
                return false;
            }

            if (refillDepth > 0 && !ledger.IsLeaf)
            {
                ledger.Personal[player] = Mathf.Max(ledger.Personal[player], player.maxRerollDice);
                Broadcast(ledger);
                return false;
            }

            int delta = value - Read(ledger, player);
            if (delta == 0) return false;

            if (batchDepth > 0 && delta > 0)
            {
                int batched;
                ledger.Batch.TryGetValue(player, out batched);
                ledger.Batch[player] = batched + delta;
                return false;
            }

            Apply(ledger, player, delta);
            Broadcast(ledger);
            return false;
        }

        private static void FlushBatch(Ledger ledger)
        {
            if (ledger.Batch.Count == 0) return;

            int amount;
            if (batchTarget == null || !ledger.Batch.TryGetValue(batchTarget, out amount))
            {
                amount = 0;
                foreach (int value in ledger.Batch.Values)
                {
                    amount = value;
                    break;
                }
            }

            ledger.Batch.Clear();
            Distribute(ledger, amount, batchTarget);
            Broadcast(ledger);
        }

        private static void BeginBatch(PlayerAvatar target)
        {
            if (batchDepth++ == 0) batchTarget = target;
        }

        private static void EndBatch()
        {
            if (--batchDepth > 0) return;

            FlushBatch(Leaf);
            FlushBatch(Dice);
            batchTarget = null;
        }

        [HarmonyPatch(typeof(UnitAvatar), nameof(UnitAvatar.NetworkcurrentMoney), MethodType.Setter)]
        private static class MoneySetter
        {
            [HarmonyPrefix]
            private static bool Before(UnitAvatar __instance, int value)
            {
                return Intercept(Leaf, __instance as PlayerAvatar, value);
            }
        }

        [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.NetworkrerollDice), MethodType.Setter)]
        private static class DiceSetter
        {
            [HarmonyPrefix]
            private static bool Before(PlayerAvatar __instance, int value)
            {
                return Intercept(Dice, __instance, value);
            }
        }

        [HarmonyPatch]
        private static class AbsoluteMoney
        {
            [HarmonyTargetMethods]
            private static IEnumerable<System.Reflection.MethodBase> Targets()
            {
                yield return AccessTools.Method(typeof(UnitAvatar), nameof(UnitAvatar.SetMoney));
                yield return AccessTools.Method(typeof(UnitAvatar), "UserCode_CmdSetMoney__Int32");
            }

            [HarmonyPrefix]
            private static void Before()
            {
                absoluteDepth++;
            }

            [HarmonyFinalizer]
            private static Exception After(Exception __exception)
            {
                absoluteDepth--;
                return __exception;
            }
        }

        [HarmonyPatch(typeof(QuestController), "Reward")]
        private static class DiceRefill
        {
            [HarmonyPrefix]
            private static void Before()
            {
                refillDepth++;
            }

            [HarmonyFinalizer]
            private static Exception After(Exception __exception)
            {
                refillDepth--;
                return __exception;
            }
        }

        [HarmonyPatch(typeof(Money), "AddToInventory")]
        private static class LeafPickup
        {
            [HarmonyPrefix]
            private static void Before(PlayerAvatar target)
            {
                BeginBatch(target);
            }

            [HarmonyFinalizer]
            private static Exception After(Exception __exception)
            {
                EndBatch();
                return __exception;
            }
        }

        [HarmonyPatch(typeof(MagicFountain), "Update")]
        private static class FountainReward
        {
            [HarmonyPrefix]
            private static void Before()
            {
                BeginBatch(null);
            }

            [HarmonyFinalizer]
            private static Exception After(Exception __exception)
            {
                EndBatch();
                return __exception;
            }
        }

        [HarmonyPatch(typeof(PlayerSpawner), "Initialize")]
        private static class PlayerInitialize
        {
            [HarmonyPrefix]
            private static void Before(PlayerSpawner __instance)
            {
                if (!NetworkServer.active || __instance == null) return;

                Initializing.Add(__instance);
                PlayerAvatar player = __instance.PlayerAvatar;
                if (player == null) return;

                Detach(Leaf, player);
                Detach(Dice, player);
            }

            [HarmonyFinalizer]
            private static Exception After(PlayerSpawner __instance, Exception __exception)
            {
                Initializing.Remove(__instance);
                return __exception;
            }
        }

        private static void Detach(Ledger ledger, PlayerAvatar player)
        {
            int personal;
            if (!ledger.Personal.TryGetValue(player, out personal)) return;

            ledger.Personal.Remove(player);
            Write(ledger, player, personal);
            Broadcast(ledger);
        }
    }
}
