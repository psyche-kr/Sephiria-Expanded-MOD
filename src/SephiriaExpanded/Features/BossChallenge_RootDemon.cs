using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SephiriaExpanded
{
    internal sealed class BossChallenge_RootDemon : IBossChallenge
    {
        private static BossChallengeRules Rules => BossChallenge.RootDemon;

        public BossChallenge_RootDemon()
        {
            BossDialogue.RegisterEncounter(typeof(UnitAI_RootDemon), ai =>
            {
                if (Rules.Level >= ExtraPatternLevel) BossDialogue.SayEncounter(ai, BossName, EncounterLine, Rules.Level);
            });
        }

        private const int ExtraPatternLevel = 1;
        private const int HardLevel = 2;
        private const float VerticalChance = 0.5f;
        private const int VerticalBullets = 1;
        private const float LineLength = 26f;
        private const float LineWarningTime = 0.75f;
        private const float VerticalSpeedRate = 0.6f;
        private const float EdgeInterval = 1.5f;
        private const float EdgeMargin = 1f;
        private const float EdgeSpacing = 2f;
        private const string BossName = "하즈";
        private const string EncounterLine = "과업은 행동으로 증명하는 것이고.";
        private const string DeathLine = "책임은 침묵으로 견뎌내는 것이다.";
        private static readonly string[] SpeedFields = { "speed", "maxSpeed", "startSpeed", "moveSpeed" };
        private static readonly HashSet<UnitAI_RootDemon> DeathLineShown = new HashSet<UnitAI_RootDemon>();

        private static readonly AccessTools.FieldRef<Unit_RootDemonPart, UnitAvatar> AttackTarget = AccessTools.FieldRefAccess<Unit_RootDemonPart, UnitAvatar>("attackTarget");
        private static readonly AccessTools.FieldRef<Unit_RootDemonPart, Vector2> ZoneBottomLeft = AccessTools.FieldRefAccess<Unit_RootDemonPart, Vector2>("bossZone_lb");
        private static readonly AccessTools.FieldRef<Unit_RootDemonPart, Vector2> ZoneTopRight = AccessTools.FieldRefAccess<Unit_RootDemonPart, Vector2>("bossZone_rt");
        private static readonly AccessTools.FieldRef<RootDemonLaserRoot, List<BulletMoveModule_Laser>> LaserList = AccessTools.FieldRefAccess<RootDemonLaserRoot, List<BulletMoveModule_Laser>>("laserList");
        private static readonly AccessTools.FieldRef<RootDemonLaserRoot, bool> LaserEnding = AccessTools.FieldRefAccess<RootDemonLaserRoot, bool>("isEnding");
        private static readonly AccessTools.FieldRef<RootDemonLaserRoot, float> LaserSpeed = AccessTools.FieldRefAccess<RootDemonLaserRoot, float>("curLaserRotateSpeed");
        private static readonly HashSet<Unit_RootDemonPart> Modified = new HashSet<Unit_RootDemonPart>();
        private static readonly Dictionary<RootDemonLaserRoot, LaserState> Reversing = new Dictionary<RootDemonLaserRoot, LaserState>();
        private static readonly Dictionary<Unit_RootDemon, float> NextEdge = new Dictionary<Unit_RootDemon, float>();
        private static readonly List<RootDemonLaserRoot> LaserSnapshot = new List<RootDemonLaserRoot>();

        private sealed class LaserState
        {
            internal bool Reversed;
            internal int Direction = 1;
        }

        public void Tick()
        {
            Modified.RemoveWhere(part => part == null);
            if (Reversing.Count == 0) return;

            LaserSnapshot.Clear();
            LaserSnapshot.AddRange(Reversing.Keys);
            foreach (RootDemonLaserRoot laser in LaserSnapshot)
                if (laser == null) Reversing.Remove(laser);
        }

        public void Cleanup()
        {
            Modified.Clear();
            Reversing.Clear();
            NextEdge.Clear();
            DeathLineShown.Clear();
        }

        private static bool IsHard()
        {
            return Rules.Level >= HardLevel;
        }

        private static void Modify(Unit_RootDemonPart part)
        {
            part.lineBulletCount += Rules.Pick(0, 1);
        }

        private static bool PartActive(Unit_RootDemonPart part)
        {
            return part != null && part.parent != null && part.trueDiePhase == 0 && part.parent.diePhase == 0;
        }

        private static IEnumerator RandomPoints(Unit_RootDemonPart part)
        {
            Vector2 bottomLeft = ZoneBottomLeft(part);
            Vector2 topRight = ZoneTopRight(part);
            int count = part.isDestroyed ? part.lineCount_DestroyedState : part.pointCount;
            for (int i = 0; i < count; i++)
            {
                if (!PartActive(part) || part.tentacleBulletPrefabs.Count == 0) yield break;

                SpawnTentacle(part, new Vector3(Random.Range(bottomLeft.x, topRight.x), Random.Range(bottomLeft.y, topRight.y), 0f));
                yield return new WaitForSeconds(part.pointInterval);
            }
        }

        private static void SpawnTentacle(Unit_RootDemonPart part, Vector3 position)
        {
            Bullet.Pool.Spawn(part.tentacleBulletPrefabs[Random.Range(0, part.tentacleBulletPrefabs.Count)], position, false, EDamageFromType.DirectAttack, "BasicAttack", (float)part.RequestStandardDamage() * part.pointBulletDamageRatio, 25, 5f, part, part.GetHostileFactionLayers(EDamageFromType.DirectAttack), 0f, position, position, null, null);
        }

        private static void EdgeTentacles(Unit_RootDemonPart part)
        {
            Vector2 bottomLeft = ZoneBottomLeft(part) + Vector2.one * EdgeMargin;
            Vector2 topRight = ZoneTopRight(part) - Vector2.one * EdgeMargin;
            for (float x = bottomLeft.x; x <= topRight.x; x += EdgeSpacing)
            {
                SpawnTentacle(part, new Vector3(x, bottomLeft.y, 0f));
                SpawnTentacle(part, new Vector3(x, topRight.y, 0f));
            }

            for (float y = bottomLeft.y + EdgeSpacing; y < topRight.y; y += EdgeSpacing)
            {
                SpawnTentacle(part, new Vector3(bottomLeft.x, y, 0f));
                SpawnTentacle(part, new Vector3(topRight.x, y, 0f));
            }
        }

        private static Unit_RootDemonPart EdgeOwner(Unit_RootDemon boss)
        {
            Unit_RootDemonPart fallback = null;
            foreach (Unit_RootDemonPart part in boss.roots)
            {
                if (!PartActive(part) || part.tentacleBulletPrefabs.Count == 0) continue;
                if (!part.isDestroyed) return part;

                fallback = part;
            }

            return fallback;
        }

        private static Bullet SpawnSlowed(Unit_RootDemonPart part, Vector2 start, Vector2 end, float rate)
        {
            BulletMoveModule module = part.lineAttackBulletPrefab.GetComponent<BulletMoveModule>();
            List<KeyValuePair<FieldInfo, float>> originals = new List<KeyValuePair<FieldInfo, float>>();
            if (module != null)
            {
                foreach (string name in SpeedFields)
                {
                    FieldInfo field = module.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
                    if (field == null || field.FieldType != typeof(float)) continue;

                    float value = (float)field.GetValue(module);
                    originals.Add(new KeyValuePair<FieldInfo, float>(field, value));
                    field.SetValue(module, value * rate);
                }
            }

            try
            {
                return Bullet.Pool.Spawn(part.lineAttackBulletPrefab, start, false, EDamageFromType.DirectAttack, "BasicAttack", (float)part.RequestStandardDamage() * part.lineBulletDamageRatio, 5, 5f, part, part.GetHostileFactionLayers(EDamageFromType.DirectAttack), 0f, start, end, null, null);
            }
            finally
            {
                foreach (KeyValuePair<FieldInfo, float> original in originals)
                    original.Key.SetValue(module, original.Value);
            }
        }

        private static IEnumerator VerticalLine(Unit_RootDemonPart part, float x, float startY, int direction, float delay)
        {
            yield return new WaitForSeconds(delay);
            for (int j = 0; j < VerticalBullets; j++)
            {
                if (!PartActive(part)) yield break;

                Vector2 start = new Vector2(x + Random.Range(-0.25f, 0.25f), startY);
                Vector2 end = start + Vector2.up * direction * 5f;
                SpawnSlowed(part, start, end, VerticalSpeedRate);
                yield return new WaitForSeconds(part.lineBulletInterval);
            }
        }

        private static void VerticalLines(Unit_RootDemonPart part)
        {
            Vector2 bottomLeft = ZoneBottomLeft(part);
            Vector2 topRight = ZoneTopRight(part);
            int count = Mathf.CeilToInt((topRight.x - bottomLeft.x) / part.lineSpacing) + 1;
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Min(bottomLeft.x + i * part.lineSpacing, topRight.x);
                int direction = HorayUtility.GetRandomSign();
                float startY = direction > 0 ? bottomLeft.y - 1f : topRight.y + 1f;
                float delay = part.lineStartDelay + Random.Range(0f, part.lineStarDelayError);
                BossChallengeNet.Line(new Vector2(x, startY), new Vector2(x, startY + direction * LineLength), LineWarningTime);
                part.StartCoroutine(VerticalLine(part, x, startY, direction, delay));
            }

            AttackTarget(part) = null;
        }

        [HarmonyPatch(typeof(Unit_RootDemon), "Update")]
        private static class Tuning
        {
            [HarmonyPostfix]
            private static void After(Unit_RootDemon __instance)
            {
                if (Rules.Level < ExtraPatternLevel || !__instance.isServer || __instance.IsDead) return;

                foreach (Unit_RootDemonPart part in __instance.roots)
                    if (part != null && Modified.Add(part)) Modify(part);

                if (!IsHard() || __instance.diePhase != 0) return;

                UnitAI_RootDemon ai = __instance.GetComponent<UnitAI_RootDemon>();
                if (ai == null || ai.CurrentTarget == null) return;

                float next;
                if (!NextEdge.TryGetValue(__instance, out next))
                {
                    NextEdge[__instance] = Time.time + EdgeInterval;
                    return;
                }

                if (Time.time < next) return;

                NextEdge[__instance] = Time.time + EdgeInterval;
                Unit_RootDemonPart owner = EdgeOwner(__instance);
                if (owner != null) EdgeTentacles(owner);
            }
        }

        [HarmonyPatch(typeof(Unit_RootDemonPart), nameof(Unit_RootDemonPart.LineAttack))]
        private static class VerticalLineAttack
        {
            [HarmonyPrefix]
            private static bool Before(Unit_RootDemonPart __instance)
            {
                if (!IsHard() || !__instance.isServer || Random.value >= VerticalChance) return true;

                VerticalLines(__instance);
                return false;
            }
        }

        [HarmonyPatch(typeof(Unit_RootDemonPart), nameof(Unit_RootDemonPart.Anim_Attack))]
        private static class ExtraPoints
        {
            [HarmonyPostfix]
            private static void After(Unit_RootDemonPart __instance)
            {
                if (Rules.Level < ExtraPatternLevel || !__instance.isServer || __instance.attackType != Unit_RootDemonPart.EAttackType.Point) return;

                __instance.StartCoroutine(RandomPoints(__instance));
            }
        }

        [HarmonyPatch(typeof(RootDemonLaserRoot), nameof(RootDemonLaserRoot.SetOwner))]
        private static class LaserSetup
        {
            [HarmonyPostfix]
            private static void After(RootDemonLaserRoot __instance)
            {
                if (Rules.Level < ExtraPatternLevel || !__instance.isServer) return;

                __instance.NetworklaserCount = 2;
                if (IsHard()) Reversing[__instance] = new LaserState();
            }
        }

        [HarmonyPatch(typeof(RootDemonLaserRoot), "Update")]
        private static class LaserReverse
        {
            [HarmonyPrefix]
            private static bool Before(RootDemonLaserRoot __instance)
            {
                LaserState state;
                if (Rules.Level < ExtraPatternLevel || !Reversing.TryGetValue(__instance, out state)) return true;

                List<BulletMoveModule_Laser> list = LaserList(__instance);
                if (!__instance.isServer || list.Count <= 0) return false;

                if (!LaserEnding(__instance) && __instance.laserEndTimer.Update(Time.deltaTime))
                {
                    if (!state.Reversed)
                    {
                        state.Reversed = true;
                        state.Direction = -1;
                        __instance.laserEndTimer.Ratio = 0f;
                    }
                    else
                    {
                        for (int i = 0; i < list.Count; i++)
                            list[i].Bullet.DestroySelf(forceDestroy: false);

                        LaserEnding(__instance) = true;
                    }
                }

                if (__instance.laserRotateDelayTimer.Update(Time.deltaTime))
                {
                    float target = LaserEnding(__instance) ? 0f : __instance.laserRotateSpeed * state.Direction;
                    float speed = Mathf.MoveTowards(LaserSpeed(__instance), target, __instance.laserRotateSpeed * 2f * Time.deltaTime);
                    LaserSpeed(__instance) = speed;
                    float angle = (__instance.laserAngle + speed * Time.deltaTime) % 360f;
                    __instance.laserAngle = angle < 0f ? angle + 360f : angle;
                    for (int j = 0; j < list.Count; j++)
                        list[j].SetLaserAngle(__instance.laserAngle + j * 360f / __instance.laserCount, reliable: false);
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(RootDemonLaserRoot), nameof(RootDemonLaserRoot.ForcedEndLaser))]
        private static class LaserForcedEnd
        {
            [HarmonyPostfix]
            private static void After(RootDemonLaserRoot __instance)
            {
                LaserState state;
                if (Reversing.TryGetValue(__instance, out state)) state.Reversed = true;
            }
        }

        [HarmonyPatch(typeof(RootDemonLaserRoot), "OnDestroy")]
        private static class LaserGone
        {
            [HarmonyPostfix]
            private static void After(RootDemonLaserRoot __instance)
            {
                Reversing.Remove(__instance);
            }
        }

        [HarmonyPatch(typeof(UnitAI_BossBasic), nameof(UnitAI_BossBasic.OnBattleStartServer))]
        private static class DialogueStart
        {
            [HarmonyPostfix]
            private static void After(UnitAI_BossBasic __instance)
            {
                if (!(__instance is UnitAI_RootDemon demon) || Rules.Level < ExtraPatternLevel) return;

                BossDialogue.ClearThresholds(demon);
                DeathLineShown.Remove(demon);
                BossDialogue.SayEncounter(demon, BossName, EncounterLine, Rules.Level);
            }
        }

        [HarmonyPatch(typeof(UnitAI_RootDemon), "BeginDramaticDie")]
        private static class DeathLineTrigger
        {
            [HarmonyPostfix]
            private static void After(UnitAI_RootDemon __instance)
            {
                if (Rules.Level < ExtraPatternLevel || !__instance.isServer) return;
                if (!DeathLineShown.Add(__instance)) return;

                BossDialogue.Say(__instance, BossName, DeathLine, Rules.Level);
            }
        }
    }
}
