using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SephiriaExpanded
{
    internal sealed class BossChallenge_Askard : IBossChallenge
    {
        private static BossChallengeRules Rules => BossChallenge.Askard;

        private const string BossName = "아스카드";
        private const string EncounterLine = "발자국을 겹쳐 새길수록, 네가 다다를 곳은 똑같은 폐허뿐이다.";
        private const string DeathLine = "인정하마... 네가 이겼다.";
        private static readonly string[] StompLines = { "도망쳐라.", "무의미하다.", "허망한 몸짓이다." };
        private static readonly string[] LaserLines = { "눈빛이 가엽구나.", "증언해라.", "어리석도다." };
        private static readonly string[] HpKeys = { "Hp75", "Hp50", "Hp25" };
        private static readonly string[] HpLines = { "겨우 이 정도인가?", "절망을 직시해라!", "제법이군... 하지만.." };
        private static readonly float[] HpRatios = { 0.75f, 0.5f, 0.25f };

        private const float DarknessVanillaStart = 1.25f;
        private const float DarknessVanillaEnd = 0.45f;
        private const float DarknessStart = 1.2f;
        private const float DarknessEnd = 0.4f;

        private const float WaveVanillaDelay = 0.75f;
        private const float WaveNormalDelay = 0.66f;
        private const float WaveHardDelay = 0.5f;
        private const float FillNormalDelay = 0.88f;
        private const float FillHardDelay = 0.75f;
        private const int PointRayLength = 15;
        private const float PointRayStep = 0.05f;
        private const float PointRayAngle = 30f;
        private const float PointRayJitterDivisor = 5f;
        private const float PointRayRadius = 0.1f;
        private const float LineSideAngle = 45f;
        private const int LineMaxSteps = 20;
        private const float LinePathRadius = 0.1f;
        private const float LineSpreadDivisor = 10f;
        private const int StompNormalBullets = 2;
        private const int StompHardBullets = 4;
        private const float StompWait = 1.88f;
        private const float StompRepeatDelay = 1.5f;
        private const float LaserHardWait = 1.75f;
        private const int LaserEndAnimation = 11;
        private const float BloodRainDelay = 1f;
        private const int BloodRainDamageRate = 4;

        private struct SweepInfo
        {
            internal float Start;
            internal int Direction;
        }

        private static readonly AccessTools.FieldRef<Unit_Askard_Chapter3, BulletMoveModule_Laser> CurLaser =
            AccessTools.FieldRefAccess<Unit_Askard_Chapter3, BulletMoveModule_Laser>("curLaser");

        private static readonly AccessTools.FieldRef<Unit_Askard_Chapter3, System.Action> OnPatternAttack =
            AccessTools.FieldRefAccess<Unit_Askard_Chapter3, System.Action>("onPatternAttack");

        private static readonly AccessTools.FieldRef<Unit_Askard_Chapter3, Transform> CurrentTarget =
            AccessTools.FieldRefAccess<Unit_Askard_Chapter3, Transform>("currentTarget");

        private static readonly FieldInfo PointList = AccessTools.Field(typeof(Unit_Askard_Chapter3), "tentaclePointDataList");
        private static readonly System.Type PointType = AccessTools.Inner(typeof(Unit_Askard_Chapter3), "TentaclePointData");

        private static readonly Dictionary<Unit_Askard_Chapter3, UnitAI_Askard_Chapter3> Ais = new Dictionary<Unit_Askard_Chapter3, UnitAI_Askard_Chapter3>();
        private static readonly Dictionary<Unit_Askard_Chapter3, float[]> BaseValues = new Dictionary<Unit_Askard_Chapter3, float[]>();
        private static readonly Dictionary<Unit_Askard_Chapter3, SweepInfo> Sweeps = new Dictionary<Unit_Askard_Chapter3, SweepInfo>();
        private static readonly MethodInfo ActionSetter = AccessTools.PropertySetter(typeof(Unit_Askard_Chapter3), nameof(Unit_Askard_Chapter3.CurrentAction));
        private static readonly HashSet<Unit_Askard_Chapter3> Repeating = new HashSet<Unit_Askard_Chapter3>();
        private static readonly HashSet<UnitAI_Askard_Chapter3> EnterShown = new HashSet<UnitAI_Askard_Chapter3>();
        private static readonly Collider2D[] RayBuffer = new Collider2D[16];
        private static bool sendingEndAnimation;

        public void Tick()
        {
        }

        public void Cleanup()
        {
            Ais.Clear();
            BaseValues.Clear();
            Sweeps.Clear();
            EnterShown.Clear();
            Repeating.Clear();
        }

        private static bool IsHard()
        {
            return Rules.Level >= 2;
        }

        private static bool IsEnhanced()
        {
            return Rules.Level >= 1;
        }

        private static MethodBase CoroutineMoveNext(string coroutineName)
        {
            System.Type type = AccessTools.FirstInner(typeof(Unit_Askard_Chapter3), inner => inner.Name.Contains(coroutineName));
            return AccessTools.Method(type, "MoveNext");
        }

        private static void ReplaceWithCall(CodeInstruction instruction, string methodName)
        {
            instruction.opcode = OpCodes.Call;
            instruction.operand = AccessTools.Method(typeof(BossChallenge_Askard), methodName);
        }

        private static UnitAI_Askard_Chapter3 AiOf(Unit_Askard_Chapter3 unit)
        {
            UnitAI_Askard_Chapter3 ai;
            if (!Ais.TryGetValue(unit, out ai) || ai == null)
            {
                ai = unit.GetComponent<UnitAI_Askard_Chapter3>();
                Ais[unit] = ai;
            }

            return ai;
        }

        private static float[] BaseOf(Unit_Askard_Chapter3 unit)
        {
            float[] values;
            if (!BaseValues.TryGetValue(unit, out values))
            {
                values = new float[] { unit.stompBulletCount, unit.stompWaitTime, unit.laserFireWaitTime, unit.tentacleFillDelay };
                BaseValues[unit] = values;
            }

            return values;
        }

        private static void Say(Unit_Askard_Chapter3 unit, string text)
        {
            UnitAI_Askard_Chapter3 ai = AiOf(unit);
            if (ai != null) BossDialogue.Say(ai, BossName, text, Rules.Level);
        }

        private static void SayRandom(Unit_Askard_Chapter3 unit, string[] lines)
        {
            UnitAI_Askard_Chapter3 ai = AiOf(unit);
            if (ai != null) BossDialogue.SayUnused(ai, BossName, lines, Rules.Level);
        }

        [HarmonyPatch(typeof(UnitAI_BossBasic), nameof(UnitAI_BossBasic.OnBattleStartServer))]
        private static class BattleStart
        {
            [HarmonyPostfix]
            private static void After(UnitAI_BossBasic __instance)
            {
                if (!IsEnhanced() || !(__instance is UnitAI_Askard_Chapter3 askard)) return;

                BossDialogue.ClearThresholds(askard);
                BossDialogue.ResetDeathLine(askard);
                List<string> keys = new List<string>();
                foreach (LocalizedString speech in askard.deadlySpeechs)
                    keys.Add(speech.key);

                BossDialogue.RegisterDeathOverride(typeof(UnitAI_Askard_Chapter3), BossName, keys.ToArray(), DeathLine, () => Rules.Level);
                if (!EnterShown.Remove(askard)) BossDialogue.SayEncounter(askard, BossName, EncounterLine, Rules.Level);
            }
        }

        [HarmonyPatch(typeof(UnitAI_NewBasic), nameof(UnitAI_NewBasic.CreateSpeechBubble),
            new[] { typeof(string), typeof(bool), typeof(float), typeof(bool), typeof(Loc.KeywordValue[]) })]
        private static class EnterSpeech
        {
            [HarmonyPrefix]
            private static bool Before(UnitAI_NewBasic __instance, string key)
            {
                if (key == null || !IsEnhanced() || !__instance.isServer || !(__instance is UnitAI_Askard_Chapter3 askard)) return true;

                bool matches = false;
                foreach (LocalizedString speech in askard.firstEnterSpeechs)
                {
                    if (speech.key != key) continue;

                    matches = true;
                    break;
                }

                if (!matches) return true;

                if (EnterShown.Add(askard)) BossDialogue.Say(askard, BossName, EncounterLine, Rules.Level);

                return false;
            }
        }

        [HarmonyPatch(typeof(UnitAI_Askard_Chapter3), "BeginDramaticDie")]
        private static class DeathSpeech
        {
            [HarmonyPostfix]
            private static void After(UnitAI_Askard_Chapter3 __instance)
            {
                if (!IsEnhanced() || !__instance.isServer || __instance.deadlySpeechs.Count > 0) return;

                if (BossDialogue.TryFireThreshold(__instance, "Death")) BossDialogue.Say(__instance, BossName, DeathLine, Rules.Level);
            }
        }

        [HarmonyPatch(typeof(Unit_Askard_Chapter3), "Update")]
        private static class ThresholdWatch
        {
            [HarmonyPostfix]
            private static void After(Unit_Askard_Chapter3 __instance)
            {
                if (!__instance.isServer || !IsEnhanced() || __instance.IsDead || __instance.MaxHp <= 0f) return;

                float ratio = __instance.hp / __instance.MaxHp;
                int step = 0;
                while (step < HpRatios.Length && ratio < HpRatios[step])
                    step++;

                if (step == 0) return;

                UnitAI_Askard_Chapter3 ai = AiOf(__instance);
                if (ai == null) return;

                bool announce = false;
                for (int i = 0; i < step; i++)
                    announce = BossDialogue.TryFireThreshold(ai, HpKeys[i]) && i == step - 1;

                if (announce) BossDialogue.Say(ai, BossName, HpLines[step - 1], Rules.Level);
            }
        }

        private static float DarknessStartInterval()
        {
            return IsEnhanced() ? DarknessStart : DarknessVanillaStart;
        }

        private static float DarknessEndInterval()
        {
            return IsEnhanced() ? DarknessEnd : DarknessVanillaEnd;
        }

        [HarmonyPatch]
        private static class DarknessIntervalPatch
        {
            [HarmonyTargetMethod]
            private static MethodBase Target()
            {
                return CoroutineMoveNext("Darkness");
            }

            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (CodeInstruction instruction in instructions)
                {
                    if (instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float value)
                    {
                        if (value == DarknessVanillaStart) ReplaceWithCall(instruction, nameof(DarknessStartInterval));
                        else if (value == DarknessVanillaEnd) ReplaceWithCall(instruction, nameof(DarknessEndInterval));
                    }

                    yield return instruction;
                }
            }
        }

        private static float WaveInterval()
        {
            return IsHard() ? WaveHardDelay : IsEnhanced() ? WaveNormalDelay : WaveVanillaDelay;
        }

        [HarmonyPatch]
        private static class WaveIntervalPatch
        {
            [HarmonyTargetMethod]
            private static MethodBase Target()
            {
                return CoroutineMoveNext("CreateTentacleWave");
            }

            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (CodeInstruction instruction in instructions)
                {
                    if (instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float value && value == WaveVanillaDelay)
                        ReplaceWithCall(instruction, nameof(WaveInterval));

                    yield return instruction;
                }
            }
        }

        [HarmonyPatch(typeof(Unit_Askard_Chapter3), nameof(Unit_Askard_Chapter3.StartTentacle_Fill))]
        private static class FillPatch
        {
            [HarmonyPrefix]
            private static void Before(Unit_Askard_Chapter3 __instance)
            {
                if (!__instance.isServer) return;

                float[] values = BaseOf(__instance);
                __instance.tentacleFillDelay = IsHard() ? FillHardDelay : IsEnhanced() ? FillNormalDelay : values[3];
            }
        }

        private static object NewPoint(Vector3 position, float delay)
        {
            return System.Activator.CreateInstance(PointType, position, delay);
        }

        private static bool IsBlocked(Vector3 position, float radius)
        {
            int count = HorayPhysics2D.OverlapCircle(position, radius, RayBuffer, CombatManager.TileLayerMask);
            for (int i = 0; i < count; i++)
                if (CombatManager.Instance.IsTile(RayBuffer[i].transform.gameObject)) return true;

            return false;
        }

        [HarmonyPatch(typeof(Unit_Askard_Chapter3), nameof(Unit_Askard_Chapter3.CreateTentacle_Point))]
        private static class PointExtraRays
        {
            [HarmonyPostfix]
            private static void After(Unit_Askard_Chapter3 __instance)
            {
                if (!__instance.isServer || !IsEnhanced()) return;

                IList list = (IList)PointList.GetValue(__instance);
                Transform target = CurrentTarget(__instance);
                float baseAngle = target != null ? (target.position - __instance.transform.position).GetAngle() : 45f * Random.Range(0, 2);
                AddRay(__instance, list, baseAngle + (Random.Range(0, 2) == 0 ? PointRayAngle : -PointRayAngle));
            }
        }

        private static void AddRay(Unit_Askard_Chapter3 unit, IList list, float angle)
        {
            Vector3 previous = unit.transform.position;
            float jitter = 0f;
            for (int i = 0; i < PointRayLength; i++)
            {
                Vector3 direction = HorayUtility.GetVector3FromAngle(angle + Random.Range(-jitter, jitter));
                Vector3 point = previous + direction * unit.tentaclePointDistance;
                if (i == 0) point -= direction * unit.tentaclePointDistance * 0.5f;

                jitter = Mathf.Min(jitter + unit.tentaclePointRandomAngle / PointRayJitterDivisor, unit.tentaclePointRandomAngle);
                if (IsBlocked(point, PointRayRadius)) break;

                list.Add(NewPoint(point, i * PointRayStep));
                previous = point;
            }
        }

        [HarmonyPatch(typeof(Unit_Askard_Chapter3), nameof(Unit_Askard_Chapter3.AttackAnimation))]
        private static class LineSideLines
        {
            [HarmonyPrefix]
            private static void Before(Unit_Askard_Chapter3 __instance)
            {
                if (!__instance.isServer || !IsEnhanced() || __instance.CurrentAction != Unit_Askard.EAction.Tentacle_Line || OnPatternAttack(__instance) == null) return;

                IList list = (IList)PointList.GetValue(__instance);
                Vector3 origin = __instance.transform.position;
                float mainAngle = HorayUtility.GetAngleFromVector((__instance.CurrentLookingPosition - (Vector2)origin).normalized);
                AddLine(__instance, list, origin, mainAngle + LineSideAngle);
                AddLine(__instance, list, origin, mainAngle - LineSideAngle);
            }
        }

        private static void AddLine(Unit_Askard_Chapter3 unit, IList list, Vector3 origin, float angle)
        {
            List<Vector3> path = new List<Vector3>();
            Vector3 position = origin;
            float spread = 0f;
            for (int i = 0; i < LineMaxSteps; i++)
            {
                position += HorayUtility.GetVector3FromAngle(angle + Random.Range(-spread, spread)) * unit.tentacleLineMoveLength;
                bool blocked = IsBlocked(position, LinePathRadius);
                spread = Mathf.Min(spread + unit.tentaclePointRandomAngle / LineSpreadDivisor, unit.tentaclePointRandomAngle);
                if (blocked) break;

                path.Add(position);
            }

            for (int i = 0; i < path.Count; i++)
                list.Add(NewPoint(path[i], unit.tentacleLineMoveDelay * i));
        }

        [HarmonyPatch(typeof(Unit_Askard_Chapter3), nameof(Unit_Askard_Chapter3.StartStomp))]
        private static class StompPatch
        {
            [HarmonyPrefix]
            private static void Before(Unit_Askard_Chapter3 __instance)
            {
                if (!__instance.isServer) return;

                float[] values = BaseOf(__instance);
                __instance.stompBulletCount = (int)values[0] + (!IsEnhanced() ? 0 : IsHard() ? StompHardBullets : StompNormalBullets);
                __instance.stompWaitTime = IsEnhanced() ? StompWait : values[1];
            }

            [HarmonyPostfix]
            private static void After(Unit_Askard_Chapter3 __instance, Transform target, Vector3 centerPos)
            {
                if (!__instance.isServer || !IsEnhanced()) return;

                SayRandom(__instance, StompLines);
                if (IsHard() && Repeating.Add(__instance)) __instance.StartCoroutine(RepeatStomp(__instance, target, centerPos));
            }
        }

        private static IEnumerator RepeatStomp(Unit_Askard_Chapter3 unit, Transform target, Vector3 centerPos)
        {
            while (unit != null && unit.CurrentAction == Unit_Askard.EAction.stompAttack)
                yield return null;

            if (unit == null || unit.IsDead || unit.CurrentAction != Unit_Askard.EAction.None)
            {
                Repeating.Remove(unit);
                yield break;
            }

            ActionSetter.Invoke(unit, new object[] { Unit_Askard.EAction.Tentacle_Delay });
            yield return new WaitForSeconds(StompRepeatDelay);
            if (unit != null && !unit.IsDead && unit.CurrentAction == Unit_Askard.EAction.Tentacle_Delay)
            {
                ActionSetter.Invoke(unit, new object[] { Unit_Askard.EAction.None });
                unit.StartStomp(target, centerPos);
                while (unit != null && unit.CurrentAction == Unit_Askard.EAction.stompAttack)
                    yield return null;
            }

            Repeating.Remove(unit);
        }

        [HarmonyPatch(typeof(Unit_Askard_Chapter3), nameof(Unit_Askard_Chapter3.StartLaser))]
        private static class LaserPatch
        {
            [HarmonyPrefix]
            private static void Before(Unit_Askard_Chapter3 __instance)
            {
                if (!__instance.isServer) return;

                float[] values = BaseOf(__instance);
                __instance.laserFireWaitTime = IsHard() ? Mathf.Min(values[2], LaserHardWait) : values[2];
            }

            [HarmonyPostfix]
            private static void After(Unit_Askard_Chapter3 __instance)
            {
                if (__instance.isServer && IsEnhanced()) SayRandom(__instance, LaserLines);
            }
        }

        [HarmonyPatch(typeof(Unit_Askard_Chapter3), nameof(Unit_Askard_Chapter3.RpcCreateLaserAreaWarning))]
        private static class SweepRecord
        {
            [HarmonyPrefix]
            private static void Before(Unit_Askard_Chapter3 __instance, float startAngle, int moveDir)
            {
                if (!__instance.isServer || !IsEnhanced()) return;

                Sweeps[__instance] = new SweepInfo { Start = startAngle, Direction = moveDir };
            }
        }

        [HarmonyPatch(typeof(Unit_Askard_Chapter3), nameof(Unit_Askard_Chapter3.RpcSetAnimationTrigger))]
        private static class ReverseLaser
        {
            [HarmonyPrefix]
            private static bool Before(Unit_Askard_Chapter3 __instance, int idx)
            {
                if (sendingEndAnimation || idx != LaserEndAnimation || !__instance.isServer || !IsEnhanced()) return true;

                SweepInfo sweep;
                if (!Sweeps.TryGetValue(__instance, out sweep)) return true;

                Sweeps.Remove(__instance);
                __instance.StartCoroutine(ReverseSweep(__instance, sweep));
                return false;
            }
        }

        private static IEnumerator ReverseSweep(Unit_Askard_Chapter3 unit, SweepInfo sweep)
        {
            float start = sweep.Start + 2f * unit.laserStartAngle * sweep.Direction;
            float end = sweep.Start;
            Vector3 origin = unit.laserReadyFxPoint.position;
            Bullet bullet = Bullet.Pool.Spawn(unit.laserBulletPrefab, origin, false, EDamageFromType.DirectAttack, "BasicAttack", (float)unit.RequestStandardDamage() * unit.laserBulletDamage, 5, 5f, unit, unit.GetHostileFactionLayers(EDamageFromType.DirectAttack), 0f, origin, origin + HorayUtility.GetVector3FromAngle(start), null, null);
            BulletMoveModule_Laser laser = bullet.GetComponent<BulletMoveModule_Laser>();
            CurLaser(unit) = laser;
            float angle = start;
            float speed = 0f;
            while (laser.Bullet.IsSpawned)
            {
                if (unit == null || unit.IsDead || unit.CurrentAction != Unit_Askard.EAction.LaserBullet) yield break;

                angle = Mathf.MoveTowardsAngle(angle, end, speed * Time.deltaTime);
                laser.SetLaserAngle(angle, false);
                speed += unit.laserMoveSpeedAngle * Time.deltaTime;
                if (Mathf.Abs(Mathf.DeltaAngle(angle, end)) <= Mathf.Epsilon)
                {
                    laser.Bullet.DestroySelf(false);
                    break;
                }

                yield return new WaitForEndOfFrame();
            }

            if (unit == null || unit.IsDead || unit.CurrentAction != Unit_Askard.EAction.LaserBullet) yield break;

            sendingEndAnimation = true;
            unit.RpcSetAnimationTrigger(LaserEndAnimation);
            sendingEndAnimation = false;
        }

        [HarmonyPatch(typeof(Unit_Askard_Chapter3), nameof(Unit_Askard_Chapter3.StartBloodyRain))]
        private static class BloodRainBothSides
        {
            [HarmonyPostfix]
            private static void After(Unit_Askard_Chapter3 __instance, float xDir)
            {
                if (!__instance.isServer || !IsEnhanced()) return;

                UnitAI_Askard_Chapter3 ai = AiOf(__instance);
                BossEnvironment_Askard environment = ai == null ? null : ai.Environment as BossEnvironment_Askard;
                if (environment == null) return;

                bool fromLeft = xDir > 0f;
                Transform point = fromLeft ? environment.bloodRainRightsidePoint : environment.bloodRainLeftsidePoint;
                if (point != null) __instance.StartCoroutine(BloodRainRoutine(__instance, point.position, fromLeft ? -1f : 1f));
            }
        }

        private static IEnumerator BloodRainRoutine(Unit_Askard_Chapter3 unit, Vector3 position, float direction)
        {
            unit.RpcCreateBloodyRainWarning(position);
            yield return new WaitForSeconds(BloodRainDelay);
            if (unit == null || unit.IsDead || unit.CurrentAction == Unit_Askard.EAction.Darkness) yield break;

            Bullet.Pool.Spawn(unit.bloodRainBulletPrefab, position, false, EDamageFromType.None, null, unit.RequestStandardDamage() * BloodRainDamageRate, 25, 5f, unit, unit.GetHostileFactionLayers(EDamageFromType.None), 0.3f, position, position + new Vector3(direction, 0f, 0f), null, null);
        }
    }
}
