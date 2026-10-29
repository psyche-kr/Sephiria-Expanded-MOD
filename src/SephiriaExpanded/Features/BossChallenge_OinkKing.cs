using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal sealed class BossChallenge_OinkKing : IBossChallenge
    {
        private static BossChallengeRules Rules => BossChallenge.OinkKing;

        public BossChallenge_OinkKing()
        {
            BossDialogue.RegisterEncounter(typeof(UnitAI_OinkKing), ai =>
            {
                if (Rules.Level >= 1) BossDialogue.SayEncounter(ai, BossName, EncounterLine, Rules.Level);
            });
        }

        private const int HardLevel = 2;
        private const float LowHpRatio = 0.5f;
        private const string BossName = "라타카";
        private const string EncounterLine = "먼 길 오셨군요?";
        private const string LowHpLine = "멈춰!";
        private const string SummonLine = "공격해!";
        private const string DeathLine = "아야...";
        private const float PatternLineCooldown = 4f;
        private static readonly string[] BulletHellLines = { "피할 수 있겠어요?", "자, 춤춰 보세요!" };
        private static readonly string[] RockFallLines = { "머리 조심하세요!", "위에서 떨어져요!" };
        private const int RockFallReach = 19;
        private const int RockDensity = 2;
        private const float RockFallStepDelay = 0.08f;

        private const float BulletHellWindup = 0.5f;
        private const int AimedShotsNormal = 12;
        private const int AimedShotsHard = 12;
        private const float AimedIntervalNormal = 0.2f;
        private const float AimedIntervalHard = 0.2f;
        private const float AimedHomingPowerMin = 0.15f;
        private const float AimedHomingPowerMax = 0.3f;

        private const float SummonIntervalNormal = 16f;
        private const float SummonIntervalHard = 10f;
        private const float SummonSpawnRadius = 2f;
        private const int SummonMaxAlive = 8;

        private static readonly MethodInfo BulletHellStart = AccessTools.Method(typeof(Unit_OinkKing), "RpcBulletHell");
        private static readonly MethodInfo BulletHellEnd = AccessTools.Method(typeof(Unit_OinkKing), "RpcBulletHellEnd");
        private static readonly MethodInfo DestroyMagicCircle = AccessTools.Method(typeof(Unit_OinkKing), "DestroyCurrentMagicCircle");
        private static readonly AccessTools.FieldRef<UnitAI_OinkKing, bool> PatternDoing = AccessTools.FieldRefAccess<UnitAI_OinkKing, bool>("isPatternDoing");
        private static readonly AccessTools.FieldRef<Unit_OinkKing, Coroutine> BulletHellRoutine = AccessTools.FieldRefAccess<Unit_OinkKing, Coroutine>("currentBulletHellCoroutine");
        private static readonly AccessTools.FieldRef<Unit_OinkKing, Vector3> RockFallDirection = AccessTools.FieldRefAccess<Unit_OinkKing, Vector3>("rockFallTargetDirection");
        private static readonly AccessTools.FieldRef<Unit_OinkKing, Rect> RockFallZone = AccessTools.FieldRefAccess<Unit_OinkKing, Rect>("rockFallZone");
        private static readonly Dictionary<Unit_OinkKing, Extra> Extras = new Dictionary<Unit_OinkKing, Extra>();
        private static readonly List<Unit_OinkKing> Snapshot = new List<Unit_OinkKing>();

        private sealed class Extra
        {
            internal bool BulletHellActive;
            internal float NextSummon;
            internal float NextPatternLine;
        }

        private static void SayPattern(Unit_OinkKing boss, string[] lines)
        {
            Extra extra = GetExtra(boss);
            if (Time.time < extra.NextPatternLine) return;

            UnitAI_OinkKing ai = boss.GetComponent<UnitAI_OinkKing>();
            if (ai == null) return;

            if (BossDialogue.SayUnused(ai, BossName, lines, Rules.Level)) extra.NextPatternLine = Time.time + PatternLineCooldown;
        }

        public void Tick()
        {
            if (Extras.Count == 0) return;

            Snapshot.Clear();
            Snapshot.AddRange(Extras.Keys);
            foreach (Unit_OinkKing boss in Snapshot)
                if (boss == null) Extras.Remove(boss);
        }

        public void Cleanup()
        {
            Extras.Clear();
        }

        private static bool Active(Unit_OinkKing boss)
        {
            return Rules.Level >= 1 && boss.isServer;
        }

        private static bool IsHard()
        {
            return Rules.Level >= HardLevel;
        }

        private static Extra GetExtra(Unit_OinkKing boss)
        {
            Extra extra;
            if (!Extras.TryGetValue(boss, out extra))
            {
                extra = new Extra();
                Extras[boss] = extra;
            }

            return extra;
        }

        private static IEnumerator RockLine(Unit_OinkKing boss, Vector3 origin, Vector3 direction, Rect zone)
        {
            float step = (Mathf.Abs(direction.x) * 3f + Mathf.Abs(direction.y) * 2.25f) / 2f;
            int steps = (RockFallReach - 1) * RockDensity + 1;
            for (int i = 0; i < steps; i++)
            {
                if (boss == null || boss.IsDead) yield break;

                float count = 1f + (float)i / RockDensity;
                Vector3 offset = step * count * direction;
                DropRock(boss, origin - offset, zone);
                DropRock(boss, origin + offset, zone);
                yield return new WaitForSeconds(RockFallStepDelay / RockDensity);
            }
        }

        private static void DropRock(Unit_OinkKing boss, Vector3 point, Rect zone)
        {
            if (!zone.Contains(point)) return;

            Bullet.Pool.Spawn(boss.rockFallBulletPrefab, boss.transform.position, false, EDamageFromType.DirectAttack, "BasicAttack", boss.RequestStandardDamage() * 0.33f, 5, 1f, boss, boss.GetHostileFactionLayers(EDamageFromType.DirectAttack), 12f, boss.transform.position, point, null, null);
        }

        private static IEnumerator WaitBulletHellThenRockFall(Unit_OinkKing boss, Vector3 origin, Vector3 direction, Rect zone)
        {
            Extra extra = GetExtra(boss);
            while (boss != null && !boss.IsDead && extra.BulletHellActive) yield return null;
            if (boss == null || boss.IsDead) yield break;

            yield return RockLine(boss, origin, direction, zone);
        }

        private static ActiveSkill_Summon SummonSkill(Unit_OinkKing boss)
        {
            SkillController skills = boss.GetComponent<SkillController>();
            if (skills == null || skills.magicCharmsOnEachClient.Length == 0) return null;

            Charm_Magic charm = skills.magicCharmsOnEachClient[0];
            if (charm == null || charm.ContainedMagic == null || charm.ContainedMagic.magicPrefab == null) return null;

            return charm.ContainedMagic.magicPrefab.GetComponent<ActiveSkill_Summon>();
        }

        private static UnitAvatar SummonAt(Unit_OinkKing boss, ActiveSkill_Summon skill, Vector2 position)
        {
            GameObject spawned = Object.Instantiate(skill.unitPrefab, position, Quaternion.identity, boss.transform.parent);
            UnitAvatar unit = spawned.GetComponent<UnitAvatar>();
            if (skill.addCorpseRemover) spawned.AddComponent<UnitAddon_CorpseRemover>();
            NetworkServer.Spawn(spawned);
            if (unit == null) return null;

            unit.ChangeFaction(boss.faction);
            unit.SetLeader(boss);
            unit.followerDamageId = skill.followerDamageId;
            unit.Networkattack = (int)(boss.GetCustomStat(skill.relatedDamage) * skill.damagePercent * 0.01f + skill.defaultDamage);
            return unit;
        }

        private static void SpawnSummons(Unit_OinkKing boss)
        {
            if (boss.FollowersCount >= SummonMaxAlive) return;

            ActiveSkill_Summon skill = SummonSkill(boss);
            if (skill == null || skill.unitPrefab == null) return;

            Vector2 position = (Vector2)boss.transform.position + Random.insideUnitCircle * SummonSpawnRadius;
            SummonAt(boss, skill, position);
        }

        private static IEnumerator RunConcurrently(Unit_OinkKing boss, IEnumerator a, IEnumerator b)
        {
            bool doneA = false;
            bool doneB = false;
            boss.StartCoroutine(RunTracked(a, () => doneA = true));
            boss.StartCoroutine(RunTracked(b, () => doneB = true));
            while (!doneA || !doneB) yield return null;
        }

        private static IEnumerator RunTracked(IEnumerator routine, System.Action onDone)
        {
            yield return routine;
            onDone();
        }

        private static IEnumerator BulletHell(Unit_OinkKing boss, TopdownActorRenderingMetadata target, UnitAvatar homingTarget)
        {
            Extra extra = GetExtra(boss);
            extra.BulletHellActive = true;
            boss.steeringSpeed -= 1f;
            BulletHellStart.Invoke(boss, null);
            yield return new WaitForSeconds(BulletHellWindup);

            IEnumerator main;
            switch (Random.Range(0, 4))
            {
                case 0: main = BulletHellCircle(boss); break;
                case 1: main = BulletHellFan(boss, target); break;
                case 2: main = BulletHellSpin(boss); break;
                default: main = BulletHellCross(boss, target); break;
            }

            yield return RunConcurrently(boss, main, BulletHellAimed(boss, target, homingTarget, IsHard()));

            boss.steeringSpeed += 1f;
            BulletHellEnd.Invoke(boss, null);
            extra.BulletHellActive = false;
        }

        private static float AngleTo(Unit_OinkKing boss, TopdownActorRenderingMetadata target)
        {
            return HorayUtility.GetAngle(target.transform.position + new Vector3(0f, target.CenterYPos), boss.bulletHellPoint.position);
        }

        private static void PlayBulletHellSound(Unit_OinkKing boss)
        {
            if (boss.bulletHellFireSound != null) boss.bulletHellFireSound.Play();
        }

        private static Bullet FireBulletHell(Unit_OinkKing boss, float angle)
        {
            Vector3 from = boss.bulletHellPoint.position;
            return Bullet.Pool.Spawn(boss.bulletHellProjectilePrefab, from, false, EDamageFromType.DirectAttack, "BasicAttack", boss.RequestStandardDamage() * 0.28f, 0, 0f, boss, boss.GetHostileFactionLayers(EDamageFromType.DirectAttack), 0f, from, from + HorayUtility.GetVector3FromAngle(angle), null, null);
        }

        private static IEnumerator BulletHellAimed(Unit_OinkKing boss, TopdownActorRenderingMetadata target, UnitAvatar homingTarget, bool hard)
        {
            int shots = hard ? AimedShotsHard : AimedShotsNormal;
            float interval = hard ? AimedIntervalHard : AimedIntervalNormal;
            for (int i = 0; i < shots && target != null; i++)
            {
                PlayBulletHellSound(boss);
                Bullet bullet = FireBulletHell(boss, AngleTo(boss, target));
                if (hard && bullet != null && homingTarget != null && !homingTarget.IsDead)
                {
                    bullet.TurnOnHoming(false, homingTarget);
                    bullet.homingPower = Random.Range(AimedHomingPowerMin, AimedHomingPowerMax);
                }

                yield return new WaitForSeconds(interval);
            }
        }

        private static IEnumerator BulletHellCircle(Unit_OinkKing boss)
        {
            for (int i = 0; i < 6; i++)
            {
                PlayBulletHellSound(boss);
                for (float angle = Random.Range(0f, 15f); angle < 360f; angle += 15f)
                    FireBulletHell(boss, angle);
                yield return new WaitForSeconds(0.5f);
            }
        }

        private static IEnumerator BulletHellFan(Unit_OinkKing boss, TopdownActorRenderingMetadata target)
        {
            int spread = boss.HardPatternActivated ? 2 : 1;
            for (int i = 0; i < 14 && target != null; i++)
            {
                PlayBulletHellSound(boss);
                float angle = AngleTo(boss, target);
                for (int j = -spread; j <= spread; j++)
                    FireBulletHell(boss, angle + j * 30f);
                yield return new WaitForSeconds(0.2f);
            }
        }

        private static IEnumerator BulletHellSpin(Unit_OinkKing boss)
        {
            float angle = 0f;
            for (int i = 0; i < 80; i++)
            {
                PlayBulletHellSound(boss);
                FireBulletHell(boss, angle);
                yield return new WaitForSeconds(0.04f);
                angle += 20f;
            }
        }

        private static IEnumerator BulletHellCross(Unit_OinkKing boss, TopdownActorRenderingMetadata target)
        {
            for (int i = 0; i < 40 && target != null; i++)
            {
                PlayBulletHellSound(boss);
                float angle = AngleTo(boss, target);
                float swing = 70f * Mathf.Sin(i * Mathf.PI / 20f);
                FireBulletHell(boss, angle + swing);
                FireBulletHell(boss, angle - swing);
                yield return new WaitForSeconds(0.1f);
            }
        }

        [HarmonyPatch(typeof(Unit_OinkKing), nameof(Unit_OinkKing.RockFallAnimation))]
        private static class DenseRockFall
        {
            [HarmonyPrefix]
            private static bool Before(Unit_OinkKing __instance)
            {
                if (!Active(__instance)) return true;

                SayPattern(__instance, RockFallLines);
                DestroyMagicCircle.Invoke(__instance, null);
                Vector3 origin = __instance.transform.position;
                __instance.StartCoroutine(WaitBulletHellThenRockFall(__instance, origin, RockFallDirection(__instance), RockFallZone(__instance)));
                return false;
            }
        }

        [HarmonyPatch(typeof(Unit_OinkKing), nameof(Unit_OinkKing.BulletHell))]
        private static class ReworkBulletHell
        {
            [HarmonyPrefix]
            private static bool Before(Unit_OinkKing __instance, TopdownActorRenderingMetadata target)
            {
                if (!Active(__instance)) return true;

                if (__instance.CurrentAction == Unit_OinkKing.EAnimation.None)
                {
                    UnitAI_OinkKing ai = __instance.GetComponent<UnitAI_OinkKing>();
                    SayPattern(__instance, BulletHellLines);
                    BulletHellRoutine(__instance) = __instance.StartCoroutine(BulletHell(__instance, target, ai != null ? ai.CurrentTarget : null));
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(UnitAI_OinkKing), "PatternCoroutine_SummonMagic")]
        private static class BlockVanillaSummon
        {
            [HarmonyPrefix]
            private static bool Before(UnitAI_OinkKing __instance, ref IEnumerator __result)
            {
                if (Rules.Level < 1 || !__instance.isServer) return true;

                __result = SkipVanillaSummon(__instance);
                return false;
            }
        }

        private static IEnumerator SkipVanillaSummon(UnitAI_OinkKing ai)
        {
            yield return null;
            PatternDoing(ai) = false;
        }

        [HarmonyPatch(typeof(UnitAI_OinkKing), "OnAIUpdate_FoundEnemy")]
        private static class Pulse
        {
            [HarmonyPostfix]
            private static void After(UnitAI_OinkKing __instance)
            {
                if (Rules.Level < 1 || !__instance.isServer || __instance.IsDramaticDying) return;

                Unit_OinkKing boss = __instance.GetComponent<Unit_OinkKing>();
                if (boss == null || boss.IsDead || __instance.CurrentTarget == null) return;

                Extra extra = GetExtra(boss);
                float interval = IsHard() ? SummonIntervalHard : SummonIntervalNormal;

                if (extra.NextSummon <= 0f)
                {
                    extra.NextSummon = Time.time + interval;
                }
                else if (Time.time >= extra.NextSummon)
                {
                    extra.NextSummon = Time.time + interval;
                    if (BossDialogue.TryFireThreshold(__instance, "Summon")) BossDialogue.Say(__instance, BossName, SummonLine, Rules.Level);
                    SpawnSummons(boss);
                }

                if (boss.MaxHp > 0f && boss.hp / boss.MaxHp < LowHpRatio && BossDialogue.TryFireThreshold(__instance, "LowHp"))
                    BossDialogue.Say(__instance, BossName, LowHpLine, Rules.Level);
            }
        }

        [HarmonyPatch(typeof(UnitAI_BossBasic), nameof(UnitAI_BossBasic.OnBattleStartServer))]
        private static class DialogueStart
        {
            [HarmonyPostfix]
            private static void After(UnitAI_BossBasic __instance)
            {
                if (Rules.Level < 1 || !(__instance is UnitAI_OinkKing oink)) return;

                BossDialogue.ClearThresholds(oink);
                BossDialogue.ResetDeathLine(oink);
                BossDialogue.RegisterDeathOverride(typeof(UnitAI_OinkKing), BossName, new[] { oink.dieSay.key }, DeathLine, () => Rules.Level);
                BossDialogue.SayEncounter(oink, BossName, EncounterLine, Rules.Level);
            }
        }
    }
}
