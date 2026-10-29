using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Mirror;
using UnityEngine;
namespace SephiriaExpanded
{
    internal static class MonsterScaler
    {
        private const string BossPlayerHpKey = "bossBonusHpByPlayerNumber";
        private static readonly string[] PlayerHpKeys = { "enemyBonusHpByPlayerNumber", "minibossBonusHpByPlayerNumber", "miniEnemyBonusHpByPlayerNumber" };
        private static readonly ConditionalWeakTable<CommonEnemySpawner, HashSet<int>> phases = new ConditionalWeakTable<CommonEnemySpawner, HashSet<int>>();
        private static readonly ConditionalWeakTable<UnitAvatar, HpInfo> HpInfos = new ConditionalWeakTable<UnitAvatar, HpInfo>();
        private static bool pendingPlayerHp;

        private sealed class HpInfo
        {
            internal float Player = 1f;
            internal float Applied = 1f;
        }

        internal static float HpFactorOf(UnitAvatar unit)
        {
            HpInfo info;
            return unit != null && HpInfos.TryGetValue(unit, out info) ? info.Applied : 1f;
        }

        internal static float PlayerHpFactorOf(UnitAvatar unit)
        {
            HpInfo info;
            if (unit != null && HpInfos.TryGetValue(unit, out info)) return info.Player;
            return unit != null && unit.monsterType == EMonsterType.Boss ? BossPlayerHpFactor() : 1f;
        }

        private static float BossPlayerHpFactor()
        {
            int others = NetworkServer.connections == null ? 0 : NetworkServer.connections.Count - 1;
            return others > 0 ? 1f + others * KeywordDatabase.GetConstValue(BossPlayerHpKey) / 100f : 1f;
        }

        internal static float ExcludingPlayerBonus(float multiplier, float player)
        {
            return player > 1f ? (multiplier + player - 1f) / player : multiplier;
        }

        [HarmonyPatch(typeof(KeywordDatabase), nameof(KeywordDatabase.GetConstValue))]
        private static class PlayerHpConst
        {
            [HarmonyPostfix]
            private static void After(string key)
            {
                if (!NetworkServer.active || key == null) return;

                foreach (string playerKey in PlayerHpKeys)
                {
                    if (!string.Equals(key, playerKey, StringComparison.OrdinalIgnoreCase)) continue;

                    pendingPlayerHp = true;
                    return;
                }
            }
        }

        [HarmonyPatch(typeof(UnitAvatar), nameof(UnitAvatar.NetworkmaxHp), MethodType.Setter)]
        private static class PlayerHpSet
        {
            [HarmonyPrefix]
            private static void Before(UnitAvatar __instance, float value)
            {
                if (!pendingPlayerHp) return;

                pendingPlayerHp = false;
                if (__instance.maxHp <= 0f || value <= __instance.maxHp) return;

                HpInfos.GetOrCreateValue(__instance).Player *= value / __instance.maxHp;
            }
        }

        internal static int FloorAt(Vector3 position)
        {
            var manager = DungeonManager.Instance;
            if (manager == null || FloorGenerator.FloorGenerators == null) return 0;
            foreach (var floor in FloorGenerator.FloorGenerators)
            {
                if (floor == null || floor.isSafeFloor || floor.isTrainingFloor) continue;
                Vector3 distance = floor.transform.position - position;
                if (Mathf.Abs(distance.x) > 250 || Mathf.Abs(distance.y) > 250) continue;
                var data = floor.DataOnServer;
                if (data == null) return 0;
                int index = manager.sortedStages.IndexOf(data.stageName);
                return index >= 0 && index < 8 ? index + 1 : 0;
            }
            return 0;
        }
        private static int ComputeSpawnRepeats(float rate)
        {
            if (rate <= 0f) return 0;
            int whole = Mathf.FloorToInt(rate);
            float fraction = rate - whole;
            if (fraction > 0f && UnityEngine.Random.value < fraction) whole++;
            return whole;
        }

        [HarmonyPatch(typeof(CommonEnemySpawner), "ServerBeginSpawn")]
        private static class Spawn
        {
            [HarmonyPrefix]
            private static void Before(CommonEnemySpawner __instance, int phase)
            {
                if (!NetworkServer.active || __instance.spawnDatasByPhase == null
                    || phase < 0 || phase >= __instance.spawnDatasByPhase.Count) return;
                var done = phases.GetOrCreateValue(__instance);
                if (!done.Add(phase)) return;
                int floor = FloorAt(__instance.transform.position);
                if (floor == 0) return;
                float multiplier = ModSettings.GetMonsterScalerSpawnMultiplier(floor);
                float difficultySpawn = Difficulty.SpawnMultiplier(Difficulty.FloorLevel(floor));
                if (Mathf.Approximately(multiplier, 1f) && !ModSettings.MonsterScalerSpawnUsePerType && Mathf.Approximately(difficultySpawn, 1f)) return;
                var source = __instance.spawnDatasByPhase[phase];
                if (source == null || source.spawnDatas == null) return;
                var copy = new MonsterSpawnPhase { clearType = source.clearType, spawnDatas = new List<MonsterSpawnData>() };
                foreach (var entry in source.spawnDatas)
                {
                    if (entry == null) continue;
                    bool canMultiply = entry.monsterType == EMonsterType.Normal
                        || (entry.monsterType == EMonsterType.Miniboss && ModSettings.MonsterScalerMiniBossSpawnEnabled)
                        || (entry.monsterType == EMonsterType.Boss && ModSettings.MonsterScalerSpawnUsePerType);
                    float rate = canMultiply ? ModSettings.GetMonsterScalerSpawnMultiplier(floor, entry.monsterType) : 1f;
                    if (entry.monsterType == EMonsterType.Normal) rate = Difficulty.Effective(rate, difficultySpawn);
                    int repeats = ComputeSpawnRepeats(rate);
                    for (int i = 0; i < repeats; i++)
                    {
                        var clone = entry.GetClone();
                        clone.moneyDropPercent = entry.moneyDropPercent;
                        copy.spawnDatas.Add(clone);
                    }
                }
                __instance.spawnDatasByPhase[phase] = copy;
            }
        }
        [HarmonyPatch(typeof(UnitAvatar), "OnStartServer")]
        private static class Hp
        {
            [HarmonyPostfix]
            private static void After(UnitAvatar __instance)
            {
                if (!NetworkServer.active || __instance is PlayerAvatar || __instance.monsterType == EMonsterType.Dummy) return;
                __instance.StartCoroutine(ApplyHp(__instance));
            }
        }
        private static IEnumerator ApplyHp(UnitAvatar unit)
        {
            yield return null;
            if (unit == null || !NetworkServer.active || unit.maxHp <= 0) yield break;
            int floor = FloorAt(unit.transform.position);
            if (floor == 0) yield break;
            int difficulty = Difficulty.FloorLevel(floor);
            HpInfo info = HpInfos.GetOrCreateValue(unit);
            if (unit.monsterType == EMonsterType.Boss) info.Player = BossPlayerHpFactor();
            float hpMultiplier = ExcludingPlayerBonus(Difficulty.Effective(ModSettings.GetMonsterScalerHpMultiplier(floor, unit.monsterType), Difficulty.MonsterHpMultiplier(difficulty, unit.monsterType)), info.Player);
            info.Applied = hpMultiplier;
            if (!Mathf.Approximately(hpMultiplier, 1f))
            {
                unit.NetworkmaxHp = Math.Min(float.MaxValue, unit.maxHp * hpMultiplier);
                unit.Networkhp = Math.Min(unit.maxHp, unit.hp * hpMultiplier);
            }

            float speedMultiplier = Difficulty.Effective(ModSettings.GetMonsterScalerSpeedMultiplier(floor, unit.monsterType), Difficulty.MonsterSpeedMultiplier(difficulty, unit.monsterType));
            if (!Mathf.Approximately(speedMultiplier, 1f))
                unit.AddCustomStat(ECustomStat.AttackSpeed, Mathf.RoundToInt((speedMultiplier - 1f) * 100f));
        }

        internal static float ManualDamageMultiplier(UnitAvatar victim, UnitAvatar attacker)
        {
            if (!(victim is PlayerAvatar) || attacker == null || attacker is PlayerAvatar || attacker.monsterType == EMonsterType.Dummy) return 1f;
            int floor = FloorAt(attacker.transform.position);
            if (floor == 0) return 1f;
            float multiplier = ModSettings.GetMonsterScalerDamageMultiplier(attacker.monsterType, floor);
            return multiplier > 1f ? multiplier : 1f;
        }

        [HarmonyPatch(typeof(UnitAvatar), "ApplyDamage")]
        private static class Damage
        {
            [HarmonyPrefix]
            private static void Before(UnitAvatar __instance, DamageInstance __0, out float __state)
            {
                __state = float.NaN;
                if (!NetworkServer.active || __0 == null || __0.isSystemDamage) return;
                float multiplier = ManualDamageMultiplier(__instance, __0.origin as UnitAvatar);
                if (multiplier > 1f && __0.damage > 0)
                {
                    __state = __0.damage;
                    __0.damage = Math.Min(float.MaxValue, __0.damage * multiplier);
                }
            }

            [HarmonyFinalizer]
            private static Exception After(Exception __exception, DamageInstance __0, float __state)
            {
                if (__0 != null && !float.IsNaN(__state)) __0.damage = __state;
                return __exception;
            }
        }

        [HarmonyPatch(typeof(DropItemOnDie), "DropEXP")]
        private static class Experience
        {
            [HarmonyPrefix]
            private static void Before(UnitAvatar ___unitAvatar, ref int __0)
            {
                if (!NetworkServer.active || !ModSettings.MonsterScalerSpawnExpBalanceEnabled || __0 <= 0) return;
                var unit = ___unitAvatar;
                if (unit == null || unit is PlayerAvatar || unit.monsterType == EMonsterType.Dummy) return;
                int floor = FloorAt(unit.transform.position);
                if (floor == 0) return;
                float multiplier = ModSettings.GetMonsterScalerSpawnMultiplier(floor, unit.monsterType);
                if (multiplier > 0f && !Mathf.Approximately(multiplier, 1f))
                    __0 = Mathf.Max(1, Mathf.RoundToInt((float)__0 / multiplier));
            }
        }

        [HarmonyPatch(typeof(DropItemOnDie), "DropItems")]
        private static class LeafDrop
        {
            private static readonly FieldInfo DropUnit = AccessTools.Field(typeof(DropItemOnDie), "unitAvatar");
            private static readonly FieldInfo DropMoney = AccessTools.Field(typeof(DropItemOnDie), "dropMoney");

            private static float GetMultiplier(UnitAvatar unit)
            {
                if (!ModSettings.MonsterScalerSpawnExpBalanceEnabled || unit == null || unit is PlayerAvatar || unit.monsterType == EMonsterType.Dummy) return 1f;
                int floor = FloorAt(unit.transform.position);
                return floor == 0 ? 1f : ModSettings.GetMonsterScalerSpawnMultiplier(floor, unit.monsterType);
            }

            private static int GetBalancedMoney(UnitAvatar unit)
            {
                float multiplier = GetMultiplier(unit);
                return multiplier <= 0f || Mathf.Approximately(multiplier, 1f) ? unit.Money : Mathf.Max(0, Mathf.RoundToInt((float)unit.Money / multiplier));
            }

            private static int GetBalancedDropMoney(DropItemOnDie drop)
            {
                UnitAvatar unit = DropUnit == null ? null : DropUnit.GetValue(drop) as UnitAvatar;
                int amount = DropMoney == null ? 0 : (int)DropMoney.GetValue(drop);
                float multiplier = GetMultiplier(unit);
                return multiplier <= 0f || Mathf.Approximately(multiplier, 1f) ? amount : Mathf.Max(0, Mathf.RoundToInt((float)amount / multiplier));
            }

            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> BalanceLeafDrop(IEnumerable<CodeInstruction> instructions)
            {
                MethodInfo moneyGetter = AccessTools.Method(typeof(UnitAvatar), "get_Money");
                MethodInfo balancedMoney = AccessTools.Method(typeof(LeafDrop), "GetBalancedMoney");
                MethodInfo balancedDropMoney = AccessTools.Method(typeof(LeafDrop), "GetBalancedDropMoney");
                foreach (CodeInstruction instruction in instructions)
                {
                    if ((instruction.opcode == System.Reflection.Emit.OpCodes.Call || instruction.opcode == System.Reflection.Emit.OpCodes.Callvirt) && instruction.operand is MethodInfo method && method == moneyGetter)
                        instruction.operand = balancedMoney;
                    else if (instruction.opcode == System.Reflection.Emit.OpCodes.Ldfld && instruction.operand is FieldInfo field && field == DropMoney)
                    {
                        instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                        instruction.operand = balancedDropMoney;
                    }
                    yield return instruction;
                }
            }
        }
    }
}
