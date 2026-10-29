using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using HarmonyLib;
using UnityEngine;

namespace SephiriaExpanded
{
    internal sealed class BossChallenge_PantherRogueKing : IBossChallenge
    {
        private static BossChallengeRules Rules => BossChallenge.PantherRogueKing;

        private const int HardLevel = 2;

        private const float BasicAttackStep = 30f;
        private const float BasicAttackHardBonusStep = 45f;

        private const int SlowShurikenMainNormal = 7;
        private const int SlowShurikenMainHard = 8;
        private const int SlowShurikenSecondNormal = 5;
        private const int SlowShurikenSecondHard = 6;
        private const float SlowShurikenFanSpan = 80f;

        private const float RushRadialStep = 45f;
        private const int RushRadialShots = 8;

        private const float CloneDaggerExtraSetupDelay = 1f;
        private const float CloneDaggerExtraLifetime = 2f;

        private const string BossName = "레리드";
        private const string EncounterLine = "용케 여기까지 왔군.";
        private const string LowHpLine = "흐음... 제법이군";
        private const string DeathLine = "훌륭한 솜씨였다... 이 길, 지나가거라.";
        private const float LowHpRatio = 0.5f;

        private static readonly string[] RushLines = { "따라올 수 있겠나!", "일섬!", "빈틈이 보이는군!", "베어 가르마!", "눈 깜짝할 새다!" };
        private static readonly string[] CloneDaggerLines = { "그림자는 하나가 아니다.", "피해 보아라!", "맞혀 보거라." };
        private static readonly HashSet<UnitAI_PantherRogueKing> EnterShown = new HashSet<UnitAI_PantherRogueKing>();

        private static readonly AccessTools.FieldRef<Unit_PantherRogueKing, List<UI_AOEWarning_RangeAttackLine>> BasicAttack2Warnings =
            AccessTools.FieldRefAccess<Unit_PantherRogueKing, List<UI_AOEWarning_RangeAttackLine>>("basicAttack2Warnings");

        private static readonly AccessTools.FieldRef<UnitAI_PantherRogueKing, BossEnvironment_Panther> PantherEnv =
            AccessTools.FieldRefAccess<UnitAI_PantherRogueKing, BossEnvironment_Panther>("pantherEnvironment");

        private static readonly AccessTools.FieldRef<UnitAI_PantherRogueKing, UnitAI_PantherRogueKing.State> AiState =
            AccessTools.FieldRefAccess<UnitAI_PantherRogueKing, UnitAI_PantherRogueKing.State>("state");

        private static readonly AccessTools.FieldRef<Unit_PantherRogueKing, List<PantherRogueKingShadow>> ShadowList =
            AccessTools.FieldRefAccess<Unit_PantherRogueKing, List<PantherRogueKingShadow>>("shadowList");

        private static readonly AccessTools.FieldRef<UnitAI_PantherRogueKing, Coroutine> PatternCoroutine =
            AccessTools.FieldRefAccess<UnitAI_PantherRogueKing, Coroutine>("pattern");

        private static readonly System.Reflection.MethodInfo RushPatternMethod = AccessTools.Method(typeof(UnitAI_PantherRogueKing), "RushPatternCoroutine");

        private const int RushCasts = 2;

        private static readonly Dictionary<UnitAI_PantherRogueKing, UnitAI_PantherRogueKing.State> LastState =
            new Dictionary<UnitAI_PantherRogueKing, UnitAI_PantherRogueKing.State>();

        private static readonly Dictionary<UnitAI_PantherRogueKing, int> RushCastsDone = new Dictionary<UnitAI_PantherRogueKing, int>();
        private static readonly Dictionary<UnitAI_PantherRogueKing, int> RushDashes = new Dictionary<UnitAI_PantherRogueKing, int>();

        public void Tick()
        {
        }

        public void Cleanup()
        {
            LastState.Clear();
            RushCastsDone.Clear();
            RushDashes.Clear();
            EnterShown.Clear();
        }

        [HarmonyPatch(typeof(UnitAI_NewBasic), nameof(UnitAI_NewBasic.CreateSpeechBubble),
            new[] { typeof(string), typeof(bool), typeof(float), typeof(bool), typeof(Loc.KeywordValue[]) })]
        private static class EnterSpeech
        {
            [HarmonyPrefix]
            private static bool Before(UnitAI_NewBasic __instance, string key)
            {
                if (key == null || Rules.Level < 1 || !__instance.isServer || !(__instance is UnitAI_PantherRogueKing panther) || key != panther.tempStageEnter2Speech.key) return true;

                if (EnterShown.Add(panther)) BossDialogue.Say(panther, BossName, EncounterLine, Rules.Level);

                return false;
            }
        }

        [HarmonyPatch(typeof(UnitAI_BossBasic), nameof(UnitAI_BossBasic.OnBattleStartServer))]
        private static class DialogueStart
        {
            [HarmonyPostfix]
            private static void After(UnitAI_BossBasic __instance)
            {
                if (!(__instance is UnitAI_PantherRogueKing panther) || Rules.Level < 1) return;

                Unit_PantherRogueKing king = __instance.GetComponent<Unit_PantherRogueKing>();
                if (king == null) return;

                BossDialogue.ClearThresholds(panther);
                RushCastsDone.Remove(panther);
                RushDashes.Remove(panther);
                LastState[panther] = AiState(panther);
                BossDialogue.ResetDeathLine(panther);
                BossDialogue.RegisterDeathOverride(typeof(UnitAI_PantherRogueKing), BossName,
                    new[] { panther.bye1Speeech.key, panther.bye2Speeech.key }, DeathLine, () => Rules.Level);
                if (!EnterShown.Remove(panther)) BossDialogue.SayEncounter(panther, BossName, EncounterLine, Rules.Level);
            }
        }

        [HarmonyPatch(typeof(UnitAI_PantherRogueKing), "OnAIUpdate_FoundEnemy")]
        private static class Pulse
        {
            [HarmonyPrefix]
            private static void Before(UnitAI_PantherRogueKing __instance)
            {
                int casts;
                if (!RushCastsDone.TryGetValue(__instance, out casts) || AiState(__instance) != UnitAI_PantherRogueKing.State.None) return;

                Unit_PantherRogueKing king = __instance.GetComponent<Unit_PantherRogueKing>();
                if (king == null || !Active(king) || king.patternState != Unit_PantherRogueKing.EPatternState.Idle) return;

                if (casts >= RushCasts || __instance.CurrentTarget == null)
                {
                    RushCastsDone.Remove(__instance);
                    return;
                }

                RushCastsDone[__instance] = casts + 1;
                AiState(__instance) = UnitAI_PantherRogueKing.State.Rush;
                PatternCoroutine(__instance) = __instance.StartCoroutine((IEnumerator)RushPatternMethod.Invoke(__instance, null));
            }

            [HarmonyPostfix]
            private static void After(UnitAI_PantherRogueKing __instance)
            {
                Unit_PantherRogueKing king = __instance.GetComponent<Unit_PantherRogueKing>();
                if (king == null || !Active(king)) return;

                UnitAI_PantherRogueKing.State state = AiState(__instance);
                UnitAI_PantherRogueKing.State previous;
                LastState.TryGetValue(__instance, out previous);
                LastState[__instance] = state;
                if (state != previous && state != UnitAI_PantherRogueKing.State.None) OnPatternStarted(__instance, king, state);

                if (king.MaxHp > 0f && king.hp / king.MaxHp < LowHpRatio && BossDialogue.TryFireThreshold(__instance, "LowHp"))
                    BossDialogue.Say(__instance, BossName, LowHpLine, Rules.Level);
            }
        }

        private static bool Active(Unit_PantherRogueKing boss)
        {
            return Rules.Level >= 1 && boss.isServer;
        }

        private static bool IsHard()
        {
            return Rules.Level >= HardLevel;
        }

        [HarmonyPatch(typeof(Unit_PantherRogueKing), nameof(Unit_PantherRogueKing.CreateBasicAttack2Warning))]
        private static class BasicAttackWarning
        {
            [HarmonyPrefix]
            private static bool Before(Unit_PantherRogueKing __instance)
            {
                if (!Active(__instance)) return true;

                List<UI_AOEWarning_RangeAttackLine> warnings = BasicAttack2Warnings(__instance);
                warnings.Clear();
                for (float angle = 0f; angle < 360f; angle += BasicAttackStep)
                {
                    UI_AOEWarning_RangeAttackLine item = AOEWarningFactory.CreateAoe_RangeAttackLine(__instance.transform.position, (Vector2)__instance.transform.position + HorayUtility.GetVector2FromAngle(angle) * 4f, AOEWarningFactory.HostileWarningColor, 0.25f);
                    warnings.Add(item);
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(Unit_PantherRogueKing), nameof(Unit_PantherRogueKing.FireBasicAttack2Animation))]
        private static class BasicAttackBurst
        {
            [HarmonyPrefix]
            private static bool Before(Unit_PantherRogueKing __instance)
            {
                if (!Active(__instance)) return true;

                if (__instance.isServer)
                {
                    for (float angle = 0f; angle < 360f; angle += BasicAttackStep)
                        Bullet.Pool.Spawn(__instance.basicAttack2BulletPrefab, __instance.transform.position, false, EDamageFromType.DirectAttack, "BasicAttack", __instance.RequestStandardDamage() * 0.5f, 25, 3f, __instance, __instance.GetHostileFactionLayers(EDamageFromType.DirectAttack), 0.5f, __instance.transform.position, (Vector2)__instance.transform.position + HorayUtility.GetVector2FromAngle(angle), null, null);

                    if (IsHard())
                    {
                        for (float angle = 0f; angle < 360f; angle += BasicAttackHardBonusStep)
                            Bullet.Pool.Spawn(__instance.slowShurikenBulletPrefab, __instance.transform.position, false, EDamageFromType.DirectAttack, "BasicAttack", __instance.RequestStandardDamage(), 25, 3f, __instance, __instance.GetHostileFactionLayers(EDamageFromType.DirectAttack), 0.5f, __instance.transform.position, (Vector2)__instance.transform.position + HorayUtility.GetVector2FromAngle(angle), null, null).speedScale = Random.Range(0f, 0.1f) + 0.9f;
                    }
                }
                BasicAttack2Warnings(__instance).Clear();
                return false;
            }
        }

        [HarmonyPatch(typeof(Unit_PantherRogueKing), nameof(Unit_PantherRogueKing.FireSlowShurikenAnimation))]
        private static class SlowShurikenMain
        {
            [HarmonyPrefix]
            private static bool Before(Unit_PantherRogueKing __instance)
            {
                if (!Active(__instance)) return true;

                if (!__instance.slowShurikenThrowSound.IsNull)
                    RuntimeManager.PlayOneShot(__instance.slowShurikenThrowSound, __instance.transform.position);

                if (__instance.isServer)
                    FireShurikenFan(__instance, IsHard() ? SlowShurikenMainHard : SlowShurikenMainNormal, SlowShurikenFanSpan);
                return false;
            }
        }

        [HarmonyPatch(typeof(Unit_PantherRogueKing), nameof(Unit_PantherRogueKing.FireSlowShuriken2Animation))]
        private static class SlowShurikenSecond
        {
            [HarmonyPrefix]
            private static bool Before(Unit_PantherRogueKing __instance)
            {
                if (!Active(__instance)) return true;

                if (!__instance.slowShurikenThrowSound.IsNull)
                    RuntimeManager.PlayOneShot(__instance.slowShurikenThrowSound, __instance.transform.position);

                if (__instance.isServer)
                    FireShurikenFan(__instance, IsHard() ? SlowShurikenSecondHard : SlowShurikenSecondNormal, SlowShurikenFanSpan);
                return false;
            }
        }

        private static void FireShurikenFan(Unit_PantherRogueKing boss, int shots, float span)
        {
            if (!boss.isServer) return;

            float angle = HorayUtility.GetAngle(boss.CurrentLookingPosition, boss.transform.position);
            float step = shots <= 1 ? 0f : span * 2f / (shots - 1);
            for (int i = 0; i < shots; i++)
            {
                float offset = -span + step * i;
                Vector2 vector = HorayUtility.GetVector2FromAngle(angle + offset) * 3f;
                Bullet.Pool.Spawn(boss.slowShurikenBulletPrefab, boss.transform.position, false, EDamageFromType.DirectAttack, "BasicAttack", boss.RequestStandardDamage(), 25, 3f, boss, boss.GetHostileFactionLayers(EDamageFromType.DirectAttack), 0.5f, boss.transform.position, (Vector2)boss.transform.position + vector, null, null).speedScale = Random.Range(0f, 0.1f) + 0.9f;
            }
        }

        private static void FireRushRadial(Unit_PantherRogueKing boss)
        {
            if (!boss.isServer) return;

            for (int i = 0; i < RushRadialShots; i++)
            {
                float angle = i * RushRadialStep;
                Vector2 vector = HorayUtility.GetVector2FromAngle(angle) * 3f;
                Bullet.Pool.Spawn(boss.slowShurikenBulletPrefab, boss.transform.position, false, EDamageFromType.DirectAttack, "BasicAttack", boss.RequestStandardDamage(), 25, 3f, boss, boss.GetHostileFactionLayers(EDamageFromType.DirectAttack), 0.5f, boss.transform.position, (Vector2)boss.transform.position + vector, null, null).speedScale = Random.Range(0f, 0.1f) + 0.9f;
            }
        }

        [HarmonyPatch(typeof(Unit_PantherRogueKing), nameof(Unit_PantherRogueKing.Fury))]
        private static class RushDashCounter
        {
            [HarmonyPostfix]
            private static void After(Unit_PantherRogueKing __instance)
            {
                if (!Active(__instance)) return;

                UnitAI_PantherRogueKing ai = __instance.GetComponent<UnitAI_PantherRogueKing>();
                if (ai == null || AiState(ai) != UnitAI_PantherRogueKing.State.Rush) return;

                int dashes;
                RushDashes.TryGetValue(ai, out dashes);
                RushDashes[ai] = ++dashes;
                if (IsHard() && dashes % 2 == 0) FireRushRadial(__instance);
            }
        }

        [HarmonyPatch(typeof(UnitAI_PantherRogueKing), nameof(UnitAI_PantherRogueKing.CancelAction))]
        private static class RushChainCancel
        {
            [HarmonyPostfix]
            private static void After(UnitAI_PantherRogueKing __instance)
            {
                RushCastsDone.Remove(__instance);
            }
        }

        private static void OnPatternStarted(UnitAI_PantherRogueKing ai, Unit_PantherRogueKing king, UnitAI_PantherRogueKing.State state)
        {
            switch (state)
            {
                case UnitAI_PantherRogueKing.State.Rush:
                    if (RushCastsDone.ContainsKey(ai)) break;
                    RushCastsDone[ai] = 1;
                    RushDashes[ai] = 0;
                    BossDialogue.SayUnused(ai, BossName, RushLines, Rules.Level);
                    break;
                case UnitAI_PantherRogueKing.State.CloneDagger:
                    BossDialogue.SayUnused(ai, BossName, CloneDaggerLines, Rules.Level);
                    int extraShadows = IsHard() ? 2 : 1;
                    for (int i = 0; i < extraShadows; i++)
                        ai.StartCoroutine(ExtraAimShadow(ai, king));
                    break;
            }
        }

        private static Vector2 ShadowPosition(UnitAI_PantherRogueKing ai)
        {
            BossEnvironment_Panther env = PantherEnv(ai);
            return (Vector2)env.transform.position + env.cloneShurikenCloneAreaOffset + Random.insideUnitCircle.normalized * env.cloneShurikenCloneAreaRadius;
        }

        private static IEnumerator ExtraAimShadow(UnitAI_PantherRogueKing ai, Unit_PantherRogueKing king)
        {
            if (PantherEnv(ai) == null) yield break;

            PantherRogueKingShadow shadow = king.CreateShadow(ShadowPosition(ai));
            yield return new WaitForSeconds(CloneDaggerExtraSetupDelay);
            if (king == null || king.IsDead || ai.CurrentTarget == null)
            {
                king.DestroyShadow(shadow);
                yield break;
            }

            shadow.ThrowDagger(king, ai.CurrentTarget);
            yield return new WaitForSeconds(CloneDaggerExtraLifetime);
            king.DestroyShadow(shadow);
        }

        [HarmonyPatch(typeof(PantherRogueKingShadow), nameof(PantherRogueKingShadow.RandomDagger), typeof(UnitAvatar), typeof(Vector3))]
        private static class SecondShadowSlowShuriken
        {
            [HarmonyPrefix]
            private static bool Before(PantherRogueKingShadow __instance, UnitAvatar parent, Vector3 targetPosition)
            {
                Unit_PantherRogueKing king = parent as Unit_PantherRogueKing;
                if (king == null || !Active(king)) return true;

                UnitAI_PantherRogueKing ai = king.GetComponent<UnitAI_PantherRogueKing>();
                if (ai == null || AiState(ai) != UnitAI_PantherRogueKing.State.CloneRandomDagger) return true;
                if (ShadowList(king).IndexOf(__instance) != 1) return true;

                FireShadowSlowShuriken(__instance, king, targetPosition);
                return false;
            }
        }

        private static void FireShadowSlowShuriken(PantherRogueKingShadow shadow, Unit_PantherRogueKing king, Vector3 targetPos)
        {
            if (shadow == null || !shadow.isServer) return;

            float angle = HorayUtility.GetAngle(targetPos, shadow.transform.position);
            for (float offset = -40f; offset <= 40f; offset += 20f)
            {
                Vector2 vector = HorayUtility.GetVector2FromAngle(angle + offset) * 3f;
                Bullet.Pool.Spawn(king.slowShurikenBulletPrefab, shadow.transform.position, false, EDamageFromType.DirectAttack, "BasicAttack", king.RequestStandardDamage() * 0.75f, 25, 3f, king, king.GetHostileFactionLayers(EDamageFromType.DirectAttack), 0.5f, shadow.transform.position, (Vector2)shadow.transform.position + vector, null, null).speedScale = Random.Range(0f, 0.1f) + 0.9f;
            }
        }
    }
}
