using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal sealed class BossChallenge_BirdDemon : IBossChallenge
    {
        private static BossChallengeRules Rules => BossChallenge.BirdDemon;

        private const string BossName = "코베스";
        private const string EncounterLine = "클리포트 님에게 더이상 접근하지 말거라.";
        private static readonly string[] TornadoLines = { "너를 시험하마." };
        private const string DeathLine = "클리포트 님... 부디 용서를...";
        private static readonly float[] HpRatios = { 0.9f, 0.5f, 0.25f };
        private static readonly string[] HpLines = { "한없이 나약한 자여.", "분노가 느껴지는 군.", "이것이 너의 대답인가?" };

        private const float BandWarningTime = 0.8f;
        private const float BandGap = 0.5f;
        private const float BandNormalInterval = 0.2f;
        private const float BandHardInterval = 0.125f;
        private const int BandHorizontalCount = 4;
        private const int BandVerticalCount = 7;
        private const float DoubleRepeatDelay = 0.6f;
        private const float NormalWarnInterval = 1.33f;
        private const float NormalAttackDelay = 1.66f;
        private const float HardWarnInterval = 1f;
        private const float HardAttackDelay = 1.5f;
        private const float OuterTornadoNormalSpeed = 1.06f;
        private const float OuterTornadoHardSpeed = 1.12f;
        private const float FallStartDelay = 12f;
        private const float FallInterval = 12f;
        private const int FallCount = 5;
        private const int CircleBagVanillaModulo = 2;
        private const int CircleBagEnhancedModulo = 4;
        private const int TornadoCount = 4;
        private const float TornadoEndMargin = 0.1f;
        private const float VanillaAreaGap = 0.4f;
        private const float VanillaLowHpRatio = 0.5f;
        private const int VanillaAreaCount = 4;
        private const int VanillaLowHpAreaCount = 5;
        private const int TornadoDummyCount = 6;
        private const float TornadoDamageRate = 0.75f;
        private const float DummyHeight = -5000f;
        private const float DummySize = 0.01f;
        private const string DummyName = "SephiriaExpanded_BirdDemon_NullArea";

        private static readonly AccessTools.FieldRef<Unit_BirdDemon, bool> BandVertical =
            AccessTools.FieldRefAccess<Unit_BirdDemon, bool>("meleeAttackDirectionVertical");

        private static readonly AccessTools.FieldRef<UnitAvatar, List<Bullet>> CreatedBullets =
            AccessTools.FieldRefAccess<UnitAvatar, List<Bullet>>("createdBulletList");

        private static readonly FieldInfo PoolCountField = AccessTools.Field(typeof(UnitAI_BirdDemon), "poolCount");

        private static readonly MethodInfo FallingBulletData = AccessTools.Method(typeof(Unit_BirdDemon), "CreateFallingBulletData");

        private static readonly HashSet<Unit_BirdDemon> BandRunning = new HashSet<Unit_BirdDemon>();
        private static readonly HashSet<Unit_BirdDemon> DoubleRunning = new HashSet<Unit_BirdDemon>();
        private static readonly HashSet<UnitAI_BirdDemon> EnterShown = new HashSet<UnitAI_BirdDemon>();
        private static readonly Dictionary<Unit_BirdDemon, UnitAI_BirdDemon> Ais = new Dictionary<Unit_BirdDemon, UnitAI_BirdDemon>();
        private static readonly Dictionary<Unit_BirdDemon, float> NextFall = new Dictionary<Unit_BirdDemon, float>();
        private static GameObject dummyArea;

        public void Tick()
        {
        }

        public void Cleanup()
        {
            BandRunning.Clear();
            DoubleRunning.Clear();
            EnterShown.Clear();
            Ais.Clear();
            NextFall.Clear();
        }

        private static bool IsHard()
        {
            return Rules.Level >= 2;
        }

        private static bool IsEnhanced()
        {
            return Rules.Level >= 1;
        }

        private static float WarnInterval => IsHard() ? HardWarnInterval : NormalWarnInterval;

        private static float AttackDelay => IsHard() ? HardAttackDelay : NormalAttackDelay;

        private static float SequenceTime => (TornadoCount - 1) * WarnInterval + WarnInterval + AttackDelay + (TornadoCount - 1) * WarnInterval;

        private static UnitAI_BirdDemon AiOf(Unit_BirdDemon unit)
        {
            UnitAI_BirdDemon ai;
            if (!Ais.TryGetValue(unit, out ai) || ai == null)
            {
                ai = unit.GetComponent<UnitAI_BirdDemon>();
                Ais[unit] = ai;
            }

            return ai;
        }

        private static bool Usable(Unit_BirdDemon unit)
        {
            return unit != null && !unit.IsDead && !unit.hitStuck;
        }

        [HarmonyPatch(typeof(UnitAI_BossBasic), nameof(UnitAI_BossBasic.OnBattleStartServer))]
        private static class BattleStart
        {
            [HarmonyPostfix]
            private static void After(UnitAI_BossBasic __instance)
            {
                if (!IsEnhanced() || !(__instance is UnitAI_BirdDemon bird)) return;

                BossDialogue.ClearThresholds(bird);
                BossDialogue.ResetDeathLine(bird);
                BossDialogue.RegisterDeathOverride(typeof(UnitAI_BirdDemon), BossName, new[] { bird.bye1Speeech.key }, DeathLine, () => Rules.Level);
                if (!EnterShown.Remove(bird)) BossDialogue.SayEncounter(bird, BossName, EncounterLine, Rules.Level);

                Unit_BirdDemon unit = bird.GetComponent<Unit_BirdDemon>();
                if (unit != null && IsHard()) NextFall[unit] = Time.time + FallStartDelay;
            }
        }

        [HarmonyPatch(typeof(UnitAI_NewBasic), nameof(UnitAI_NewBasic.CreateSpeechBubble),
            new[] { typeof(string), typeof(bool), typeof(float), typeof(bool), typeof(Loc.KeywordValue[]) })]
        private static class EnterSpeech
        {
            [HarmonyPrefix]
            private static bool Before(UnitAI_NewBasic __instance, string key)
            {
                if (key == null || !IsEnhanced() || !__instance.isServer || !(__instance is UnitAI_BirdDemon bird)) return true;

                if (key == bird.enter2Speech.key) return false;
                if (key != bird.enter1Speech.key) return true;

                if (EnterShown.Add(bird)) BossDialogue.Say(bird, BossName, EncounterLine, Rules.Level);

                return false;
            }
        }

        private static int CircleBagModulo()
        {
            return IsEnhanced() ? CircleBagEnhancedModulo : CircleBagVanillaModulo;
        }

        [HarmonyPatch(typeof(UnitAI_BirdDemon), "OnAIUpdate_FoundEnemy")]
        private static class CircleBag
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
                bool replaced = false;
                for (int i = 1; i < codes.Count - 1; i++)
                {
                    if (codes[i].opcode != OpCodes.Ldc_I4_2 || codes[i + 1].opcode != OpCodes.Rem || !codes[i - 1].LoadsField(PoolCountField)) continue;

                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = AccessTools.Method(typeof(BossChallenge_BirdDemon), nameof(CircleBagModulo));
                    replaced = true;
                }

                if (!replaced) Debug.LogWarning("[Sephiria Expanded][BirdDemon] 원형탄 가방 조건을 찾지 못했습니다.");

                return codes;
            }
        }

        [HarmonyPatch(typeof(Unit_BirdDemon), nameof(Unit_BirdDemon.CreateSecondAttack))]
        private static class DoubleAttackRepeat
        {
            [HarmonyPostfix]
            private static void After(Unit_BirdDemon __instance)
            {
                if (!__instance.isServer || !IsEnhanced() || !DoubleRunning.Add(__instance)) return;

                __instance.StartCoroutine(RepeatDouble(__instance));
            }
        }

        private static IEnumerator RepeatDouble(Unit_BirdDemon unit)
        {
            yield return new WaitForSeconds(DoubleRepeatDelay);
            if (Usable(unit))
            {
                unit.CreateFirstAttackWarning();
                yield return new WaitForSeconds(unit.firstAttackWarningTime);
                if (Usable(unit))
                {
                    unit.CreateFirstAttack();
                    if (IsHard())
                    {
                        yield return new WaitForSeconds(DoubleRepeatDelay);
                        if (Usable(unit)) unit.CreateSecondAttack();
                    }
                }
            }

            DoubleRunning.Remove(unit);
        }

        [HarmonyPatch(typeof(Unit_BirdDemon), nameof(Unit_BirdDemon.CreateMeleeAttack), new System.Type[0])]
        private static class BandRepeat
        {
            [HarmonyPostfix]
            private static void After(Unit_BirdDemon __instance)
            {
                if (!__instance.isServer || !IsEnhanced() || !BandRunning.Add(__instance)) return;

                __instance.StartCoroutine(RepeatBand(__instance));
            }
        }

        private static IEnumerator RepeatBand(Unit_BirdDemon unit)
        {
            bool vertical = BandVertical(unit);
            int count = vertical ? BandVerticalCount : BandHorizontalCount;
            float interval = unit.HardPatternActivated ? BandHardInterval : BandNormalInterval;
            yield return new WaitForSeconds((count - 1) * interval + BandGap);
            if (Usable(unit))
            {
                BandVertical(unit) = !vertical;
                unit.CreateMeleeAttackWarning();
                yield return new WaitForSeconds(BandWarningTime);
                if (Usable(unit)) unit.CreateMeleeAttack();
            }

            BandRunning.Remove(unit);
        }

        [HarmonyPatch(typeof(Unit_BirdDemon), "Update")]
        private static class UpdateWatch
        {
            [HarmonyPostfix]
            private static void After(Unit_BirdDemon __instance)
            {
                if (!__instance.isServer || !IsEnhanced()) return;

                UnitAI_BirdDemon ai = AiOf(__instance);
                BossDialogue.CheckHpLines(ai, BossName, HpRatios, HpLines, Rules.Level);
                SpeedUpOuterTornado(__instance);
                DropRandomFalls(__instance, ai);
            }
        }

        private static void SpeedUpOuterTornado(Unit_BirdDemon unit)
        {
            if (unit.patternState != Unit_BirdDemon.EPatternState.OuterTornado || unit.outerTornadoBulletPrefab == null) return;

            List<Bullet> bullets = CreatedBullets(unit);
            if (bullets == null) return;

            float speed = IsHard() ? OuterTornadoHardSpeed : OuterTornadoNormalSpeed;
            string prefabName = unit.outerTornadoBulletPrefab.name;
            foreach (Bullet bullet in bullets)
            {
                if (bullet != null && bullet.speedScale == 1f && bullet.name.StartsWith(prefabName)) bullet.SetSpeedScale(speed);
            }
        }

        private static void DropRandomFalls(Unit_BirdDemon unit, UnitAI_BirdDemon ai)
        {
            float next;
            if (!IsHard() || FallingBulletData == null || !NextFall.TryGetValue(unit, out next) || Time.time < next) return;
            if (unit.IsDead || (ai != null && ai.IsDramaticDying)) return;

            NextFall[unit] = next + FallInterval;
            for (int i = 0; i < FallCount; i++)
            {
                Vector2 position = new Vector2(Random.Range(unit.outLB.x, unit.outRT.x), Random.Range(unit.outLB.y, unit.outRT.y));
                FallingBulletData.Invoke(unit, new object[] { null, position, unit.fallingBulletWaitTime, 1, 0f, false });
            }
        }

        private static List<GameObject> DummyPatterns()
        {
            if (dummyArea == null)
            {
                dummyArea = new GameObject(DummyName);
                Object.DontDestroyOnLoad(dummyArea);
                dummyArea.transform.position = new Vector3(0f, DummyHeight, 0f);
                BoxCollider2D box = dummyArea.AddComponent<BoxCollider2D>();
                box.size = Vector2.one * DummySize;
                box.enabled = false;
            }

            List<GameObject> list = new List<GameObject>();
            for (int i = 0; i < TornadoDummyCount; i++)
                list.Add(dummyArea);

            return list;
        }

        [HarmonyPatch(typeof(Unit_BirdDemon), nameof(Unit_BirdDemon.Tornado))]
        private static class TornadoOrdered
        {
            [HarmonyPrefix]
            private static void Before(Unit_BirdDemon __instance, ref List<GameObject> patterns)
            {
                if (!__instance.isServer || !IsEnhanced() || patterns == null || patterns.Count == 0) return;

                UnitAI_BirdDemon ai = AiOf(__instance);
                if (ai != null) BossDialogue.SayUnused(ai, BossName, TornadoLines, Rules.Level);

                List<GameObject> real = patterns;
                patterns = DummyPatterns();
                __instance.StartCoroutine(OrderedAreas(__instance, real, WarnInterval, AttackDelay));
            }
        }

        [HarmonyPatch(typeof(Unit_BirdDemon), "CreateAreaAttack")]
        private static class DummyAreaTime
        {
            [HarmonyPrefix]
            private static void Before(Unit_BirdDemon __instance, BoxCollider2D[] colliders, ref float time)
            {
                if (dummyArea == null || colliders == null || colliders.Length == 0 || colliders[0] == null || colliders[0].gameObject != dummyArea) return;

                int count = __instance.HpRatio < VanillaLowHpRatio ? VanillaLowHpAreaCount : VanillaAreaCount;
                time = (SequenceTime + TornadoEndMargin - (count - 1) * VanillaAreaGap) / count;
            }
        }

        private static IEnumerator OrderedAreas(Unit_BirdDemon unit, List<GameObject> real, float interval, float attackDelay)
        {
            yield return new WaitForSeconds(unit.tornadoDelay);
            if (unit == null) yield break;

            List<GameObject> pool = new List<GameObject>(real);
            List<BoxCollider2D[]> chosen = new List<BoxCollider2D[]>();
            for (int i = 0; i < TornadoCount; i++)
            {
                if (pool.Count == 0) pool = new List<GameObject>(real);

                int index = Random.Range(0, pool.Count);
                chosen.Add(pool[index].GetComponentsInChildren<BoxCollider2D>());
                pool.RemoveAt(index);
            }

            for (int i = 0; i < TornadoCount; i++)
            {
                if (!Alive(unit)) yield break;

                ShowWarning(unit, chosen[i], interval);
                if (i < TornadoCount - 1) yield return new WaitForSeconds(interval);
            }

            yield return new WaitForSeconds(interval + attackDelay);
            for (int i = 0; i < TornadoCount; i++)
            {
                if (!Alive(unit)) yield break;

                Explode(unit, chosen[i]);
                if (i < TornadoCount - 1) yield return new WaitForSeconds(interval);
            }
        }

        private static bool Alive(Unit_BirdDemon unit)
        {
            return Usable(unit) && unit.patternState == Unit_BirdDemon.EPatternState.Tornado;
        }

        private static void ShowWarning(Unit_BirdDemon unit, BoxCollider2D[] colliders, float time)
        {
            Vector3[] positions = new Vector3[colliders.Length];
            Vector2[] sizes = new Vector2[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                positions[i] = colliders[i].transform.position;
                sizes[i] = colliders[i].size;
            }

            unit.RpcCreateOrderedExplosionWarning(positions, sizes, time);
        }

        private static void Explode(Unit_BirdDemon unit, BoxCollider2D[] colliders)
        {
            foreach (BoxCollider2D collider in colliders)
            {
                GameObject obj = Object.Instantiate(unit.areaAttackPrefab, collider.transform.position, Quaternion.identity);
                WideAreaExplosion explosion = obj.GetComponent<WideAreaExplosion>();
                explosion.Networkowner = unit;
                explosion.attackFaction = unit.GetHostileFactionLayers(EDamageFromType.DirectAttack);
                explosion.damage = (float)unit.RequestStandardDamage() * TornadoDamageRate;
                explosion.Networksize = collider.size;
                NetworkServer.Spawn(obj);
            }
        }
    }
}
