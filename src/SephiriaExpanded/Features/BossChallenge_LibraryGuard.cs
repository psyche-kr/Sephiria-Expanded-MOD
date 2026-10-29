using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SephiriaExpanded
{
    internal sealed class BossChallenge_LibraryGuard : IBossChallenge
    {
        private static BossChallengeRules Rules => BossChallenge.LibraryGuard;

        public BossChallenge_LibraryGuard()
        {
            BossDialogue.RegisterEncounter(typeof(UnitAI_LibraryGuard), ai =>
            {
                if (Rules.Level >= 1) BossDialogue.SayEncounter(ai, BossName, EncounterLine, Rules.Level);
            });
        }

        private const int HardLevel = 2;
        private const float HardSpeedRate = 1.2f;

        private const float HalfHpRatio = 0.5f;
        private const float LowHpRatio = 0.1f;

        private const string BossName = "팬택시스";
        private const string EncounterLine = "적대자 감지. 파괴합니다.";
        private const string HalfHpLine = "작동 이상 감지.";
        private const string LowHpLine = "리미터 해제. 최종 프로토콜 실행.";
        private const string DeathLine = "에러 코드 발동. 시스템 중지.";
        private static readonly string[] SpinDashLines = { "돌진 강화.", "가속.", "추적 시스템 가동.", "회전." };
        private static readonly string[] JumpAttackLines = { "분쇄 시스템 가동.", "공격.", "파괴." };

        private const float FireBallDelay = 1f;
        private const float MapInset = 2f;
        private const int MapAttempts = 30;

        private const int JumpAttackHardCount = 5;

        private const int FragmentCount = 6;
        private const float FragmentStep = 60f;
        private const float FragmentRange = 10f;
        private const float FragmentDamageRate = 0.5f;

        private const float ZigzagVanillaInterval = 1.2f;
        private const float ExplosionVanillaTime = 2f;
        private const float ExplosionHardTime = 1.6f;

        private const float ChakramSpinSpeed = 1080f;
        private const float ChakramGrowSpeed = 2f;
        private const float ChakramMaxRadius = 3f;
        private const float ChakramHitRadius = 0.5f;
        private const float ChakramHitCooldown = 1f;
        private const float ChakramYScale = 0.625f;
        private const int ChakramCount = 3;

        private static readonly AccessTools.FieldRef<UnitAI_LibraryGuard, List<int>> PatternQueue =
            AccessTools.FieldRefAccess<UnitAI_LibraryGuard, List<int>>("patternQueue");

        private static readonly Dictionary<Unit_LibraryGuard, Unit_LibraryGuard.EState> LastState =
            new Dictionary<Unit_LibraryGuard, Unit_LibraryGuard.EState>();

        private static readonly Dictionary<Unit_LibraryGuard, UnitAI_LibraryGuard> Ais =
            new Dictionary<Unit_LibraryGuard, UnitAI_LibraryGuard>();

        private static readonly MethodInfo FireBallIndicator = AccessTools.Method(typeof(Unit_LibraryGuard), "RpcFireBall");

        private static readonly Dictionary<Unit_LibraryGuard, int> BaseZigzagCount = new Dictionary<Unit_LibraryGuard, int>();
        private static readonly Dictionary<Unit_LibraryGuard, List<Unit_LibraryGuard.Attacked>> ChakramHits = new Dictionary<Unit_LibraryGuard, List<Unit_LibraryGuard.Attacked>>();
        private static readonly Dictionary<Unit_LibraryGuard, GameObject[]> ChakramObjects = new Dictionary<Unit_LibraryGuard, GameObject[]>();
        private static readonly Collider2D[] ChakramBuffer = new Collider2D[16];

        public void Tick()
        {
        }

        public void Cleanup()
        {
            LastState.Clear();
            Ais.Clear();
            BaseZigzagCount.Clear();
            ChakramHits.Clear();
            foreach (GameObject[] objects in ChakramObjects.Values)
                DestroyChakrams(objects);
            ChakramObjects.Clear();
        }

        private static bool IsHard()
        {
            return Rules.Level >= HardLevel;
        }

        private static MethodBase CoroutineMoveNext(string coroutineName)
        {
            System.Type type = AccessTools.FirstInner(typeof(Unit_LibraryGuard), inner => inner.Name.Contains(coroutineName));
            return AccessTools.Method(type, "MoveNext");
        }

        private static void ReplaceWithCall(CodeInstruction instruction, string methodName)
        {
            instruction.opcode = OpCodes.Call;
            instruction.operand = AccessTools.Method(typeof(BossChallenge_LibraryGuard), methodName);
        }

        [HarmonyPatch(typeof(UnitAI_LibraryGuard), "OnAIUpdate_FoundEnemy")]
        private static class Pulse
        {
            [HarmonyPrefix]
            private static void Before(UnitAI_LibraryGuard __instance)
            {
                if (Rules.Level < 1 || !__instance.isServer) return;

                List<int> queue = PatternQueue(__instance);
                queue.RemoveAll(pattern => pattern == 4 || pattern == 5);
                if (queue.Count == 0)
                {
                    queue.Add(0);
                    queue.Add(1);
                    queue.Add(2);
                    queue.Add(3);
                }
            }

            [HarmonyPostfix]
            private static void After(UnitAI_LibraryGuard __instance)
            {
                if (Rules.Level < 1 || !__instance.isServer) return;

                Unit_LibraryGuard unit = __instance.GetComponent<Unit_LibraryGuard>();
                if (unit == null) return;

                Unit_LibraryGuard.EState state = unit.currentState;
                Unit_LibraryGuard.EState previous;
                LastState.TryGetValue(unit, out previous);
                LastState[unit] = state;
                if (state == previous) return;

                switch (state)
                {
                    case Unit_LibraryGuard.EState.ZigzagBullet:
                        StartOrderedExplosion(__instance, unit);
                        break;
                    case Unit_LibraryGuard.EState.SpinDash:
                        StartChakram(unit);
                        SayRandom(__instance, SpinDashLines);
                        break;
                    case Unit_LibraryGuard.EState.JumpAttack:
                        SayRandom(__instance, JumpAttackLines);
                        FireExtraFireBalls(__instance, unit);
                        break;
                    case Unit_LibraryGuard.EState.Laser:
                        FireExtraFireBalls(__instance, unit);
                        break;
                }
            }
        }

        private static void SayRandom(UnitAI_LibraryGuard ai, string[] lines)
        {
            BossDialogue.SayUnused(ai, BossName, lines, Rules.Level);
        }

        private static float HpRatio(Unit_LibraryGuard unit)
        {
            return unit.MaxHp > 0f ? unit.hp / unit.MaxHp : 1f;
        }

        internal static bool Annihilating(Unit_LibraryGuard unit)
        {
            return BossChallengeNet.GetDisplayLevel(typeof(Unit_LibraryGuard)) >= 1 && HpRatio(unit) < LowHpRatio;
        }

        private static int FireBallCount(Unit_LibraryGuard unit)
        {
            float ratio = HpRatio(unit);
            int count = 1;
            if (ratio < HalfHpRatio) count++;
            if (IsHard() && ratio < LowHpRatio) count++;
            return count;
        }

        [HarmonyPatch(typeof(UnitAI_BossBasic), nameof(UnitAI_BossBasic.OnBattleStartServer))]
        private static class BattleStart
        {
            [HarmonyPostfix]
            private static void After(UnitAI_BossBasic __instance)
            {
                if (Rules.Level < 1 || !(__instance is UnitAI_LibraryGuard guard)) return;

                BossDialogue.ClearThresholds(guard);
                BossDialogue.SayEncounter(guard, BossName, EncounterLine, Rules.Level);
            }
        }

        [HarmonyPatch(typeof(UnitAI_LibraryGuard), "BeginDramaticDie")]
        private static class DeathSpeech
        {
            [HarmonyPostfix]
            private static void After(UnitAI_LibraryGuard __instance)
            {
                if (Rules.Level < 1 || !__instance.isServer) return;

                if (BossDialogue.TryFireThreshold(__instance, "Death")) BossDialogue.Say(__instance, BossName, DeathLine, Rules.Level);
            }
        }

        private static void TickThresholds(Unit_LibraryGuard unit)
        {
            if (Rules.Level < 1 || unit.IsDead) return;

            float ratio = HpRatio(unit);
            if (ratio >= HalfHpRatio) return;

            UnitAI_LibraryGuard ai;
            if (!Ais.TryGetValue(unit, out ai) || ai == null)
            {
                ai = unit.GetComponent<UnitAI_LibraryGuard>();
                Ais[unit] = ai;
            }

            if (ai == null) return;

            if (BossDialogue.TryFireThreshold(ai, "HalfHp")) BossDialogue.Say(ai, BossName, HalfHpLine, Rules.Level);
            if (ratio < LowHpRatio && BossDialogue.TryFireThreshold(ai, "LowHp")) BossDialogue.Say(ai, BossName, LowHpLine, Rules.Level);
        }

        private static void StartOrderedExplosion(UnitAI_LibraryGuard ai, Unit_LibraryGuard unit)
        {
            BossEnvironment environment = ai.Environment;
            if (environment == null) return;

            unit.PatternOrderedExplosion(environment.BossZoneBottomLeft, environment.BossZoneTopRight);
        }

        private static void FireExtraFireBalls(UnitAI_LibraryGuard ai, Unit_LibraryGuard unit)
        {
            int count = FireBallCount(unit);
            for (int i = 0; i < count; i++)
                unit.StartCoroutine(LaunchFireBall(unit, RandomMapPosition(ai.Environment, unit.transform.position)));
        }

        private static IEnumerator LaunchFireBall(Unit_LibraryGuard unit, Vector2 position)
        {
            FireBallIndicator.Invoke(unit, new object[] { (Vector3)position });
            yield return new WaitForSeconds(FireBallDelay);
            if (unit == null || unit.IsDead) yield break;

            Bullet fireBall = Bullet.Pool.Spawn(unit.fireBallPrefab, unit.transform.position, false, EDamageFromType.DirectAttack, "BasicAttack", unit.RequestStandardDamage(), 25, 1f, unit, unit.GetHostileFactionLayers(EDamageFromType.DirectAttack), 0.5f, unit.transform.position, position, null, null);
            if (IsHard() && fireBall != null) fireBall.onDespawn += landed => unit.StartCoroutine(SpawnFragments(unit, landed.transform.position));
        }

        private static IEnumerator SpawnFragments(Unit_LibraryGuard unit, Vector3 origin)
        {
            yield return null;
            if (unit == null || unit.IsDead) yield break;

            for (int i = 0; i < FragmentCount; i++)
            {
                Vector3 direction = HorayUtility.GetVector3FromAngle(FragmentStep * i);
                Bullet.Pool.Spawn(unit.jumpAttackShockwaveBulletPrefab, origin, false, EDamageFromType.DirectAttack, "BasicAttack", (float)unit.RequestStandardDamage() * FragmentDamageRate, 0, 0f, unit, unit.GetHostileFactionLayers(EDamageFromType.DirectAttack), 0f, origin, origin + direction.normalized * FragmentRange, null, null);
            }
        }

        private static Vector2 RandomMapPosition(BossEnvironment environment, Vector3 fallback)
        {
            if (environment == null) return fallback;

            Vector2 min = (Vector2)environment.BossZoneBottomLeft + Vector2.one * MapInset;
            Vector2 max = (Vector2)environment.BossZoneTopRight - Vector2.one * MapInset;
            if (max.x <= min.x || max.y <= min.y) return environment.BossZoneCenter;

            for (int i = 0; i < MapAttempts; i++)
            {
                Vector2 point = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
                if (!Physics2D.OverlapPoint(point, CombatManager.BlockCharacterLayerMask)) return point;
            }

            return environment.BossZoneCenter;
        }

        [HarmonyPatch(typeof(Unit_LibraryGuard), nameof(Unit_LibraryGuard.PatternZigzagBullet))]
        private static class ZigzagCount
        {
            [HarmonyPrefix]
            private static void Before(Unit_LibraryGuard __instance)
            {
                if (!__instance.isServer) return;

                int baseCount;
                if (!BaseZigzagCount.TryGetValue(__instance, out baseCount))
                {
                    baseCount = __instance.zigzagBulletCount;
                    BaseZigzagCount[__instance] = baseCount;
                }

                __instance.zigzagBulletCount = IsHard() ? Mathf.FloorToInt(baseCount * HardSpeedRate) : baseCount;
            }
        }

        private static float ZigzagInterval()
        {
            return IsHard() ? ZigzagVanillaInterval / HardSpeedRate : ZigzagVanillaInterval;
        }

        [HarmonyPatch]
        private static class ZigzagIntervalPatch
        {
            [HarmonyTargetMethod]
            private static MethodBase Target()
            {
                return CoroutineMoveNext("ZigzagBulletCoroutine");
            }

            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (CodeInstruction instruction in instructions)
                {
                    if (instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float value && value == ZigzagVanillaInterval)
                        ReplaceWithCall(instruction, nameof(ZigzagInterval));

                    yield return instruction;
                }
            }
        }

        private static float ExplosionTime()
        {
            return IsHard() ? ExplosionHardTime : ExplosionVanillaTime;
        }

        [HarmonyPatch]
        private static class ExplosionTimingPatch
        {
            [HarmonyTargetMethod]
            private static MethodBase Target()
            {
                return CoroutineMoveNext("OrderedExplosionCoroutine");
            }

            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> list = new List<CodeInstruction>(instructions);
                for (int i = 0; i < list.Count; i++)
                {
                    CodeInstruction instruction = list[i];
                    if (instruction.opcode != OpCodes.Ldc_R4 || !(instruction.operand is float value)) continue;

                    if (value == ExplosionVanillaTime) ReplaceWithCall(instruction, nameof(ExplosionTime));
                }

                return list;
            }
        }

        [HarmonyPatch(typeof(Unit_LibraryGuard), nameof(Unit_LibraryGuard.PatternJumpAttack))]
        private static class JumpAttackCount
        {
            [HarmonyPostfix]
            private static void After(Unit_LibraryGuard __instance)
            {
                if (IsHard() && __instance.isServer && __instance.currentState == Unit_LibraryGuard.EState.JumpAttack) __instance.jumpAttackCount = JumpAttackHardCount;
            }
        }

        private static void StartChakram(Unit_LibraryGuard unit)
        {
            unit.NetworkcurrentChakramRadius = 0f;
            unit.NetworkcurrentChakramAngle = 0f;
            unit.NetworkchakramState = 1;
            ChakramHits[unit] = new List<Unit_LibraryGuard.Attacked>();
        }

        private static void EndChakram(Unit_LibraryGuard unit)
        {
            ChakramHits.Remove(unit);
            unit.NetworkcurrentChakramRadius = 0f;
            unit.NetworkchakramState = 0;
        }

        private static Vector3 ChakramPosition(Unit_LibraryGuard unit, int index)
        {
            Vector2 direction = HorayUtility.GetVector2FromAngle(unit.currentChakramAngle + index * 360f / ChakramCount);
            direction.y *= ChakramYScale;
            return unit.chakramCenterPoint.position + (Vector3)direction * unit.currentChakramRadius;
        }

        [HarmonyPatch(typeof(Unit_LibraryGuard), "Update")]
        private static class ChakramWhileSpinning
        {
            [HarmonyPostfix]
            private static void After(Unit_LibraryGuard __instance)
            {
                if (__instance.isServer)
                {
                    TickChakramServer(__instance);
                    TickThresholds(__instance);
                }
                if (__instance.isClient) TickChakramClient(__instance);
            }
        }

        private static void TickChakramServer(Unit_LibraryGuard unit)
        {
            List<Unit_LibraryGuard.Attacked> hits;
            if (!ChakramHits.TryGetValue(unit, out hits)) return;

            if (unit.currentState != Unit_LibraryGuard.EState.SpinDash || unit.IsDead || unit.chakramCenterPoint == null)
            {
                EndChakram(unit);
                return;
            }

            float delta = Time.deltaTime;
            float angle = unit.currentChakramAngle + delta * ChakramSpinSpeed;
            if (angle > 360f) angle -= 360f;
            unit.NetworkcurrentChakramAngle = angle;
            unit.NetworkcurrentChakramRadius = Mathf.Min(unit.currentChakramRadius + delta * ChakramGrowSpeed, ChakramMaxRadius);

            for (int i = 0; i < ChakramCount; i++)
                HitAround(unit, hits, ChakramPosition(unit, i));

            for (int i = hits.Count - 1; i >= 0; i--)
            {
                hits[i].timer += delta;
                if (hits[i].timer > ChakramHitCooldown) hits.RemoveAt(i);
            }
        }

        private static void HitAround(Unit_LibraryGuard unit, List<Unit_LibraryGuard.Attacked> hits, Vector3 position)
        {
            int count = HorayPhysics2D.OverlapCircle(position, ChakramHitRadius, ChakramBuffer, CombatManager.Topdown1FLayerMask);
            for (int i = 0; i < count; i++)
            {
                Hitbox hitbox;
                if (!ChakramBuffer[i].TryGetComponent<Hitbox>(out hitbox)) continue;

                CombatBehaviour target = hitbox.GetCombatBehaviour(0);
                if (target == null || hits.Exists(hit => hit.combatBehaviour == target)) continue;

                DamageInstance damage = DamageInstance.GetDamage(unit, "Chakram", unit.transform.position, unit.GetHostileFactionLayers(EDamageFromType.DirectAttack), unit.RequestStandardDamage(), EDamageType.Slice, EDamageFromType.DirectAttack, Vector2.zero, 25, 1f);
                target.ApplyDamage(damage);
                hits.Add(new Unit_LibraryGuard.Attacked { timer = 0f, combatBehaviour = target });
            }
        }

        private static void TickChakramClient(Unit_LibraryGuard unit)
        {
            GameObject[] objects;
            ChakramObjects.TryGetValue(unit, out objects);

            bool active = unit.currentState == Unit_LibraryGuard.EState.SpinDash && unit.chakramState >= 1 && !unit.IsDead;
            if (!active)
            {
                if (objects == null) return;

                DestroyChakrams(objects);
                ChakramObjects.Remove(unit);
                return;
            }

            if (unit.chakramPrefab == null || unit.chakramCenterPoint == null) return;

            if (objects == null)
            {
                objects = new GameObject[ChakramCount];
                for (int i = 0; i < ChakramCount; i++)
                {
                    objects[i] = Object.Instantiate(unit.chakramPrefab, unit.chakramCenterPoint.position, Quaternion.identity);
                    objects[i].name = "SephiriaExpanded_Unit_LibraryGuard_Chakram";
                    objects[i].SetActive(true);
                }

                ChakramObjects[unit] = objects;
            }

            for (int i = 0; i < ChakramCount; i++)
                objects[i].transform.position = ChakramPosition(unit, i);
        }

        private static void DestroyChakrams(GameObject[] objects)
        {
            foreach (GameObject chakram in objects)
                if (chakram != null) Object.Destroy(chakram);
        }
    }
}
