using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace SephiriaExpanded
{
    internal sealed class BossChallenge_MadArmadillo : IBossChallenge
    {
        private static BossChallengeRules Rules => BossChallenge.MadArmadillo;

        public BossChallenge_MadArmadillo()
        {
            BossDialogue.RegisterEncounter(typeof(UnitAI_MadArmadillo), ai =>
            {
                UnitAI_MadArmadillo mad = ai as UnitAI_MadArmadillo;
                if (Rules.Level >= 1 && mad != null && mad.NetworkgolemAi != null) BossDialogue.SayEncounter(mad.NetworkgolemAi, BossName, Greeting(), Rules.Level);
            });
        }

        private static string Greeting()
        {
            return Rules.Level >= 2 ? GreetingHard : GreetingNormal;
        }

        private const int HardLevel = 2;
        private const int StompCountFixed = 5;
        private const int ShockwaveCountFixed = 5;
        private const int SpreadBulletSetsFixed = 5;
        private const int SpreadBulletShotsNormal = 18;
        private const int SpreadBulletShotsHard = 20;
        private const float SpreadBulletIntervalNormal = 0.66f;
        private const float SpreadBulletIntervalHard = 0.56f;
        private const float SpreadBulletSpan = 180f;
        private const float Phase4MoveSpeedBonus = 0.2f;
        private const float Phase4LaserSpeedBonus = 0.3f;
        private const float Phase5MissileInterval = 1f;

        private const string BossName = "골렘";
        private const string ErmaName = "에르마";
        private const string ErmaScreamLine = "내.. 걸작이..!!";
        private const string GreetingNormal ="위협 등급 B. 제압 시퀀스 가동";
        private const string GreetingHard = "위협 등급 S. 전멸 시퀀스 가동";

        private const string Phase4GreetingRaw = "접À근û´이ëÀÚ상¸¦ ¼¶¸감êÇ지Õ´Ï´Ù. ";
        private static readonly string Phase5Greeting = ColorizeKorean("제ì´ê±°거í¸¦ë¤모ÀÚ°íë¤¸듈±ê´Ïê°가¸¦ë¤ÀÚ동.", "red");

        private static UnitAI_MadArmadillo ExitingErma;

        private static readonly HashSet<Unit_ArmadilloGolem> ExtendingShockwave = new HashSet<Unit_ArmadilloGolem>();
        private static readonly HashSet<UnitAI_ArmadilloGolem> Phase4Started = new HashSet<UnitAI_ArmadilloGolem>();
        private static readonly HashSet<UnitAI_ArmadilloGolem> Phase5Started = new HashSet<UnitAI_ArmadilloGolem>();

        private static readonly AccessTools.FieldRef<UnitAI_ArmadilloGolem, Vector2> HandAreaBottomLeft =
            AccessTools.FieldRefAccess<UnitAI_ArmadilloGolem, Vector2>("handAreaBottomLeft");

        private static readonly AccessTools.FieldRef<UnitAI_ArmadilloGolem, Vector2> HandAreaTopRight =
            AccessTools.FieldRefAccess<UnitAI_ArmadilloGolem, Vector2>("handAreaTopRight");

        private static string ColorizeKorean(string text, string colorName)
        {
            StringBuilder builder = new StringBuilder();
            foreach (char c in text)
            {
                if (c >= '가' && c <= '힣') builder.Append("<color=").Append(colorName).Append('>').Append(c).Append("</color>");
                else builder.Append(c);
            }

            return builder.ToString();
        }

        public void Tick()
        {
        }

        public void Cleanup()
        {
            ExitingErma = null;
            ExtendingShockwave.Clear();
            Phase4Started.Clear();
            Phase5Started.Clear();
        }

        private static bool IsHard()
        {
            return Rules.Level >= HardLevel;
        }

        private static int ExtraLives()
        {
            if (Rules.Level >= 2) return 2;
            if (Rules.Level >= 1) return 1;
            return 0;
        }

        [HarmonyPatch(typeof(UnitAI_MadArmadillo), nameof(UnitAI_MadArmadillo.Life), MethodType.Getter)]
        private static class LifeBoost
        {
            [HarmonyPostfix]
            private static void After(ref int __result)
            {
                __result += ExtraLives();
            }
        }

        [HarmonyPatch(typeof(UnitAI_BossBasic), nameof(UnitAI_BossBasic.OnBattleStartServer))]
        private static class GreetingSpeech
        {
            [HarmonyPostfix]
            private static void After(UnitAI_BossBasic __instance)
            {
                if (!__instance.isServer || !(__instance is UnitAI_MadArmadillo mad)) return;

                UnitAI_ArmadilloGolem golemAi = mad.NetworkgolemAi;
                if (golemAi == null) return;

                if (Rules.Level < 1) return;

                if (BossDialogue.SayEncounter(golemAi, BossName, Greeting(), Rules.Level)) BossDialogue.Hold(mad, golemAi);
            }
        }

        [HarmonyPatch(typeof(UnitAI_MadArmadillo), "ExitFromGolem")]
        private static class GolemDeathLine
        {
            [HarmonyPrefix]
            private static void Before(UnitAI_MadArmadillo __instance)
            {
                if (__instance.isServer && Rules.Level >= 1) ExitingErma = __instance;
            }

            [HarmonyPostfix]
            private static void After(UnitAI_MadArmadillo __instance)
            {
                if (ExitingErma != __instance) return;

                ExitingErma = null;
                BossDialogue.Say(__instance, ErmaName, ErmaScreamLine, Rules.Level);
            }

            [HarmonyFinalizer]
            private static void Finish()
            {
                ExitingErma = null;
            }
        }

        [HarmonyPatch(typeof(UnitAI_NewBasic), nameof(UnitAI_NewBasic.CreateSpeechBubble),
            new[] { typeof(string), typeof(bool), typeof(float), typeof(bool), typeof(Loc.KeywordValue[]) })]
        private static class SuppressVanillaScream
        {
            [HarmonyPrefix]
            private static bool Before(UnitAI_NewBasic __instance)
            {
                return ExitingErma == null || __instance != ExitingErma;
            }
        }

        [HarmonyPatch(typeof(Unit_ArmadilloGolem_Hand), nameof(Unit_ArmadilloGolem_Hand.ApplyDamage))]
        private static class DisableHandHitbox
        {
            [HarmonyPrefix]
            private static bool Before(ref EApplyDamageResult __result)
            {
                if (Rules.Level < 1) return true;

                __result = EApplyDamageResult.Fail_Absolute;
                return false;
            }
        }

        [HarmonyPatch(typeof(Unit_ArmadilloGolem), nameof(Unit_ArmadilloGolem.HandPattern_Stomp))]
        private static class StompCount
        {
            [HarmonyPrefix]
            private static void Before(ref int count)
            {
                if (Rules.Level < 1) return;

                count = StompCountFixed;
            }
        }

        [HarmonyPatch(typeof(Unit_ArmadilloGolem_Hand), "RpcStompDown")]
        private static class StompImpactQuake
        {
            [HarmonyPostfix]
            private static void After(Unit_ArmadilloGolem_Hand __instance)
            {
                if (!IsHard() || !__instance.isServer) return;

                Unit_ArmadilloGolem golem = __instance.parent as Unit_ArmadilloGolem;
                if (golem == null || golem.HandState != Unit_ArmadilloGolem.EHandState.Stomping) return;

                __instance.FireShockwave();
            }
        }

        [HarmonyPatch(typeof(Unit_ArmadilloGolem), nameof(Unit_ArmadilloGolem.HandPattern_ShockwaveSmash))]
        private static class ShockwaveExtend
        {
            [HarmonyPostfix]
            private static void After(Unit_ArmadilloGolem __instance, Transform target)
            {
                if (Rules.Level < 1 || ExtendingShockwave.Contains(__instance)) return;

                ExtendingShockwave.Add(__instance);
                __instance.StartCoroutine(ExtendShockwaveRoutine(__instance, target));
            }
        }

        private static IEnumerator ExtendShockwaveRoutine(Unit_ArmadilloGolem golem, Transform target)
        {
            for (int i = 0; i < ShockwaveCountFixed - 3; i++)
            {
                while (golem != null && !golem.IsDead && golem.HandState != Unit_ArmadilloGolem.EHandState.Idle) yield return null;
                if (golem == null || golem.IsDead) break;

                golem.HandPattern_ShockwaveSmash(target);
            }

            ExtendingShockwave.Remove(golem);
        }

        [HarmonyPatch(typeof(Unit_ArmadilloGolem), nameof(Unit_ArmadilloGolem.HeadPattern_SpreadBullet))]
        private static class SpreadBulletTuning
        {
            [HarmonyPrefix]
            private static void Before(Unit_ArmadilloGolem __instance)
            {
                if (Rules.Level < 1) return;

                __instance.spreadBulletCount = SpreadBulletSetsFixed;
                __instance.spreadBulletTimer.time = IsHard() ? SpreadBulletIntervalHard : SpreadBulletIntervalNormal;
            }
        }

        private static float SpreadBulletStep()
        {
            if (Rules.Level < 1) return 12f;

            int shots = IsHard() ? SpreadBulletShotsHard : SpreadBulletShotsNormal;
            return SpreadBulletSpan / (shots - 1);
        }

        [HarmonyPatch(typeof(Unit_ArmadilloGolem), "Update")]
        private static class SpreadBulletStepPatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (CodeInstruction instruction in instructions)
                {
                    if (instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float value && value == 12f)
                    {
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BossChallenge_MadArmadillo), nameof(SpreadBulletStep)));
                        continue;
                    }

                    yield return instruction;
                }
            }
        }

        private static Vector3 ClampToArena(Vector3 position, Vector2 bottomLeft, Vector2 topRight)
        {
            position.x = Mathf.Clamp(position.x, bottomLeft.x + 1f, topRight.x - 1f);
            position.y = Mathf.Clamp(position.y, bottomLeft.y + 1f, topRight.y - 1f);
            return position;
        }

        private static void FireMissileLine(UnitAI_ArmadilloGolem ai, Unit_ArmadilloGolem golem, Vector3 aimPosition)
        {
            Vector2 bottomLeft = HandAreaBottomLeft(ai);
            Vector2 topRight = HandAreaTopRight(ai);
            aimPosition = ClampToArena(aimPosition, bottomLeft, topRight);

            Vector3 start = new Vector3(
                aimPosition.x + Random.Range(2f, 6f) * ((aimPosition.x > (bottomLeft.x + topRight.x) / 2f) ? 1f : -1f) * ((aimPosition.y > (bottomLeft.y + topRight.y) / 2f) ? 1f : -1f),
                topRight.y + 5f);
            Vector3 direction = (aimPosition - start).normalized;

            Vector3 cursor = aimPosition;
            while (cursor.y < topRight.y - 2f && cursor.x >= bottomLeft.x && cursor.x <= topRight.x)
                cursor += -direction * ai.missileGap;

            int index = 0;
            for (float distance = 0f; distance < 20f; distance += ai.missileGap)
            {
                Vector3 point = cursor + direction * distance;
                if (point.y <= bottomLeft.y) break;

                float time = ai.missileWarning + ai.missileFireInterval * index;
                golem.LaunchMissile(point + (Vector3)Random.insideUnitCircle * ai.missileError, time, time - ai.missileWarning, ai.missileWarning);
                index++;
            }
        }

        private static void FireMissileConverge(UnitAI_ArmadilloGolem ai, Unit_ArmadilloGolem golem)
        {
            BossEnvironment_MadArmadillo env = ai.armadilloEnvironment;
            if (env == null || ai.CurrentTarget == null) return;

            Vector2 bottomLeft = HandAreaBottomLeft(ai);
            Vector2 topRight = HandAreaTopRight(ai);

            float leftX = env.transform.position.x + env.bossZoneLeftSide;
            float rightX = env.transform.position.x + env.bossZoneRightSide;
            float topY = env.transform.position.y + env.bossZoneTopSide;
            float bottomY = env.transform.position.y + env.bossZoneBottomSide;

            Vector3 targetPos = ai.CurrentTarget.transform.position;
            Vector3 start = new Vector3(
                targetPos.x + Random.Range(2f, 6f) * ((targetPos.x > (bottomLeft.x + topRight.x) / 2f) ? 1f : -1f) * ((targetPos.y > (bottomLeft.y + topRight.y) / 2f) ? 1f : -1f),
                topRight.y + 5f);
            Vector3 direction = (targetPos - start).normalized;

            Vector3 cursor = targetPos;
            while (cursor.y < topRight.y - 2f && cursor.x >= bottomLeft.x && cursor.x <= topRight.x)
                cursor += -direction * ai.missileGap;

            int index = 0;
            for (float distance = 0f; distance < 20f; distance += ai.missileGap)
            {
                Vector3 point = cursor + direction * distance;
                if (point.y <= bottomLeft.y) break;

                float time = ai.missileWarning + ai.missileFireInterval * index;
                for (int i = 1; i < 5; i++)
                {
                    Vector3 left = point + new Vector3(-i * 3.6f, Random.Range(0f, 0.0625f));
                    if (left.x >= leftX && left.x <= rightX && left.y <= topY && left.y >= bottomY)
                        golem.LaunchMissile(left + (Vector3)Random.insideUnitCircle * ai.missileError, time, time - ai.missileWarning, ai.missileWarning);
                }

                for (int j = 1; j < 5; j++)
                {
                    Vector3 right = point + new Vector3(j * 3.6f, Random.Range(0f, 0.0625f));
                    if (right.x >= leftX && right.x <= rightX && right.y <= topY && right.y >= bottomY)
                        golem.LaunchMissile(right + (Vector3)Random.insideUnitCircle * ai.missileError, time, time - ai.missileWarning, ai.missileWarning);
                }

                index++;
            }
        }

        private static void FireMissileGrid(UnitAI_ArmadilloGolem ai, Unit_ArmadilloGolem golem)
        {
            BossEnvironment_MadArmadillo env = ai.armadilloEnvironment;
            if (env == null || ai.CurrentTarget == null) return;

            float leftX = env.transform.position.x + env.bossZoneLeftSide;
            float rightX = env.transform.position.x + env.bossZoneRightSide;
            float topY = env.transform.position.y + env.bossZoneTopSide;
            float bottomY = env.transform.position.y + env.bossZoneBottomSide;
            float centerX = env.transform.position.x + (env.bossZoneLeftSide + env.bossZoneRightSide) / 2f;

            Vector2 dirA, dirB, originA, originB;
            if (Random.Range(0f, 100f) >= 50f)
            {
                dirA = new Vector2(rightX - leftX, bottomY - topY).normalized;
                dirB = new Vector2(leftX - rightX, bottomY - topY).normalized;
                originA = new Vector2(leftX, topY);
                originB = new Vector2(rightX, topY);
            }
            else
            {
                dirA = new Vector2(0f, -1f);
                dirB = new Vector2(1.6f, 0f);
                originA = new Vector2(centerX, topY);
                originB = new Vector2(leftX, ai.CurrentTarget.transform.position.y);
            }

            int index = 0;
            for (float distance = 0f; distance < 20f; distance += ai.missileGap)
            {
                float time = ai.missileWarning + ai.missileFireInterval * index;
                Vector2 pointA = originA + dirA * distance;
                if (pointA.x >= leftX && pointA.x <= rightX && pointA.y <= topY && pointA.y >= bottomY)
                    golem.LaunchMissile((Vector3)pointA + (Vector3)Random.insideUnitCircle * ai.missileError, time, time - ai.missileWarning, ai.missileWarning);

                Vector2 pointB = originB + dirB * distance;
                if (pointB.x >= leftX && pointB.x <= rightX && pointB.y <= topY && pointB.y >= bottomY)
                    golem.LaunchMissile((Vector3)pointB + (Vector3)Random.insideUnitCircle * ai.missileError, time, time - ai.missileWarning, ai.missileWarning);

                index++;
            }
        }

        [HarmonyPatch(typeof(UnitAI_ArmadilloGolem), "OnAIUpdate_FoundEnemy")]
        private static class PhaseOverride
        {
            [HarmonyPrefix]
            private static bool Before(UnitAI_ArmadilloGolem __instance)
            {
                if (Rules.Level < 1 || !__instance.isServer) return true;

                int phase = CurrentPhase(__instance);
                if (phase == 4)
                {
                    StartPhase4(__instance);
                    return false;
                }

                if (phase == 5 && IsHard())
                {
                    StartPhase5(__instance);
                    return false;
                }

                return true;
            }
        }

        private static int CurrentPhase(UnitAI_ArmadilloGolem ai)
        {
            Unit_ArmadilloGolem golem = ai.GetComponent<Unit_ArmadilloGolem>();
            UnitAvatar owner = golem != null ? golem.owner : null;
            UnitAI_MadArmadillo ownerAi = owner != null ? owner.GetComponent<UnitAI_MadArmadillo>() : null;
            if (ownerAi == null) return 0;

            return ownerAi.Life - ownerAi.CurrentLife + 1;
        }

        private static void StartPhase4(UnitAI_ArmadilloGolem ai)
        {
            if (Phase4Started.Contains(ai)) return;

            Unit_ArmadilloGolem golem = ai.GetComponent<Unit_ArmadilloGolem>();
            if (golem == null) return;

            Phase4Started.Add(ai);
            string phase4Color = Rules.Level >= 2 ? "red" : "yellow";
            string phase4Greeting = ColorizeKorean(Phase4GreetingRaw, phase4Color);
            BossDialogue.SayRaw(ai, BossName, phase4Greeting, phase4Greeting, phase4Color);
            ai.StartCoroutine(Phase4Loop(ai, golem));
        }

        private static IEnumerator Phase4Loop(UnitAI_ArmadilloGolem ai, Unit_ArmadilloGolem golem)
        {
            while (golem != null && !golem.IsDead && CurrentPhase(ai) == 4)
            {
                if (ai.CurrentTarget != null)
                {
                    switch (Random.Range(0, 3))
                    {
                        case 0: FireMissileLine(ai, golem, ai.CurrentTarget.transform.position); break;
                        case 1: FireMissileConverge(ai, golem); break;
                        default: FireMissileGrid(ai, golem); break;
                    }
                }

                yield return new WaitForSeconds(Phase5MissileInterval);
            }

            Phase4Started.Remove(ai);
        }

        private static void StartPhase5(UnitAI_ArmadilloGolem ai)
        {
            if (Phase5Started.Contains(ai)) return;

            Unit_ArmadilloGolem golem = ai.GetComponent<Unit_ArmadilloGolem>();
            if (golem == null) return;

            Phase5Started.Add(ai);
            golem.NetworkmoveSpeedMultiplier = golem.moveSpeedMultiplier + Phase4MoveSpeedBonus;
            golem.laseSpeed_left *= 1f + Phase4LaserSpeedBonus;
            golem.laseSpeed_right *= 1f + Phase4LaserSpeedBonus;
            BossDialogue.SayRaw(ai, BossName, Phase5Greeting, Phase5Greeting, "red");
            ai.StartCoroutine(Phase5Loop(ai, golem));
        }

        private static IEnumerator Phase5Loop(UnitAI_ArmadilloGolem ai, Unit_ArmadilloGolem golem)
        {
            Vector2 bottomLeft = HandAreaBottomLeft(ai);
            Vector2 topRight = HandAreaTopRight(ai);

            while (golem != null && !golem.IsDead && CurrentPhase(ai) == 5)
            {
                if (ai.CurrentTarget != null && golem.HeadState == Unit_ArmadilloGolem.EHeadState.Idle && golem.HandState == Unit_ArmadilloGolem.EHandState.Idle)
                {
                    golem.HeadPattern_Subshot(ai.CurrentTarget.transform);
                    golem.HandPattern_Laser(bottomLeft.x, topRight.x, bottomLeft.y - 0.75f, topRight.y - 1f);
                }

                yield return null;
            }

            if (golem != null && !golem.IsDead) golem.HeadPattern_StopSubshot();
            Phase5Started.Remove(ai);
        }
    }
}
