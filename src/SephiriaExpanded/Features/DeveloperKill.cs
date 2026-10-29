using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    [HarmonyPatch(typeof(RandomEnemyPhaseSpawner), "StartSpawn")]
    internal static class DeveloperKill
    {
        private const float Interval = 0.5f;

        private static float nextTime;

        internal static bool Enabled;

        internal static void Tick()
        {
            if (!Enabled || !NetworkServer.active || Time.unscaledTime < nextTime) return;

            nextTime = Time.unscaledTime + Interval;
            PlayerAvatar player = FindPlayer();
            if (player == null || CombatManager.Instance == null || RuntimeFactionManager.Instance == null) return;

            List<UnitAvatar> creatures = CombatManager.Instance.AllCreatures;
            for (int i = creatures.Count - 1; i >= 0; i--)
            {
                UnitAvatar unit = i < creatures.Count ? creatures[i] : null;
                if (IsTarget(unit, player)) Strike(unit);
            }
        }

        [HarmonyPostfix]
        private static void SkipPhases(RandomEnemyPhaseSpawner __instance)
        {
            if (!Enabled || !NetworkServer.active || __instance == null) return;

            __instance.CheatSkipRemainingPhases();
        }

        private static PlayerAvatar FindPlayer()
        {
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                PlayerAvatar avatar = spawner == null ? null : spawner.PlayerAvatar;
                if (avatar != null) return avatar;
            }

            return null;
        }

        private static bool IsTarget(UnitAvatar unit, PlayerAvatar player)
        {
            if (unit == null || unit.IsDead) return false;
            if (unit.monsterType != EMonsterType.Boss && unit.monsterType != EMonsterType.Miniboss) return false;

            return RuntimeFactionManager.Instance.GetRelationBehaviour(unit.faction, player.faction, unit.attackableTargetSelector) == ERelationBehaviour.Hostile;
        }

        private static void Strike(UnitAvatar unit)
        {
            DamageInstance damage = DamageInstance.GetDamage(null, "System", unit.transform.position, 4294967295L, Mathf.Max(unit.MaxHp, unit.hp) * 2f, EDamageType.Fall, EDamageFromType.None, Vector2.zero, 5, 1f);
            damage.isSystemDamage = true;
            unit.ApplyDamage(damage);
        }
    }
}
