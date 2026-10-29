using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SephiriaExpanded
{
    internal sealed class BossChallenge_QBoss : IBossChallenge
    {
        private static BossChallengeRules Rules => BossChallenge.QBoss;

        private const string BossName = "클리포트";
        private const string RecoveryLine = "이대로 끝일 줄 알았습니까?";
        private const string EncounterLine = "어디 한번 보여주시지요.";
        private const string DeathLine = "끝없는 고통을 안겨드리지요. 부디 꺾이지 않기를...";
        private const string DeathKey = "Death";
        private const float DeathLineTime = 4f;
        private static readonly float[] HpRatios = { 0.66f, 0.33f };
        private static readonly string[] HpLines = { "크윽...", "더이상..." };
        private const float RecoveryRatio = 0.05f;
        private const float RecoveryTime = 5f;
        private const float RecoveryLineTime = 5f;
        private const float RecoveryFloorHp = 1f;
        private const float DescendTimeout = 3f;
        private const float RecoveredDamageRate = 3f;
        private const int RecoveredLaserCount = 6;
        private const int LevitationSortingOrder = 5;
        private const float RecoveredSpeedRate = 1.2f;
        private const float RecoveredCrackInterval = 3f;
        private const float VanillaFastMoveSpeed = 5f;
        private const int FastMoveSearchWindow = 12;
        private const int RootPattern = 4;
        private const int GeometryPattern = 5;
        private static readonly int[] RecoveredPatterns = { 0, 1, 2, 3 };

        private static readonly AccessTools.FieldRef<Unit_QBoss, float> LevitationTimer =
            AccessTools.FieldRefAccess<Unit_QBoss, float>("levitationTimer");

        private static readonly MethodInfo SetSortingOrder = AccessTools.Method(typeof(Unit_QBoss), "RpcSetSortingOrder");
        private static readonly MethodInfo GeometryStart = AccessTools.Method(typeof(Unit_QBoss), "RpcGeometryProjectileStart");
        private static readonly MethodInfo GeometryEnd = AccessTools.Method(typeof(Unit_QBoss), "RpcGeometryProjectileEnd");

        private enum Phase
        {
            None,
            Recovering,
            Recovered
        }

        private static readonly Dictionary<Unit_QBoss, Phase> Phases = new Dictionary<Unit_QBoss, Phase>();
        private static readonly Dictionary<Unit_QBoss, int> LaserFrames = new Dictionary<Unit_QBoss, int>();
        private static readonly HashSet<UnitAI_BossBasic> EnterShown = new HashSet<UnitAI_BossBasic>();
        private static bool spawningLasers;

        public void Tick()
        {
        }

        public void Cleanup()
        {
            Phases.Clear();
            LaserFrames.Clear();
            EnterShown.Clear();
        }

        private static bool IsEnhanced()
        {
            return Rules.Level >= 1;
        }

        private static Phase PhaseOf(Unit_QBoss unit)
        {
            Phase phase;
            return unit != null && Phases.TryGetValue(unit, out phase) ? phase : Phase.None;
        }

        internal static bool Recolored(Unit_QBoss unit)
        {
            return PhaseOf(unit) != Phase.None;
        }

        [HarmonyPatch(typeof(UnitAI_BossBasic), nameof(UnitAI_BossBasic.OnBattleStartServer))]
        private static class BattleStart
        {
            [HarmonyPostfix]
            private static void After(UnitAI_BossBasic __instance)
            {
                Unit_QBoss unit = __instance.Avatar as Unit_QBoss;
                if (unit == null) return;

                Phases.Remove(unit);
                BossDialogue.ClearThresholds(__instance);
                if (IsEnhanced() && !EnterShown.Remove(__instance)) BossDialogue.SayEncounter(__instance, BossName, EncounterLine, Rules.Level);
            }
        }

        private static LocalizedString[][] EnterSpeeches(UnitAI_NewBasic ai)
        {
            if (ai is UnitAI_QBossAdv adv) return new[] { adv.helloSpeech, adv.helloChapter6Speech };
            if (ai is UnitAI_QBoss basic) return new[] { basic.helloSpeeech };
            return null;
        }

        private static IEnumerable<LocalizedString> DeathSpeeches(UnitAI_NewBasic ai)
        {
            if (ai is UnitAI_QBossAdv adv)
            {
                yield return adv.chapter5ClearSpeechSaySephiroth;
                foreach (LocalizedString speech in adv.chapter5ClearSpeech)
                    yield return speech;

                yield return adv.chapter5ClearSpeechSay_Last;
            }
            else if (ai is UnitAI_QBoss basic)
            {
                foreach (LocalizedString speech in basic.bye1Speeech)
                    yield return speech;
            }
        }

        private static bool IsEnterSpeech(UnitAI_NewBasic ai, string key, out bool first)
        {
            first = false;
            LocalizedString[][] groups = EnterSpeeches(ai);
            if (groups == null) return false;

            foreach (LocalizedString[] group in groups)
            {
                for (int i = 0; i < group.Length; i++)
                {
                    if (group[i].key != key) continue;

                    first = i == 0;
                    return true;
                }
            }

            return false;
        }

        private static bool IsDeathSpeech(UnitAI_NewBasic ai, string key)
        {
            foreach (LocalizedString speech in DeathSpeeches(ai))
            {
                if (speech != null && speech.key == key) return true;
            }

            return false;
        }

        [HarmonyPatch(typeof(UnitAI_NewBasic), nameof(UnitAI_NewBasic.CreateSpeechBubble),
            new[] { typeof(string), typeof(bool), typeof(float), typeof(bool), typeof(Loc.KeywordValue[]) })]
        private static class SpeechReplace
        {
            [HarmonyPrefix]
            private static bool Before(UnitAI_NewBasic __instance, string key)
            {
                if (key == null || !IsEnhanced() || !__instance.isServer || !(__instance is UnitAI_BossBasic ai) || !(ai.Avatar is Unit_QBoss)) return true;

                bool first;
                if (IsEnterSpeech(ai, key, out first))
                {
                    if (first && EnterShown.Add(ai)) BossDialogue.Say(ai, BossName, EncounterLine, Rules.Level);
                    return false;
                }

                if (!IsDeathSpeech(ai, key)) return true;

                if (BossDialogue.TryFireThreshold(ai, DeathKey)) BossDialogue.Say(ai, BossName, DeathLine, Rules.Level, DeathLineTime);
                return false;
            }
        }

        [HarmonyPatch(typeof(Unit_QBoss), "Update")]
        private static class HpLineWatch
        {
            [HarmonyPostfix]
            private static void After(Unit_QBoss __instance)
            {
                if (!__instance.isServer || !IsEnhanced() || PhaseOf(__instance) != Phase.Recovered) return;

                BossDialogue.CheckHpLines(__instance.GetComponent<UnitAI_BossBasic>(), BossName, HpRatios, HpLines, Rules.Level);
            }
        }

        [HarmonyPatch]
        private static class DeadlyCheck
        {
            [HarmonyTargetMethods]
            private static IEnumerable<MethodBase> Targets()
            {
                yield return AccessTools.Method(typeof(UnitAI_QBoss), "CheckDeadlyDamage");
                yield return AccessTools.Method(typeof(UnitAI_QBossAdv), "CheckDeadlyDamage");
            }

            [HarmonyPrefix]
            private static bool Before(UnitAI_BossBasic __instance)
            {
                Unit_QBoss unit = __instance.Avatar as Unit_QBoss;
                if (unit == null || !__instance.isServer || !IsEnhanced() || unit.IsDead || unit.MaxHp <= 0f) return true;

                if (PhaseOf(unit) != Phase.None || unit.hp / unit.MaxHp >= RecoveryRatio) return true;

                if (unit.hp <= 0f) unit.Networkhp = RecoveryFloorHp;
                StartRecovery(unit, __instance);
                return false;
            }
        }

        private static void StartRecovery(Unit_QBoss unit, UnitAI_BossBasic ai)
        {
            Phases[unit] = Phase.Recovering;
            ai.CancelAction();
            unit.CancelCurrentAction();
            BossDialogue.HoldFor(ai, RecoveryTime);
            BossBreak.Refill(unit, RecoveryTime);
            unit.StartInvulnerable();
            Vector2 center = unit.bossZoneInitialized ? (unit.bossZoneBottomLeft + unit.bossZoneTopRight) * 0.5f : (Vector2)ai.Environment.BossZoneCenter;
            unit.ReqSetPosition(center, true);
            unit.NetworkcurrentAction = Unit_QBoss.EAnimation.GeometryProjectile;
            unit.steeringSpeed = 0f;
            GeometryStart?.Invoke(unit, null);
            unit.NetworklevitationPhase = Unit_QBoss.ELevitationPhase.Rising;
            LevitationTimer(unit) = 0f;
            SetSortingOrder?.Invoke(unit, new object[] { LevitationSortingOrder });
            BossDialogue.Say(ai, BossName, RecoveryLine, Rules.Level, RecoveryLineTime);
            unit.StartCoroutine(Recover(unit, unit.hp));
        }

        private static IEnumerator Recover(Unit_QBoss unit, float startHp)
        {
            float start = Time.time;
            while (unit != null && !unit.IsDead && Time.time - start < RecoveryTime)
            {
                unit.Networkhp = Mathf.Lerp(startHp, unit.MaxHp, (Time.time - start) / RecoveryTime);
                yield return null;
            }

            if (unit == null || unit.IsDead) yield break;

            unit.Networkhp = unit.MaxHp;
            GeometryEnd?.Invoke(unit, null);
            unit.NetworklevitationPhase = Unit_QBoss.ELevitationPhase.Descending;
            float descend = Time.time;
            while (unit != null && unit.levitationPhase == Unit_QBoss.ELevitationPhase.Descending && Time.time - descend < DescendTimeout)
                yield return null;

            if (unit == null) yield break;

            unit.NetworklevitationPhase = Unit_QBoss.ELevitationPhase.None;
            unit.NetworkcurrentAction = Unit_QBoss.EAnimation.None;
            unit.steeringSpeed = 1f;
            unit.StopInvulnerable();
            Phases[unit] = Phase.Recovered;
        }

        [HarmonyPatch(typeof(UnitAvatar), "ApplyDamage")]
        private static class DoubleDamage
        {
            [HarmonyPrefix]
            private static void Before(UnitAvatar __instance, DamageInstance __0)
            {
                if (__0 == null || __0.isSystemDamage || !(__instance is Unit_QBoss unit) || PhaseOf(unit) != Phase.Recovered) return;

                UnitAvatar origin = __0.origin as UnitAvatar;
                if (origin is PlayerAvatar || (origin != null && origin.NetworkLeader is PlayerAvatar)) __0.damage *= RecoveredDamageRate;
            }
        }

        [HarmonyPatch(typeof(Unit_QBoss), nameof(Unit_QBoss.CreateTreeLaser))]
        private static class HexLaser
        {
            [HarmonyPrefix]
            private static bool Before(Unit_QBoss __instance, Vector3 direction)
            {
                if (spawningLasers || !__instance.isServer || PhaseOf(__instance) != Phase.Recovered) return true;

                int frame;
                if (LaserFrames.TryGetValue(__instance, out frame) && frame == Time.frameCount) return false;

                LaserFrames[__instance] = Time.frameCount;
                float baseAngle = HorayUtility.GetAngleFromVector(direction);
                spawningLasers = true;
                try
                {
                    for (int i = 0; i < RecoveredLaserCount; i++)
                    {
                        Vector3 laserDirection = HorayUtility.GetVector3FromAngle(baseAngle + i * 360f / RecoveredLaserCount);
                        __instance.CreateTreeLaser(__instance.transform.position + laserDirection, laserDirection);
                    }
                }
                finally
                {
                    spawningLasers = false;
                }

                return false;
            }
        }

        private static float FastMoveSpeed(Unit_QBoss unit)
        {
            return PhaseOf(unit) == Phase.Recovered ? VanillaFastMoveSpeed * RecoveredSpeedRate : VanillaFastMoveSpeed;
        }

        [HarmonyPatch(typeof(Unit_QBoss), "Update")]
        private static class FastMovePatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                FieldInfo target = AccessTools.Field(typeof(Unit_QBoss), "fastMovePosition");
                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
                int lastLoad = -FastMoveSearchWindow - 1;
                bool replaced = false;
                for (int i = 0; i < codes.Count && !replaced; i++)
                {
                    if (codes[i].LoadsField(target)) lastLoad = i;
                    if (codes[i].opcode != OpCodes.Ldc_R4 || !(codes[i].operand is float value) || value != VanillaFastMoveSpeed || i - lastLoad > FastMoveSearchWindow) continue;

                    codes[i].opcode = OpCodes.Ldarg_0;
                    codes[i].operand = null;
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BossChallenge_QBoss), nameof(FastMoveSpeed))));
                    replaced = true;
                }

                if (!replaced) Debug.LogWarning("[Sephiria Expanded][QBoss] 빠른 접근 속도 위치를 찾지 못했습니다.");

                return codes;
            }
        }

        [HarmonyPatch]
        private static class PatternFilter
        {
            [HarmonyTargetMethods]
            private static IEnumerable<MethodBase> Targets()
            {
                yield return AccessTools.Method(typeof(UnitAI_QBoss), "OnAIUpdate_FoundEnemy");
                yield return AccessTools.Method(typeof(UnitAI_QBossAdv), "OnAIUpdate_FoundEnemy");
            }

            [HarmonyPrefix]
            private static void Before(UnitAI_BossBasic __instance)
            {
                if (PhaseOf(__instance.Avatar as Unit_QBoss) != Phase.Recovered) return;

                List<int> list;
                Timer geometryTimer;
                if (__instance is UnitAI_QBossAdv adv)
                {
                    list = adv.patternList;
                    geometryTimer = adv.geometryProjectileIntervalTimer;
                    adv.additionalCrackTimer.time = RecoveredCrackInterval;
                }
                else if (__instance is UnitAI_QBoss basic)
                {
                    list = basic.patternList;
                    geometryTimer = basic.geometryProjectileIntervalTimer;
                }
                else return;

                geometryTimer.Ratio = 0f;
                list.RemoveAll(pattern => pattern == RootPattern || pattern == GeometryPattern);
                if (list.Count > 0) return;

                list.AddRange(RecoveredPatterns);
                list.Shuffle();
            }
        }
    }
}
