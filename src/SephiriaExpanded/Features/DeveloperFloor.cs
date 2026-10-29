using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    [HarmonyPatch]
    internal static class DeveloperFloor
    {
        private const float Timeout = 60f;
        private const float PollInterval = 0.25f;

        private static readonly Type[] SpawnerTypes = { typeof(EnemySpawner), typeof(CommonEnemySpawner), typeof(RandomEnemyPhaseSpawner) };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (Type type in SpawnerTypes)
                yield return AccessTools.DeclaredMethod(type, "OnStopServer");
        }

        [HarmonyPostfix]
        private static void ReleaseSpawned(NetworkBehaviour __instance)
        {
            if (!DeveloperMode.Activated || !NetworkServer.active || HorayNetworkManager.IsShuttingDownNow) return;

            List<UnitAvatar> spawned = AccessTools.Field(__instance.GetType(), "spawned")?.GetValue(__instance) as List<UnitAvatar>;
            if (spawned == null) return;

            foreach (UnitAvatar unit in spawned)
                if (unit != null) NetworkServer.Destroy(unit.gameObject);

            spawned.Clear();
        }

        internal static IEnumerator EnterMiniBossRoom(string guid)
        {
            float deadline = Time.unscaledTime + Timeout;
            while (Time.unscaledTime < deadline)
            {
                yield return new WaitForSecondsRealtime(PollInterval);
                if (!AllArrived(guid)) continue;

                Vector3? position = FindMiniBossPosition(guid);
                if (position.HasValue) Teleport(position.Value);
                else DeveloperMode.ShowMessage("미니보스 방을 찾지 못했습니다.");
                yield break;
            }
        }

        private static bool AllArrived(string guid)
        {
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                PlayerAvatar avatar = spawner == null ? null : spawner.PlayerAvatar;
                if (avatar != null && (avatar.currentFloorGuid != guid || avatar.loadingScreenType != -1)) return false;
            }

            return true;
        }

        private static Vector3? FindMiniBossPosition(string guid)
        {
            FloorGenerator floor = FloorGenerator.FloorGenerators.Find(generator => generator != null && generator.guid == guid);
            if (floor == null) return null;

            foreach (GameObject prop in floor.floorRelatedNetworkObjects)
            {
                if (prop == null) continue;

                if (prop.TryGetComponent(out EnemySpawner enemy))
                {
                    MonsterSpawnData data = FindMiniBoss(enemy.spawnDatasByPhase);
                    if (data != null) return GetPosition(enemy.transform, enemy.detectArea_lb, enemy.detectArea_rt, data);
                }

                if (prop.TryGetComponent(out CommonEnemySpawner common))
                {
                    MonsterSpawnData data = FindMiniBoss(common.spawnDatasByPhase);
                    if (data != null) return GetPosition(common.transform, common.detectArea_lb, common.detectArea_rt, data);
                }
            }

            return null;
        }

        private static MonsterSpawnData FindMiniBoss(List<MonsterSpawnPhase> phases)
        {
            foreach (MonsterSpawnPhase phase in phases)
                foreach (MonsterSpawnData data in phase.spawnDatas)
                    if (data.monsterType == EMonsterType.Miniboss) return data;

            return null;
        }

        private static Vector3 GetPosition(Transform spawner, Vector2 detectLeftBottom, Vector2 detectRightTop, MonsterSpawnData data)
        {
            Vector2 offset = detectLeftBottom == detectRightTop ? data.position : (detectLeftBottom + detectRightTop) * 0.5f;
            return spawner.position + (Vector3)offset;
        }

        private static void Teleport(Vector3 position)
        {
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                PlayerAvatar avatar = spawner == null ? null : spawner.PlayerAvatar;
                if (avatar != null) avatar.ReqSetPosition(position + (Vector3)UnityEngine.Random.insideUnitCircle, teleport: true);
            }
        }
    }
}
