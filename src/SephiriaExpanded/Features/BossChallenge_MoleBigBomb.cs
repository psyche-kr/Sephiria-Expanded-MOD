using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SephiriaExpanded
{
    internal sealed class BossChallenge_MoleBigBomb : IBossChallenge
    {
        private static BossChallengeRules Rules => BossChallenge.MoleBigBomb;

        public BossChallenge_MoleBigBomb()
        {
            BossDialogue.RegisterEncounter(typeof(UnitAI_MoleBigBomb), ai =>
            {
                if (Rules.Level >= ExtraPatternLevel) BossDialogue.SayEncounter(ai, BossName, EncounterLine, Rules.Level);
            });
        }

        private const int ExtraPatternLevel = 1;
        private const int HardLevel = 2;
        private const float AftershockWarningComplete = 1.3f;
        private const float AftershockWarningTime = 0.2f;
        private const float MissileWarningReveal = 0.1f + RingCount * RingDelay;
        private const float DynamiteDamageRate = 0.5f;
        private const float OverheatRatio = 0.2f;
        private const float OverheatSpeed = 0.18f;
        private const float TackleSideOffset = 4f;
        private const float DynamiteColumnStep = 2.3f;
        private const float DynamiteColumnDelay = 0.06f;
        private const float DynamiteDropHeight = 16f;
        private const float RingStep = 1.75f;
        private const float RingDelay = 0.08f;
        private const int RingCount = 7;
        private const float OverheatBombInterval = 5f;
        private const float LowHpRatio = 0.5f;

        private const string BossName = "오드너";
        private const string EncounterLine = "놀아볼까, 친구?";
        private const string LowHpLine = "고작 이 정도야?";
        private const string OverheatLinePrefix = "나 조금 ";
        private const string OverheatLineWord = "과열";
        private const string OverheatLineSuffix = " 될지도!";
        private const string DeathLine = "너, 강하네!";
        private const float PatternLineCooldown = 4f;
        private static readonly string[] PunchLines = { "펑!", "한 대 더~!" };
        private static readonly string[] MissileLines = { "하늘에서 선물이야!", "위를 봐~!" };
        private static readonly string[] SpreadLines = { "폭죽 놀이다!", "골라 먹어~!" };
        private static readonly string[] RemoteLines = { "줄줄이 팡팡!", "어디로 숨을래?" };
        private static readonly Color OverheatTextColor = new Color32(0x99, 0x00, 0x85, 0xFF);

        private static readonly Dictionary<MoleBigBombDynamiteZone, Coroutine> LineRoutines = new Dictionary<MoleBigBombDynamiteZone, Coroutine>();
        private static readonly MethodInfo MissileWarning = AccessTools.Method(typeof(Unit_MoleBigBomb), "RpcMissileDown", new[] { typeof(Vector2), typeof(float), typeof(bool) });
        private static readonly MethodInfo MissileAttackFired = AccessTools.Method(typeof(Unit_MoleBigBomb), "RpcMissileAttackFired");
        private static readonly Dictionary<Unit_MoleBigBomb, Extra> Extras = new Dictionary<Unit_MoleBigBomb, Extra>();
        private static readonly List<Unit_MoleBigBomb> Snapshot = new List<Unit_MoleBigBomb>();
        private static bool replayingImpactSound;

        private sealed class Extra
        {
            internal float NextMissileDrop;
            internal float NextTackleSide;
            internal float NextOverheatBombThrow;
            internal float NextPatternLine;
            internal bool RemoteRecasting;
            internal bool SpreadRecasting;
            internal bool Recasting;
            internal UnitAI_MoleBigBomb Ai;
        }

        public void Tick()
        {
            Prune();
        }

        public void Cleanup()
        {
            Extras.Clear();
            LineRoutines.Clear();
        }

        private static bool IsHard()
        {
            return Rules.Level >= HardLevel;
        }

        private static void Prune()
        {
            if (Extras.Count == 0) return;

            Snapshot.Clear();
            Snapshot.AddRange(Extras.Keys);
            foreach (Unit_MoleBigBomb boss in Snapshot)
                if (boss == null) Extras.Remove(boss);
        }

        private static Extra GetExtra(Unit_MoleBigBomb boss)
        {
            Extra extra;
            if (!Extras.TryGetValue(boss, out extra))
            {
                extra = new Extra();
                Extras[boss] = extra;
            }

            return extra;
        }

        private static UnitAI_MoleBigBomb GetAi(Unit_MoleBigBomb boss, Extra extra)
        {
            if (extra.Ai == null) extra.Ai = boss.GetComponent<UnitAI_MoleBigBomb>();
            return extra.Ai;
        }

        private static void SayPattern(Unit_MoleBigBomb boss, string[] lines)
        {
            if (Rules.Level < ExtraPatternLevel || boss == null || !boss.isServer) return;

            Extra extra = GetExtra(boss);
            if (Time.time < extra.NextPatternLine) return;

            UnitAI_MoleBigBomb ai = GetAi(boss, extra);
            if (ai == null) return;

            if (BossDialogue.SayUnused(ai, BossName, lines, Rules.Level)) extra.NextPatternLine = Time.time + PatternLineCooldown;
        }

        internal static bool Overheated(Unit_MoleBigBomb boss)
        {
            return Rules.Level >= ExtraPatternLevel && boss.MaxHp > 0f && boss.hp / boss.MaxHp <= OverheatRatio;
        }

        private static void AssignZone(Unit_MoleBigBomb boss, UnitAI_MoleBigBomb ai)
        {
            if (boss.remoteDynamiteZone != null || ai == null || ai.Environment == null) return;

            MoleBigBombDynamiteZone zone = ai.Environment.GetComponent<MoleBigBombDynamiteZone>();
            if (zone != null) boss.SetDynamiteZone(zone);
        }

        private static void ThrowDynamite(Unit_MoleBigBomb boss, Vector2 to)
        {
            boss.tackleDynamiteFireData.CreateAttack(EDamageFromType.DirectAttack, (float)boss.RequestStandardDamage() * DynamiteDamageRate, "BasicAttack", false, boss, boss.transform.position, to, boss.TopdownActor.size.y * 0.5f, null);
        }

        private static void TackleSides(Unit_MoleBigBomb boss)
        {
            float[] radii = { 3.5f, 6.5f };
            for (int side = -1; side <= 1; side += 2)
            {
                foreach (float radius in radii)
                {
                    Vector2 offset = Random.insideUnitCircle * radius;
                    offset.x = offset.x / 2f + side * TackleSideOffset;
                    ThrowDynamite(boss, (Vector2)boss.transform.position + offset);
                }
            }
        }

        private static bool DynamiteBounds(MoleBigBombDynamiteZone zone, out Vector2 bottomLeft, out Vector2 topRight)
        {
            bottomLeft = Vector2.zero;
            topRight = Vector2.zero;
            if (zone.linebombPointBottomLeft != null && zone.linebombPointTopRight != null)
            {
                bottomLeft = zone.linebombPointBottomLeft.position;
                topRight = zone.linebombPointTopRight.position;
                return true;
            }

            bool found = false;
            foreach (Transform point in zone.dynamitePoints)
            {
                if (point == null) continue;

                Vector2 position = point.position;
                bottomLeft = found ? Vector2.Min(bottomLeft, position) : position;
                topRight = found ? Vector2.Max(topRight, position) : position;
                found = true;
            }

            return found;
        }

        private static IEnumerator LineDynamite(MoleBigBombDynamiteZone zone, UnitAvatar owner, float damage, Vector2 bottomLeft, Vector2 topRight)
        {
            int columns = Mathf.Max(2, Mathf.CeilToInt((topRight.x - bottomLeft.x) / DynamiteColumnStep) + 1);
            float stepX = (topRight.x - bottomLeft.x) / (columns - 1);
            float height = topRight.y - bottomLeft.y;
            float upperY = bottomLeft.y + height * 0.8f;
            float lowerY = bottomLeft.y + height * 0.2f;
            bool upperFromRight = Random.value < 0.5f;
            for (int i = 0; i < columns; i++)
            {
                if (owner == null || owner.IsDead) yield break;

                float upperX = bottomLeft.x + (upperFromRight ? columns - 1 - i : i) * stepX;
                float lowerX = bottomLeft.x + (upperFromRight ? i : columns - 1 - i) * stepX;
                SpawnDynamite(zone, owner, damage, new Vector2(upperX, upperY));
                SpawnDynamite(zone, owner, damage, new Vector2(lowerX, lowerY));
                yield return new WaitForSeconds(DynamiteColumnDelay);
            }
        }

        private static void SpawnDynamite(MoleBigBombDynamiteZone zone, UnitAvatar owner, float damage, Vector2 position)
        {
            Bullet.Pool.Spawn(zone.dynamitePrefab, position, false, EDamageFromType.DirectAttack, "BasicAttack", damage, 25, 1f, owner, owner.GetHostileFactionLayers(EDamageFromType.DirectAttack), DynamiteDropHeight, position, position, null, null);
        }
        private static void ShowMissileWarning(Unit_MoleBigBomb boss, Vector2 center, float offset)
        {
            if (MissileWarning != null) MissileWarning.Invoke(boss, new object[] { center, offset, false });
        }

        private static IEnumerator AftershockRing(Unit_MoleBigBomb boss, Vector2 center, float offset)
        {
            List<CombatBehaviour> shared = new List<CombatBehaviour>();
            for (int time = 0; time < RingCount; time++)
            {
                if (boss == null || boss.IsDead) yield break;

                for (int i = 0; i < RingCount && (time != 0 || i < 1); i++)
                {
                    Vector2 point = center + time * RingStep * HorayUtility.GetVector2FromAngle((float)i / RingCount * 360f + offset);
                    if (Physics2D.OverlapPoint(point, CombatManager.TileLayerMask)) continue;

                    Bullet.Pool.Spawn(boss.missileExplosionBulletPrefab, point, false, EDamageFromType.DirectAttack, "BasicAttack", boss.RequestStandardDamage(), 25, 1f, boss, boss.GetHostileFactionLayers(EDamageFromType.DirectAttack), 0f, center, point, shared, null);
                }

                yield return new WaitForSeconds(RingDelay);
            }
        }

        private static IEnumerator MissileAftershock(Unit_MoleBigBomb boss)
        {
            Vector2 center = boss.transform.position;
            float offset = boss.missileAngleOffset + 180f / RingCount;
            yield return new WaitForSeconds(Mathf.Max(0f, AftershockWarningComplete - MissileWarningReveal));
            if (boss == null || boss.IsDead) yield break;

            ShowMissileWarning(boss, center, offset);
            yield return new WaitForSeconds(0.15f);
            ShowMissileWarning(boss, center, offset);
            yield return new WaitForSeconds(MissileWarningReveal + AftershockWarningTime - 0.15f);
            if (boss == null || boss.IsDead) yield break;

            if (MissileAttackFired != null)
            {
                replayingImpactSound = true;
                MissileAttackFired.Invoke(boss, null);
                replayingImpactSound = false;
            }

            boss.StartCoroutine(AftershockRing(boss, center, offset));
        }

        [HarmonyPatch(typeof(UnitAI_BossBasic), nameof(UnitAI_BossBasic.OnBattleStartServer))]
        private static class BattleStart
        {
            [HarmonyPostfix]
            private static void After(UnitAI_BossBasic __instance)
            {
                if (Rules.Level < ExtraPatternLevel || !(__instance is UnitAI_MoleBigBomb mole)) return;

                Unit_MoleBigBomb boss = __instance.GetComponent<Unit_MoleBigBomb>();
                if (boss == null) return;

                AssignZone(boss, mole);

                BossDialogue.ClearThresholds(mole);
                BossDialogue.ResetDeathLine(mole);
                BossDialogue.RegisterDeathOverride(typeof(UnitAI_MoleBigBomb), BossName,
                    new[] { mole.bye1Speech.key, mole.bye2Speech.key }, DeathLine, () => Rules.Level);
                BossDialogue.SayEncounter(mole, BossName, EncounterLine, Rules.Level);
            }
        }

        [HarmonyPatch(typeof(UnitAI_MoleBigBomb), "OnAIUpdate_FoundEnemy")]
        private static class Pulse
        {
            [HarmonyPostfix]
            private static void After(UnitAI_MoleBigBomb __instance)
            {
                if (Rules.Level < ExtraPatternLevel) return;

                Unit_MoleBigBomb boss = __instance.GetComponent<Unit_MoleBigBomb>();
                if (boss == null) return;

                if (boss.remoteDynamiteZone == null) AssignZone(boss, __instance);

                if (boss.patternState == Unit_MoleBigBomb.EPatternState.Idle && Overheated(boss))
                    __instance.majorPatternTimer.AddTimer(Time.deltaTime * OverheatSpeed);
            }
        }

        [HarmonyPatch(typeof(Unit_MoleBigBomb), "Update")]
        private static class HoverDynamite
        {
            [HarmonyPostfix]
            private static void After(Unit_MoleBigBomb __instance)
            {
                if (Rules.Level < ExtraPatternLevel || !__instance.isServer || __instance.IsDead) return;

                if (IsHard() && __instance.isTackleRunning)
                {
                    Extra tackle = GetExtra(__instance);
                    if (Time.time >= tackle.NextTackleSide)
                    {
                        tackle.NextTackleSide = Time.time + __instance.tackleDynamiteFireTimer.time;
                        TackleSides(__instance);
                    }
                }

                if (__instance.MaxHp > 0f && __instance.hp / __instance.MaxHp < LowHpRatio &&
                    BossDialogue.TryFireThreshold(GetAi(__instance, GetExtra(__instance)), "LowHp"))
                {
                    UnitAI_MoleBigBomb lowHpAi = GetAi(__instance, GetExtra(__instance));
                    if (lowHpAi != null) BossDialogue.Say(lowHpAi, BossName, LowHpLine, Rules.Level);
                }

                if (Overheated(__instance))
                {
                    Extra overheat = GetExtra(__instance);
                    if (BossDialogue.TryFireThreshold(GetAi(__instance, overheat), "Overheat"))
                    {
                        UnitAI_MoleBigBomb overheatLineAi = GetAi(__instance, overheat);
                        if (overheatLineAi != null)
                        {
                            string levelColor = Rules.Level >= 2 ? "red" : "yellow";
                            string wordHex = ColorUtility.ToHtmlStringRGB(OverheatTextColor);
                            string line = "<color=" + levelColor + ">" + OverheatLinePrefix + "</color>" +
                                "<color=#" + wordHex + ">" + OverheatLineWord + "</color>" +
                                "<color=" + levelColor + ">" + OverheatLineSuffix + "</color>";
                            BossDialogue.SayRaw(overheatLineAi, BossName, line, line, levelColor);
                        }
                    }

                    if (Time.time >= overheat.NextOverheatBombThrow)
                    {
                        overheat.NextOverheatBombThrow = Time.time + OverheatBombInterval;
                        UnitAI_MoleBigBomb overheatAi = GetAi(__instance, overheat);
                        if (overheatAi != null && overheatAi.CurrentTarget != null)
                        {
                            List<CombatBehaviour> shared = new List<CombatBehaviour>();
                            Vector2 targetPos = overheatAi.CurrentTarget.transform.position;
                            Bullet.Pool.Spawn(__instance.bombSpreadBulletPrefab, __instance.transform.position, false, EDamageFromType.DirectAttack, "BasicAttack", __instance.RequestStandardDamage(), 25, 1f, __instance, __instance.GetHostileFactionLayers(EDamageFromType.DirectAttack), 0f, __instance.transform.position, targetPos, shared, null);
                        }
                    }
                }

                if (__instance.patternState != Unit_MoleBigBomb.EPatternState.Missile || __instance.missilePhase != 1) return;

                Extra extra = GetExtra(__instance);
                UnitAI_MoleBigBomb ai = GetAi(__instance, extra);
                if (ai == null || ai.CurrentTarget == null || Time.time < extra.NextMissileDrop) return;

                extra.NextMissileDrop = Time.time + Rules.Pick(0.5f, 0.25f);
                ThrowDynamite(__instance, (Vector2)ai.CurrentTarget.transform.position + Random.insideUnitCircle * 3f);
            }
        }

        [HarmonyPatch(typeof(Unit_MoleBigBomb), "RpcMissileAttackFired")]
        private static class MissileEnd
        {
            [HarmonyPostfix]
            private static void After(Unit_MoleBigBomb __instance)
            {
                if (Rules.Level < ExtraPatternLevel || !__instance.isServer || replayingImpactSound) return;

                SayPattern(__instance, MissileLines);
                __instance.StartCoroutine(MissileAftershock(__instance));
            }
        }

        [HarmonyPatch(typeof(Unit_MoleBigBomb), nameof(Unit_MoleBigBomb.FireBombSpreadAnimation))]
        private static class SpreadExtra
        {
            [HarmonyPostfix]
            private static void After(Unit_MoleBigBomb __instance)
            {
                MoleBigBombDynamiteZone zone = __instance.remoteDynamiteZone;
                if (Rules.Level < ExtraPatternLevel || !__instance.isServer || zone == null || zone.linebombPointBottomLeft == null || zone.linebombPointTopRight == null) return;

                SayPattern(__instance, SpreadLines);
                List<CombatBehaviour> shared = new List<CombatBehaviour>();
                int total = Rules.Pick(1, 2);
                for (int i = 0; i < total; i++)
                {
                    Vector2 point = new Vector2(Random.Range(zone.linebombPointBottomLeft.position.x, zone.linebombPointTopRight.position.x), Random.Range(zone.linebombPointBottomLeft.position.y, zone.linebombPointTopRight.position.y));
                    Bullet.Pool.Spawn(__instance.bombSpreadBulletPrefab, __instance.transform.position, false, EDamageFromType.DirectAttack, "BasicAttack", __instance.RequestStandardDamage(), 25, 1f, __instance, __instance.GetHostileFactionLayers(EDamageFromType.DirectAttack), 0f, __instance.transform.position, point, shared, null);
                }
            }
        }

        [HarmonyPatch(typeof(Unit_MoleBigBomb), nameof(Unit_MoleBigBomb.EndBombSpreadAnimation))]
        private static class SpreadRecast
        {
            [HarmonyPrefix]
            private static void Before(Unit_MoleBigBomb __instance, out bool __state)
            {
                __state = __instance.isServer && __instance.patternState == Unit_MoleBigBomb.EPatternState.BombSpread;
            }

            [HarmonyPostfix]
            private static void After(Unit_MoleBigBomb __instance, bool __state)
            {
                if (!__state || Rules.Level < ExtraPatternLevel) return;

                Extra extra = GetExtra(__instance);
                if (extra.SpreadRecasting)
                {
                    extra.SpreadRecasting = false;
                    return;
                }

                if (!IsHard() || !Overheated(__instance) || __instance.remoteDynamiteZone == null) return;

                extra.SpreadRecasting = true;
                __instance.BombSpread();
            }
        }

        [HarmonyPatch(typeof(Unit_MoleBigBomb), nameof(Unit_MoleBigBomb.EndRemoteDynamiteAnimation))]
        private static class RemoteRecast
        {
            [HarmonyPrefix]
            private static void Before(Unit_MoleBigBomb __instance, out bool __state)
            {
                __state = __instance.isServer && __instance.patternState == Unit_MoleBigBomb.EPatternState.RemoteDynamite;
            }

            [HarmonyPostfix]
            private static void After(Unit_MoleBigBomb __instance, bool __state)
            {
                if (!__state || Rules.Level < ExtraPatternLevel) return;

                Extra extra = GetExtra(__instance);
                if (extra.RemoteRecasting)
                {
                    extra.RemoteRecasting = false;
                    return;
                }

                if (!IsHard() || !Overheated(__instance)) return;

                extra.RemoteRecasting = true;
                __instance.RemoteDynamite();
            }
        }

        [HarmonyPatch(typeof(Unit_MoleBigBomb), nameof(Unit_MoleBigBomb.BombPunchEndAnimation))]
        private static class PunchRecast
        {
            [HarmonyPrefix]
            private static void Before(Unit_MoleBigBomb __instance, out bool __state)
            {
                __state = __instance.isServer && __instance.patternState == Unit_MoleBigBomb.EPatternState.BombPunch;
            }

            [HarmonyPostfix]
            private static void After(Unit_MoleBigBomb __instance, bool __state)
            {
                if (!__state || Rules.Level < ExtraPatternLevel) return;

                Extra extra = GetExtra(__instance);
                if (extra.Recasting)
                {
                    extra.Recasting = false;
                    return;
                }

                UnitAI_MoleBigBomb ai = GetAi(__instance, extra);
                if (ai == null || ai.CurrentTarget == null || ai.CurrentTarget.IsDead) return;

                extra.Recasting = true;
                SayPattern(__instance, PunchLines);
                __instance.Aim(ai.CurrentTarget.transform.position);
                __instance.BombPunch(ai.CurrentTarget.transform.position);
            }
        }

        [HarmonyPatch(typeof(MoleBigBombDynamiteZone), nameof(MoleBigBombDynamiteZone.CreateDynamite))]
        private static class LineDynamiteCast
        {
            [HarmonyPrefix]
            private static bool Before(MoleBigBombDynamiteZone __instance, UnitAvatar owner, float damage)
            {
                Vector2 bottomLeft;
                Vector2 topRight;
                if (Rules.Level < ExtraPatternLevel || !(owner is Unit_MoleBigBomb) || __instance.dynamitePoints == null || !DynamiteBounds(__instance, out bottomLeft, out topRight)) return true;

                Coroutine previous;
                if (LineRoutines.TryGetValue(__instance, out previous) && previous != null) __instance.StopCoroutine(previous);

                LineRoutines[__instance] = __instance.StartCoroutine(LineDynamite(__instance, owner, damage, bottomLeft, topRight));
                SayPattern((Unit_MoleBigBomb)owner, RemoteLines);
                return true;
            }
        }

        [HarmonyPatch(typeof(MoleBigBombDynamiteZone), nameof(MoleBigBombDynamiteZone.CancelDynamite))]
        private static class LineDynamiteCancel
        {
            [HarmonyPostfix]
            private static void After(MoleBigBombDynamiteZone __instance)
            {
                Coroutine routine;
                if (!LineRoutines.TryGetValue(__instance, out routine)) return;

                if (routine != null) __instance.StopCoroutine(routine);
                LineRoutines.Remove(__instance);
            }
        }    }
}
