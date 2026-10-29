using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class BossInfo
    {
        private const string Usage = "사용법: //boss (현재 보스) / //boss 층 (그 층의 보스 전부) / //boss 층 번호 (예: //boss 1 2)";

        private sealed class Entry
        {
            internal int Stage;
            internal Type[] Types;
        }

        private static Entry Boss(int stage, params Type[] types)
        {
            return new Entry { Stage = stage, Types = types };
        }

        private static readonly Entry[][] Floors =
        {
            new[] { Boss(0, typeof(Unit_MoleChieftain)), Boss(0, typeof(Unit_MoleBigBomb)), Boss(0, typeof(Unit_RootDemon)) },
            new[] { Boss(1, typeof(Unit_OinkKing)), Boss(1, typeof(Unit_PantherRogueKing)) },
            new[] { Boss(2, typeof(Unit_MadArmadillo)), Boss(2, typeof(Unit_LibraryGuard)) },
            new[] { Boss(3, typeof(Unit_Askard_Chapter3)) },
            new[] { Boss(4, typeof(Unit_BirdDemon)), Boss(4, typeof(Unit_LizardDemon)), Boss(5, typeof(Unit_QBoss)) },
            new[] { Boss(6, typeof(Unit_QQBoss), typeof(Unit_QQQBoss)) }
        };

        internal static Type[] TypesFor(int stage, string name)
        {
            foreach (Entry[] floor in Floors)
            {
                foreach (Entry entry in floor)
                    if (entry.Stage == stage && entry.Types.Length > 0 && BossChallenge.DisplayName(entry.Types[0]) == name) return entry.Types;
            }

            return null;
        }

        internal static bool IsMain(UnitAvatar unit, Type type)
        {
            return unit != null && type.Name.StartsWith("Unit_", StringComparison.Ordinal) && Clean(unit) == type.Name.Substring(5);
        }

        internal static void Run(string text)
        {
            string[] tokens = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length <= 1)
            {
                ShowCurrent();
                return;
            }

            int floor;
            int index = 0;
            if (!int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out floor) || floor < 1 || floor > Floors.Length || (tokens.Length > 2 && !int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out index)))
            {
                DeveloperMode.ShowMessage(Usage);
                return;
            }

            Entry[] bosses = Floors[floor - 1];
            if (tokens.Length > 2 && (index < 1 || index > bosses.Length))
            {
                DeveloperMode.ShowMessage(floor + "층 보스는 " + bosses.Length + "종류입니다. 번호는 1~" + bosses.Length + "입니다.");
                return;
            }

            for (int i = 0; i < bosses.Length; i++)
            {
                if (index != 0 && index != i + 1) continue;

                foreach (Type type in bosses[i].Types)
                    ShowPrefab(floor, bosses[i].Stage, type);
            }
        }
        private static string Clean(UnityEngine.Object target)
        {
            return target == null ? string.Empty : target.name.Replace("(Clone)", string.Empty).Trim();
        }

        private static string Stagger(UnitAvatar unit)
        {
            return "저항 " + unit.staggerResist + " | 컷 " + unit.minStaggerResponseCut;
        }

        private static void ShowCurrent()
        {
            int shown = 0;
            foreach (UnitAvatar unit in UnityEngine.Object.FindObjectsByType<UnitAvatar>(FindObjectsSortMode.None))
            {
                if (unit == null || unit is PlayerAvatar || unit.IsDead || unit.monsterType != EMonsterType.Boss) continue;

                string display = BossChallenge.DisplayName(unit.GetType()) ?? unit.Name;
                DeveloperMode.ShowMessage("현재 보스: " + display + " (" + unit.GetType().Name + ") | 프리팹 " + Clean(unit) + " | 체력 " + Mathf.RoundToInt(unit.hp) + "/" + Mathf.RoundToInt(unit.MaxHp));
                DeveloperMode.ShowMessage("   " + Stagger(unit) + " | 슈퍼아머 " + Mathf.RoundToInt(unit.remainingSuperArmor) + "/" + Mathf.RoundToInt(unit.InitializedMaxSuperArmor));
                shown++;
            }

            if (shown == 0) DeveloperMode.ShowCurrentFloorBoss();
        }

        private static void ShowPrefab(int floor, int stage, Type type)
        {
            string title = floor + "층 보스 " + (BossChallenge.DisplayName(type) ?? type.Name) + " (" + type.Name + ")";
            List<UnitAvatar> prefabs = FindPrefabs(floor, stage, type);
            if (prefabs.Count == 0)
            {
                DeveloperMode.ShowMessage(title + " | 프리팹을 찾지 못했습니다.");
                return;
            }

            foreach (UnitAvatar unit in prefabs)
                DeveloperMode.ShowMessage(title + " | 프리팹 " + Clean(unit) + " | 체력 " + Mathf.RoundToInt(unit.maxHp) + " | " + Stagger(unit));
        }

        private static void Collect(GameObject floorPrefab, string stageName, Type type, List<UnitAvatar> candidates)
        {
            foreach (BossSpawner spawner in floorPrefab.GetComponentsInChildren<BossSpawner>(true))
            {
                GameObject prefab = spawner.bossSocialID == null ? null : spawner.bossSocialID.avatarPrefab;
                UnitAvatar unit = prefab == null ? null : prefab.GetComponent<UnitAvatar>();
                if (unit == null) continue;

                Debug.Log("[Sephiria Expanded][BossInfo] " + stageName + " / 방 " + floorPrefab.name + " / 스포너 " + spawner.name + " / 프리팹 " + prefab.name + " / " + unit.GetType().Name + " / " + unit.monsterType);
                if (unit.GetType() == type && !candidates.Contains(unit)) candidates.Add(unit);
            }
        }

        private static List<UnitAvatar> FindPrefabs(int floor, int stage, Type type)
        {
            List<UnitAvatar> candidates = new List<UnitAvatar>();
            try
            {
                DungeonManager dungeon = DungeonManager.Instance;
                RaceEntity race = dungeon == null ? null : dungeon.Race;
                StageEntity[] stages = race == null ? null : race.stages;
                StageEntity entity = stages != null && stage < stages.Length ? stages[stage] : null;
                StageEntity_Choice choice = entity as StageEntity_Choice;
                if (choice != null)
                {
                    foreach (GameObject floorPrefab in choice.fixedEventFloorPrefabs)
                    {
                        FloorGenerator generator = floorPrefab == null ? null : floorPrefab.GetComponent<FloorGenerator>();
                        if (generator != null && generator.floorThreatType == EFloorThreatType.Boss) Collect(floorPrefab, entity.name, type, candidates);
                    }
                }

                if (candidates.Count == 0 && entity != null)
                {
                    foreach (GameObject floorPrefab in entity.GetAllPrefabs())
                    {
                        if (floorPrefab != null) Collect(floorPrefab, entity.name, type, candidates);
                    }
                }

                if (candidates.Count == 0)
                {
                    foreach (UnityEngine.Object loaded in Resources.FindObjectsOfTypeAll(type))
                    {
                        UnitAvatar unit = loaded as UnitAvatar;
                        if (unit != null && unit.GetType() == type && !unit.gameObject.scene.IsValid() && !candidates.Contains(unit)) candidates.Add(unit);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            List<UnitAvatar> mains = candidates.FindAll(unit => IsMain(unit, type));
            if (mains.Count > 0) return mains;

            List<UnitAvatar> bosses = candidates.FindAll(unit => unit.monsterType == EMonsterType.Boss && !unit.name.EndsWith("_Ally", StringComparison.Ordinal));
            return bosses.Count > 0 ? bosses : candidates;
        }
    }
}
