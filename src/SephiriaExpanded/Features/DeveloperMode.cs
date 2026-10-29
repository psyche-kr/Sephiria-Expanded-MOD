using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal sealed class DeveloperMode : MonoBehaviour
    {
        private const string OpenCommand = "//dev";
        private const string BossCommand = "//boss";
        private const string AllBossParameter = "ALL";

        private const int PresetSteal = 100;
        private const int PresetToughness = 100;
        private const int PresetFixedDamage = 700;

        internal const int FixedDamageStep = 500;
        internal const int FixedDamageStepMax = 200;
        internal const int StatStep = 10;
        internal const int DefenseStepMax = 33;
        internal const int ToughnessStepMax = 100;
        private const string ToughnessStat = "TOUGHNESS";

        private static readonly FieldInfo CurrentLevel = AccessTools.Field(typeof(LevelController), "currentLevel");
        private static readonly FieldInfo CurrentExp = AccessTools.Field(typeof(LevelController), "currentExp");
        private static readonly FieldInfo LevelUpHealPercent = AccessTools.Field(typeof(LevelController), "levelUpHealPercent");
        private static readonly FieldInfo MoveSpeed = AccessTools.Field(typeof(UnitAvatar), "moveSpeed");
        private static readonly MethodInfo LocalAddExp = AccessTools.Method(typeof(LevelController), "LocalAddExp", new[] { typeof(int) });
        private static readonly MethodInfo StartInvulnerable = AccessTools.Method(typeof(UnitAvatar), "StartInvulnerable");
        private static readonly MethodInfo StopInvulnerable = AccessTools.Method(typeof(UnitAvatar), "StopInvulnerable");
        private static readonly FieldInfo RequestLeaveTimer = AccessTools.Field(typeof(DungeonManager), "requestLeaveTimer");
        private static readonly MethodInfo LocalStopBattle = AccessTools.Method(typeof(UnitAvatar), "LocalStopBattle");

        private PlayerAvatar godAvatar;
        private bool godEnabled;
        private PlayerAvatar speedAvatar;
        private float baseMoveSpeed;
        private bool baseMoveSpeedCaptured;
        private int speedSetting = 1;
        private PlayerAvatar presetAvatar;
        private bool presetEnabled;
        private int presetOriginalHpSteal;
        private int presetOriginalMpSteal;
        private PlayerAvatar damageAvatar;
        private int damageApplied;
        private int damageStep;
        private readonly StatHold defenseHold = new StatHold((avatar, value) => avatar.AddCustomStat(ECustomStat.DamageReduction, value));
        private readonly StatHold toughnessHold = new StatHold((avatar, value) => avatar.AddCustomStat(ToughnessStat, value));
        private int defenseStep;
        private int toughnessStep;

        internal static DeveloperMode Instance { get; private set; }
        internal static bool Activated { get; private set; }

        internal bool God => godEnabled;
        internal bool Preset => presetEnabled;
        internal int Speed => speedSetting;
        internal int FixedDamageStepValue => damageStep;
        internal int DefenseStep => defenseStep;
        internal int ToughnessStep => toughnessStep;

        [HarmonyPatch(typeof(UI_ChatInput), "OnSubmitCommand")]
        private static class Submit
        {
            [HarmonyPrefix]
            private static bool Before(UI_ChatInput __instance, string command)
            {
                string text = command == null ? string.Empty : command.Trim();
                bool toggle = text.Equals(OpenCommand, StringComparison.OrdinalIgnoreCase);
                bool boss = text.Equals(BossCommand, StringComparison.OrdinalIgnoreCase) || text.StartsWith(BossCommand + " ", StringComparison.OrdinalIgnoreCase);
                bool hard = HardRecordCommand.Matches(text);
                if (!toggle && !boss && !hard)
                    return true;

                if (toggle) SetActivated(!Activated);
                else if (Activated && hard) HardRecordCommand.Run(text);
                else if (Activated) BossInfo.Run(text);
                else ShowMessage("SE 개발자 설정이 비활성화 상태입니다. //dev로 활성화하세요.");

                if (__instance != null)
                {
                    if (__instance.inputField != null)
                        __instance.inputField.SetTextWithoutNotify(string.Empty);
                    __instance.Close();
                }

                return false;
            }
        }

        private static string CleanName(UnityEngine.Object target)
        {
            return target == null ? string.Empty : target.name.Replace("(Clone)", string.Empty).Trim();
        }

        private static string BossPrefabName(FloorData floor)
        {
            try
            {
                GameObject prefab = RaceDatabase.FindFloorGeneratorPrefabObject(floor.prefabAssetId);
                FloorGenerator generator = prefab == null ? null : prefab.GetComponent<FloorGenerator>();
                BossSpawner spawner = generator == null ? null : MainSpawner(generator);
                SocialIDEntity social = spawner == null ? null : spawner.bossSocialID;
                return social == null ? null : CleanName(social.avatarPrefab);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }
        }

        internal static void ShowCurrentFloorBoss()
        {
            PlayerAvatar local = LocalPlayer.Find();
            DungeonManager dungeon = DungeonManager.Instance;
            FloorData floor;
            if (local != null && dungeon != null && dungeon.generatedFloors.TryGetValue(local.currentFloorGuid, out floor) && floor.threatType == EFloorThreatType.Boss)
            {
                ShowMessage("이 층의 보스: " + (FloorBossName(floor) ?? "알 수 없음") + " | 프리팹 " + (BossPrefabName(floor) ?? "알 수 없음") + " | 층 " + floor.name);
                return;
            }

            ShowMessage("현재 층에는 보스가 없습니다.");
        }
        private static void SetActivated(bool value)
        {
            Activated = value;
            if (!value)
            {
                DevPanel.Close();
                if (Instance != null) Instance.ResetDev();
            }

            ShowMessage(value ? "SE 개발자 설정 활성화" : "SE 개발자 설정 비활성화");
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            DeveloperKill.Tick();
        }

        private void OnDestroy()
        {
            RestoreAll();
            DeveloperKill.Enabled = false;
            Activated = false;
            if (Instance == this) Instance = null;
        }

        internal void SetGod(bool enabled)
        {
            if (enabled == godEnabled) return;

            if (enabled)
            {
                PlayerAvatar avatar = LocalPlayer.Find();
                if (avatar == null || StartInvulnerable == null) return;

                StartInvulnerable.Invoke(avatar, null);
                godAvatar = avatar;
                godEnabled = true;
                return;
            }

            StopGod();
        }

        private void StopGod()
        {
            if (godAvatar != null && StopInvulnerable != null)
                StopInvulnerable.Invoke(godAvatar, null);

            godAvatar = null;
            godEnabled = false;
        }

        internal void SetSpeed(int value)
        {
            speedSetting = Mathf.Clamp(value, 1, 10);
            ApplySpeed();
        }

        private void ApplySpeed()
        {
            PlayerAvatar avatar = LocalPlayer.Find();
            if (avatar == null || MoveSpeed == null || MoveSpeed.FieldType != typeof(float)) return;

            if (speedAvatar != avatar || !baseMoveSpeedCaptured)
            {
                RestoreSpeed();
                speedAvatar = avatar;
                baseMoveSpeed = (float)MoveSpeed.GetValue(avatar);
                baseMoveSpeedCaptured = true;
            }

            MoveSpeed.SetValue(avatar, baseMoveSpeed * speedSetting);
        }

        private void RestoreSpeed()
        {
            if (speedAvatar != null && baseMoveSpeedCaptured && MoveSpeed != null)
                MoveSpeed.SetValue(speedAvatar, baseMoveSpeed);

            speedAvatar = null;
            baseMoveSpeedCaptured = false;
        }

        internal void SetPreset(bool enabled)
        {
            if (enabled == presetEnabled) return;

            if (enabled)
            {
                PlayerAvatar avatar = LocalPlayer.Find();
                if (avatar == null) return;

                presetAvatar = avatar;
                presetEnabled = true;
                presetOriginalHpSteal = avatar.GetCustomStat(ECustomStat.HPSteal);
                presetOriginalMpSteal = avatar.GetCustomStat(ECustomStat.MPSteal);
                avatar.AddCustomStat(ECustomStat.HPSteal, PresetSteal - presetOriginalHpSteal);
                avatar.AddCustomStat(ECustomStat.MPSteal, PresetSteal - presetOriginalMpSteal);
                ApplySpeed();
                SyncFixedDamage();
                SyncToughness();
                return;
            }

            RestorePreset();
            ApplySpeed();
            SyncFixedDamage();
            SyncToughness();
        }

        private void RestorePreset()
        {
            if (presetEnabled && presetAvatar != null)
            {
                presetAvatar.AddCustomStat(ECustomStat.HPSteal, presetOriginalHpSteal - presetAvatar.GetCustomStat(ECustomStat.HPSteal));
                presetAvatar.AddCustomStat(ECustomStat.MPSteal, presetOriginalMpSteal - presetAvatar.GetCustomStat(ECustomStat.MPSteal));
            }

            presetAvatar = null;
            presetEnabled = false;
        }

        internal void SetFixedDamageStep(int step)
        {
            damageStep = Mathf.Clamp(step, 0, FixedDamageStepMax);
            SyncFixedDamage();
        }

        private void SyncFixedDamage()
        {
            PlayerAvatar avatar = LocalPlayer.Find();
            int desired = damageStep * FixedDamageStep + (presetEnabled ? PresetFixedDamage : 0);

            if (damageAvatar != null && damageAvatar != avatar)
                damageAvatar.AddCustomStat(ECustomStat.TrueDamage, -damageApplied);

            if (damageAvatar != avatar) damageApplied = 0;
            damageAvatar = avatar;
            if (avatar == null || desired == damageApplied) return;

            avatar.AddCustomStat(ECustomStat.TrueDamage, desired - damageApplied);
            damageApplied = desired;
        }

        private void RestoreFixedDamage()
        {
            if (damageAvatar != null && damageApplied != 0)
                damageAvatar.AddCustomStat(ECustomStat.TrueDamage, -damageApplied);

            damageAvatar = null;
            damageApplied = 0;
        }

        private sealed class StatHold
        {
            private readonly Action<PlayerAvatar, int> add;
            private PlayerAvatar avatar;
            private int applied;

            internal StatHold(Action<PlayerAvatar, int> add)
            {
                this.add = add;
            }

            internal void Sync(int desired)
            {
                PlayerAvatar current = LocalPlayer.Find();
                if (avatar != null && avatar != current) add(avatar, -applied);

                if (avatar != current) applied = 0;
                avatar = current;
                if (current == null || desired == applied) return;

                add(current, desired - applied);
                applied = desired;
            }

            internal void Restore()
            {
                if (avatar != null && applied != 0) add(avatar, -applied);

                avatar = null;
                applied = 0;
            }
        }

        internal void SetDefenseStep(int step)
        {
            defenseStep = Mathf.Clamp(step, 0, DefenseStepMax);
            SyncDefense();
        }

        private void SyncDefense()
        {
            defenseHold.Sync(defenseStep * StatStep);
        }

        internal void SetToughnessStep(int step)
        {
            toughnessStep = Mathf.Clamp(step, 0, ToughnessStepMax);
            SyncToughness();
        }

        private void SyncToughness()
        {
            toughnessHold.Sync(toughnessStep + (presetEnabled ? PresetToughness : 0));
        }

        internal void SetKill(bool enabled)
        {
            DeveloperKill.Enabled = enabled;
        }

        internal void ResetDev()
        {
            StopGod();
            RestorePreset();
            speedSetting = 1;
            RestoreSpeed();
            damageStep = 0;
            RestoreFixedDamage();
            defenseStep = 0;
            defenseHold.Restore();
            toughnessStep = 0;
            toughnessHold.Restore();
            DeveloperKill.Enabled = false;
            ModSettings.ResetDevCheats();
            ModSettings.Save();
        }

        private void RestoreAll()
        {
            StopGod();
            RestorePreset();
            RestoreSpeed();
            RestoreFixedDamage();
            defenseHold.Restore();
            toughnessHold.Restore();
        }

        internal void MoveToFloor(int chapter)
        {
            MoveToStageFloor(chapter, FindStartFloor, "시작");
        }


        internal static readonly string[][] BossNamesByStage =
        {
            new[] { "모그디", "오드너", "하즈" },
            new[] { "라타카", "레리드" },
            new[] { "에르마", "팬택시스" },
            new[] { "아스카드" },
            new[] { "코베스", "크라즈" },
            new[] { "클리포트" },
            new[] { "클리포트" }
        };

        internal void MoveToBoss(int chapter, string bossName = null)
        {
            MoveToStageFloor(chapter, floors => ForcedBossFloor(floors, bossName, chapter), "보스");
        }

        private static string FloorBossName(FloorData floor)
        {
            try
            {
                GameObject prefab = RaceDatabase.FindFloorGeneratorPrefabObject(floor.prefabAssetId);
                FloorGenerator generator = prefab == null ? null : prefab.GetComponent<FloorGenerator>();
                return generator == null ? null : BossName(generator);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }
        }

        private static BossSpawner MainSpawner(FloorGenerator generator)
        {
            BossSpawner first = null;
            foreach (BossSpawner spawner in generator.GetComponentsInChildren<BossSpawner>(true))
            {
                if (first == null) first = spawner;

                GameObject prefab = spawner.bossSocialID == null ? null : spawner.bossSocialID.avatarPrefab;
                UnitAvatar unit = prefab == null ? null : prefab.GetComponent<UnitAvatar>();
                if (unit != null && unit.monsterType == EMonsterType.Boss && !prefab.name.EndsWith("_Ally", StringComparison.Ordinal)) return spawner;
            }

            return first;
        }

        private static string BossName(FloorGenerator floor)
        {
            BossSpawner spawner = MainSpawner(floor);
            SocialIDEntity social = spawner == null ? null : spawner.bossSocialID;
            UnitAvatar unit = social == null || social.avatarPrefab == null ? null : social.avatarPrefab.GetComponent<UnitAvatar>();
            string known = unit == null ? null : BossChallenge.DisplayName(unit.GetType());
            if (known != null) return known;

            string name = social == null ? null : social.Name.ToString();
            if (string.IsNullOrEmpty(name) && unit != null) name = unit.Name;

            if (string.IsNullOrEmpty(name)) name = floor.aName.ToString();
            return string.IsNullOrEmpty(name) ? floor.name : KeywordDatabase.Convert(name, useColor: false, useSprite: false);
        }

        internal void MoveToMiniBoss(int chapter)
        {
            FloorData miniBossFloor = MoveToStageFloor(chapter, FindMiniBossFloor, "미니보스");
            if (miniBossFloor != null) StartCoroutine(DeveloperFloor.EnterMiniBossRoom(miniBossFloor.guid));
        }

        private static string MoveMessage(int chapter, string label)
        {
            if (chapter == 6) return "스테이지 5-2 보스 층으로 이동했습니다.";

            return "스테이지 " + (chapter == 7 ? 6 : chapter) + " " + label + " 층으로 이동했습니다.";
        }

        private static FloorData MoveToStageFloor(int chapter, Func<List<FloorData>, FloorData> selector, string label)
        {
            if (chapter < 1 || !NetworkServer.active) return null;

            DungeonManager dungeon = DungeonManager.Instance;
            RaceEntity race = dungeon == null ? null : dungeon.Race;
            StageEntity[] stages = race == null ? null : race.stages;
            if (stages == null || chapter > stages.Length || stages[chapter - 1] == null) return null;

            StageEntity stage = stages[chapter - 1];
            if (string.IsNullOrWhiteSpace(stage.name)) return null;

            try
            {
                RestorePausePanel();

                if (label == "시작")
                {
                    dungeon.LoadStageAndMove(stage.name);
                    ShowMessage(MoveMessage(chapter, label));
                    return null;
                }

                if (label == "보스") dungeon.BossGenerateParameter = AllBossParameter;

                dungeon.LoadStage(stage.name);
                FloorData target = selector(dungeon.GetAllFloorInStage(stage.name));
                if (target == null) return null;

                SetInDungeon(dungeon, true);
                dungeon.MoveTogether(target.guid, "FLOORSTARTING", stage.stageLoadingScreenIdx, false, false);
                ShowMessage(MoveMessage(chapter, label));
                return target;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }
        }

        private static FloorData FindStartFloor(List<FloorData> floors)
        {
            if (floors == null || floors.Count == 0)
                return null;

            FloorData start = null;
            for (int i = 0; i < floors.Count; i++)
            {
                FloorData floor = floors[i];
                if (floor == null)
                    continue;

                if (start == null || floor.nodeProgress < start.nodeProgress)
                    start = floor;
            }

            return start;
        }

        private static readonly MethodInfo AddGeneratedFloor = AccessTools.Method(typeof(DungeonManager), "AddToGeneratedFloor");

        private static FloorData ForcedBossFloor(List<FloorData> floors, string bossName, int chapter)
        {
            if (string.IsNullOrEmpty(bossName)) return FindBossFloor(floors, bossName);

            if (floors != null)
            {
                foreach (FloorData floor in floors)
                {
                    if (floor != null && floor.threatType == EFloorThreatType.Boss && FloorBossName(floor) == bossName) return floor;
                }
            }

            FloorData created = null;
            try
            {
                created = CreateBossFloor(floors, bossName, chapter);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            return created ?? FindBossFloor(floors, bossName);
        }

        private static int BossFloorScore(GameObject prefab, Type[] types)
        {
            int best = -1;
            FloorGenerator generator = prefab.GetComponent<FloorGenerator>();
            foreach (BossSpawner spawner in prefab.GetComponentsInChildren<BossSpawner>(true))
            {
                GameObject avatar = spawner.bossSocialID == null ? null : spawner.bossSocialID.avatarPrefab;
                UnitAvatar unit = avatar == null ? null : avatar.GetComponent<UnitAvatar>();
                if (unit == null || Array.IndexOf(types, unit.GetType()) < 0 || unit.monsterType != EMonsterType.Boss) continue;

                int score = BossInfo.IsMain(unit, unit.GetType()) ? 2 : 0;
                if (generator != null && generator.floorThreatType == EFloorThreatType.Boss) score += 1;
                best = Mathf.Max(best, score);
            }

            return best;
        }

        private static FloorData CreateBossFloor(List<FloorData> floors, string bossName, int chapter)
        {
            Type[] types = BossInfo.TypesFor(chapter - 1, bossName);
            DungeonManager dungeon = DungeonManager.Instance;
            RaceEntity race = dungeon == null ? null : dungeon.Race;
            StageEntity stage = race == null || race.stages == null || chapter > race.stages.Length ? null : race.stages[chapter - 1];
            if (types == null || stage == null) return null;

            GameObject chosen = null;
            int chosenScore = -1;
            List<GameObject> pool = new List<GameObject>(stage.GetAllPrefabs());
            if (RaceDatabase.floorGeneratorDictionary != null) pool.AddRange(RaceDatabase.floorGeneratorDictionary.Values);
            foreach (GameObject prefab in pool)
            {
                if (prefab == null || prefab.GetComponent<FloorGenerator>() == null || prefab.GetComponent<NetworkIdentity>() == null) continue;

                int score = BossFloorScore(prefab, types);
                if (score > chosenScore)
                {
                    chosen = prefab;
                    chosenScore = score;
                }
            }

            if (chosen == null) return null;

            int difficulty = 0;
            int nodeProgress = 0;
            int globalX = 0;
            int globalY = 0;
            foreach (KeyValuePair<string, FloorData> pair in dungeon.generatedFloors)
            {
                globalX = Mathf.Max(globalX, pair.Value.globalX);
                if (pair.Value.stageName != stage.name) continue;

                difficulty = Mathf.Max(difficulty, pair.Value.difficulty);
                nodeProgress = Mathf.Max(nodeProgress, pair.Value.nodeProgress);
                globalY = pair.Value.globalY;
            }

            FloorData data = new FloorData
            {
                guid = Guid.NewGuid().ToString(),
                name = chosen.name,
                stageName = stage.name,
                seed = new System.Random().Next(),
                globalX = globalX + 2,
                globalY = globalY,
                nodeProgress = nodeProgress + 1,
                prefabAssetId = chosen.GetComponent<NetworkIdentity>().assetId,
                difficulty = difficulty,
                threatType = EFloorThreatType.Boss,
                disturbance = string.Empty,
                bossHard = string.Empty,
                randomRoomCount = 0
            };
            Debug.Log("[Sephiria Expanded][DevMove] " + bossName + " 보스방을 강제로 만듭니다: " + chosen.name + " (점수 " + chosenScore + ")");
            if (AddGeneratedFloor != null) AddGeneratedFloor.Invoke(dungeon, new object[] { data });
            else dungeon.generatedFloors.Add(data.guid, data);

            return data;
        }
        private static FloorData FindBossFloor(List<FloorData> floors, string bossName)
        {
            if (floors == null || floors.Count == 0)
                return null;

            FloorData boss = null;
            FloorData fallback = null;
            FloorData matched = null;
            for (int i = 0; i < floors.Count; i++)
            {
                FloorData floor = floors[i];
                if (floor == null)
                    continue;

                if (!string.IsNullOrEmpty(bossName) && floor.threatType == EFloorThreatType.Boss)
                {
                    if (matched == null && FloorBossName(floor) == bossName) matched = floor;
                }

                if (fallback == null || floor.nodeProgress > fallback.nodeProgress)
                    fallback = floor;

                if (floor.threatType != EFloorThreatType.Boss)
                    continue;
                if (boss == null || floor.nodeProgress > boss.nodeProgress)
                    boss = floor;
            }

            return matched ?? boss ?? fallback;
        }

        private static FloorData FindMiniBossFloor(List<FloorData> floors)
        {
            if (floors == null || floors.Count == 0)
                return null;

            StageEntity targetStage = null;
            DungeonManager dungeon = DungeonManager.Instance;
            RaceEntity race = dungeon == null ? null : dungeon.Race;
            StageEntity[] stages = race == null ? null : race.stages;

            string stageName = null;
            for (int i = 0; i < floors.Count; i++)
            {
                FloorData floor = floors[i];
                if (floor != null && !string.IsNullOrWhiteSpace(floor.stageName))
                {
                    stageName = floor.stageName;
                    break;
                }
            }

            if (stages != null && !string.IsNullOrWhiteSpace(stageName))
            {
                for (int i = 0; i < stages.Length; i++)
                {
                    StageEntity stage = stages[i];
                    if (stage != null && stage.name == stageName)
                    {
                        targetStage = stage;
                        break;
                    }
                }
            }

            HashSet<uint> miniBossPrefabIds = new HashSet<uint>();

            if (targetStage != null)
            {
                FieldInfo minibossPrefabsField = AccessTools.Field(targetStage.GetType(), "minibossFloorPrefabs");
                GameObject[] minibossPrefabs = minibossPrefabsField == null
                    ? null
                    : minibossPrefabsField.GetValue(targetStage) as GameObject[];

                if (minibossPrefabs != null)
                {
                    for (int i = 0; i < minibossPrefabs.Length; i++)
                    {
                        GameObject prefab = minibossPrefabs[i];
                        if (prefab == null)
                            continue;

                        NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
                        if (identity != null)
                            miniBossPrefabIds.Add(identity.assetId);
                    }
                }
            }

            FloorData exactMiniBoss = null;
            FloorData fallback = null;

            for (int i = 0; i < floors.Count; i++)
            {
                FloorData floor = floors[i];
                if (floor == null || floor.threatType != EFloorThreatType.MiniBoss)
                    continue;

                if (!floor.isHidden && !floor.pocketDimension)
                {
                    if (fallback == null || floor.nodeProgress > fallback.nodeProgress)
                        fallback = floor;

                    if (miniBossPrefabIds.Contains(floor.prefabAssetId))
                    {
                        if (exactMiniBoss == null || floor.nodeProgress > exactMiniBoss.nodeProgress)
                            exactMiniBoss = floor;
                    }
                }
            }

            return exactMiniBoss ?? fallback;
        }

        private static void RestorePausePanel()
        {
            if (UIManager.Instance != null) UIManager.Instance.SetAllowPausePanelOpening(true);
        }

        private static void SetInDungeon(DungeonManager dungeon, bool inDungeon)
        {
            int value = inDungeon ? 1 : 0;
            dungeon.dungeonEnvironment["IsInDungeon"] = value;

            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                PlayerAvatar avatar = spawner == null ? null : spawner.PlayerAvatar;
                if (avatar == null) continue;

                avatar.NetworkisInDungeon = value;
                if (inDungeon) spawner.gotoMySessionOnGameOverServerside = 0;
            }
        }

        internal static void MoveToBase()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            DungeonManager dungeon = DungeonManager.Instance;
            if (dungeon == null)
            {
                return;
            }

            try
            {
                RestorePausePanel();

                if (dungeon.isRunStarted)
                {
                    if (LocalStopBattle != null)
                    {
                        PlayerAvatar[] players = FindObjectsByType<PlayerAvatar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                        for (int i = 0; i < players.Length; i++)
                        {
                            PlayerAvatar player = players[i];
                            if (player != null && player.IsInBattle)
                                LocalStopBattle.Invoke(player, null);
                        }
                    }

                    dungeon.RequestLeaveOnHost();

                    if (RequestLeaveTimer != null && RequestLeaveTimer.FieldType == typeof(float))
                        RequestLeaveTimer.SetValue(dungeon, -1f);

                    ShowMessage("진행 중인 런을 포기하고 마을로 이동합니다.");
                    return;
                }

                FloorData home = dungeon.FindHomeFloor();
                if (home == null)
                {
                    return;
                }

                SetInDungeon(dungeon, false);
                dungeon.MoveTogether(home.guid, "FLOORSTARTING", 0, false, false);
                ShowMessage("마을로 이동했습니다.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        internal static int MaxLevel()
        {
            MorePlayerLevel.Refresh();
            int[] table = MorePlayerLevel.GetTable();
            return table == null || table.Length == 0 ? 30 : table.Length;
        }

        internal static int GetLevel()
        {
            LevelController controller = FindController(LocalPlayer.Find());
            return controller == null || CurrentLevel == null ? 1 : (int)CurrentLevel.GetValue(controller);
        }

        internal static void SetLevel(int level)
        {
            PlayerAvatar avatar = LocalPlayer.Find();
            LevelController controller = FindController(avatar);
            int[] table = MorePlayerLevel.GetTable();
            if (controller == null || table == null || table.Length == 0 || CurrentLevel == null || CurrentExp == null)
            {
                return;
            }

            level = Mathf.Clamp(level, 1, table.Length);
            int currentLevel = (int)CurrentLevel.GetValue(controller);
            int currentExp = (int)CurrentExp.GetValue(controller);
            int targetIndex = Mathf.Clamp(level - 1, 0, table.Length - 1);
            int targetExp = table[targetIndex];

            if (NetworkServer.active && level > currentLevel)
            {
                int requiredExp = targetExp - currentExp;
                if (requiredExp > 0 && LocalAddExp != null)
                    LocalAddExp.Invoke(controller, new object[] { requiredExp });

                if ((int)CurrentLevel.GetValue(controller) != level)
                {
                    CurrentLevel.SetValue(controller, level);
                    CurrentExp.SetValue(controller, targetExp);
                }

                return;
            }

            CurrentLevel.SetValue(controller, level);
            CurrentExp.SetValue(controller, targetExp);
            ApplyLocalHeal(avatar, controller, level > currentLevel);
        }

        private static void ApplyLocalHeal(PlayerAvatar avatar, LevelController controller, bool leveledUp)
        {
            if (!leveledUp || avatar == null || LevelUpHealPercent == null)
                return;

            int healPercent = (int)LevelUpHealPercent.GetValue(controller);
            if (healPercent <= 0)
                return;

            if (NetworkServer.active)
            {
                avatar.HealPercent(healPercent);
                return;
            }

            float healed = avatar.Networkhp + avatar.MaxHp * healPercent / 100f;
            avatar.Networkhp = Mathf.Min(avatar.MaxHp, healed);
        }

        private static LevelController FindController(PlayerAvatar avatar)
        {
            LevelController[] controllers = FindObjectsByType<LevelController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (LevelController controller in controllers)
                if (controller != null && controller.Avatar == avatar)
                    return controller;

            return avatar == null ? null : avatar.GetComponentInChildren<LevelController>(true);
        }

        internal static void ShowMessage(string message)
        {
            UI_HUDLogViewer[] viewers = FindObjectsByType<UI_HUDLogViewer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (UI_HUDLogViewer viewer in viewers)
                if (viewer != null)
                    viewer.SpawnLog(message, Color.green);
        }
    }
}
