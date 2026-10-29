using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SephiriaExpanded
{
    internal sealed class BossChallenge_MoleChieftain : IBossChallenge
    {
        private static BossChallengeRules Rules => BossChallenge.MoleChieftain;

        private const float FollowUpDelay = 0.15f;
        private const float PendingLifetime = 6f;
        private const int ExtraPatternLevel = 1;
        private const float DynamiteDamageRate = 0.5f;
        private const float RockDamageRate = 0.6f;
        private const int SideDynamiteCount = 5;
        private const float SideDynamiteMin = 2f;
        private const float SideDynamiteMax = 5f;
        private const float JumpBurstMin = 3f;
        private const float JumpBurstMax = 8f;
        private const int HoverRockCount = 2;
        private const float HoverRockInterval = 1f;
        private const float PhaseRockInterval = 2f;
        private const float PhaseRockRadius = 2f;
        private const int SweepLevel = 2;
        private const float RainInterval = 20f;
        private const float RainStep = 2.6f;
        private const float RainDelay = 0.1f;
        private const float SweepInterval = 14f;
        private const float SweepStep = 1f;
        private const float SweepDelay = 0.07f;
        private const float SweepMinGap = 3f;
        private const float SweepRowSpacing = 1.2f;
        private const float RestGrace = 4f;
        private const int StompRays = 12;
        private const int StompSteps = 14;
        private const float StompInterval = 0.1f;
        private const float StompStepDistance = 1.5f;
        private const float StompWarningTime = 0.5f;
        private const float PatternGap = 5f;
        private const string BossName = "모그디";
        private const string EncounterLine = "어디 한번 붙어보자!";
        private const string LandingLine = "2라운드다!";
        private const string DeathLine = "크윽... 내가 밀리다니.";
        private static readonly string[] DynamiteLines = { "터져라!", "날려주마!", "한 방 더!", "피할 틈은 없다!", "쾅이다!" };
        private static readonly string[] Phase1LowHpLines = { "몸이 풀리는군!", "속도를 올린다!" };
        private static readonly string[] Phase2LowHpLines = { "아직 멀었다!", "더 빠르게!", "끝까지 간다!" };
        private static readonly AccessTools.FieldRef<UnitAI_MoleChieftain, bool> LifeDecreased = AccessTools.FieldRefAccess<UnitAI_MoleChieftain, bool>("isLifeDecreased");
        private static readonly AccessTools.FieldRef<UnitAI_MoleChieftain, bool> GoToDrumStage = AccessTools.FieldRefAccess<UnitAI_MoleChieftain, bool>("goToDrumStage");
        private static readonly AccessTools.FieldRef<UnitAI_MoleChieftain, bool> NeedToEscape = AccessTools.FieldRefAccess<UnitAI_MoleChieftain, bool>("needToEscapeFromDrumStage");
        private static readonly AccessTools.FieldRef<UnitAI_MoleChieftain, bool> DrillZoneReady = AccessTools.FieldRefAccess<UnitAI_MoleChieftain, bool>("isDrillZoneInitialized");
        private static readonly AccessTools.FieldRef<UnitAI_MoleChieftain, Vector2> DrillZoneBottomLeft = AccessTools.FieldRefAccess<UnitAI_MoleChieftain, Vector2>("drillZoneBottomLeft");
        private static readonly AccessTools.FieldRef<UnitAI_MoleChieftain, Vector2> DrillZoneTopRight = AccessTools.FieldRefAccess<UnitAI_MoleChieftain, Vector2>("drillZoneTopRight");
        private static readonly AccessTools.FieldRef<UnitAI_MoleChieftain, Unit_MoleChieftain> BossOf = AccessTools.FieldRefAccess<UnitAI_MoleChieftain, Unit_MoleChieftain>("moleCheiftain");
        private static readonly AccessTools.FieldRef<UnitAI_MoleChieftain, int> AiPhase = AccessTools.FieldRefAccess<UnitAI_MoleChieftain, int>("aiPhase");
        private static readonly AccessTools.FieldRef<UnitAI_MoleChieftain, MoleDrumStage> AiDrumStage = AccessTools.FieldRefAccess<UnitAI_MoleChieftain, MoleDrumStage>("drumStage");
        private static readonly AccessTools.FieldRef<UnitAI_MoleChieftain, Vector2> AiStageBottomLeft = AccessTools.FieldRefAccess<UnitAI_MoleChieftain, Vector2>("drumStageBottomLeft");
        private static readonly AccessTools.FieldRef<UnitAI_MoleChieftain, Vector2> AiStageTopRight = AccessTools.FieldRefAccess<UnitAI_MoleChieftain, Vector2>("drumStageTopRight");
        private static readonly AccessTools.FieldRef<Unit_MoleChieftain, bool> DrumAttacking = AccessTools.FieldRefAccess<Unit_MoleChieftain, bool>("isDrumStageAttacking");
        private static readonly AccessTools.FieldRef<Unit_MoleChieftain, bool> Hovering = AccessTools.FieldRefAccess<Unit_MoleChieftain, bool>("superJumpDynamite");
        private static readonly AccessTools.FieldRef<Unit_MoleChieftain, Trigger> JumpDynamiteTrigger = AccessTools.FieldRefAccess<Unit_MoleChieftain, Trigger>("superJumpDynamiteTrigger");
        private static readonly AccessTools.FieldRef<Unit_MoleChieftain, Vector2> StageBottomLeft = AccessTools.FieldRefAccess<Unit_MoleChieftain, Vector2>("drumStageBottomLeft");
        private static readonly AccessTools.FieldRef<Unit_MoleChieftain, Vector2> StageTopRight = AccessTools.FieldRefAccess<Unit_MoleChieftain, Vector2>("drumStageTopRight");
        private static readonly AccessTools.FieldRef<Unit_MoleChieftain, bool> HandStompFiring = AccessTools.FieldRefAccess<Unit_MoleChieftain, bool>("isHandStompFiring");
        private static readonly AccessTools.FieldRef<Unit_MoleChieftain, UI_AOEWarning_RangeAttackLine> HandStompWarning = AccessTools.FieldRefAccess<Unit_MoleChieftain, UI_AOEWarning_RangeAttackLine>("handStompWarning");
        private static readonly AccessTools.FieldRef<Unit_MoleChieftain, List<CombatBehaviour>> HandStompHit = AccessTools.FieldRefAccess<Unit_MoleChieftain, List<CombatBehaviour>>("handStompSharedHit");
        private static readonly Dictionary<Unit_MoleChieftain, Extra> Extras = new Dictionary<Unit_MoleChieftain, Extra>();
        private static readonly List<Unit_MoleChieftain> Snapshot = new List<Unit_MoleChieftain>();

        private sealed class Sweeper
        {
            internal bool Active;
            internal bool Vertical;
            internal float Position;
            internal float Fixed;
            internal int Index;
            internal int Direction;
            internal float NextStep;
            internal bool ReturnOnEnd;
            internal bool Returned;
        }

        private sealed class Extra
        {
            internal bool WasHovering;
            internal bool WasResting;
            internal float BlockUntil;
            internal float NextHoverRock;
            internal float NextPhaseRock;
            internal float NextRain;
            internal bool RainRunning;
            internal readonly Sweeper RainHorizontal = new Sweeper();
            internal float NextSweep;
            internal bool SweepRunning;
            internal readonly Sweeper[] Lines = { new Sweeper(), new Sweeper() };
            internal UnitAI_MoleChieftain Ai;
            internal Coroutine StompRoutine;
            internal bool LandingLineShown;
            internal bool PendingLandingLine;
        }

        private sealed class ChainState
        {
            internal string Pending;
            internal int Chain;
            internal float ReadyAt;
            internal float ExpireAt;
            internal float CooldownUntil;
        }

        private static BossChallenge_MoleChieftain current;
        private static bool replayingRock;
        private readonly Dictionary<Unit_MoleChieftain, ChainState> chainStates = new Dictionary<Unit_MoleChieftain, ChainState>();
        private readonly List<Unit_MoleChieftain> chainSnapshot = new List<Unit_MoleChieftain>();
        private bool replayingChain;

        public BossChallenge_MoleChieftain()
        {
            current = this;
            BossDialogue.RegisterEncounter(typeof(UnitAI_MoleChieftain), ai =>
            {
                if (Rules.Level >= ExtraPatternLevel) BossDialogue.SayEncounter(ai, BossName, EncounterLine, Rules.Level);
            });
        }

        public void Tick()
        {
            Prune();

            if (Rules.Level < 1)
            {
                if (chainStates.Count > 0) chainStates.Clear();
                return;
            }

            chainSnapshot.Clear();
            chainSnapshot.AddRange(chainStates.Keys);
            foreach (Unit_MoleChieftain boss in chainSnapshot)
            {
                ChainState state = chainStates[boss];
                if (boss == null || boss.IsDead)
                {
                    chainStates.Remove(boss);
                    continue;
                }

                if (string.IsNullOrEmpty(state.Pending)) continue;

                float now = Time.unscaledTime;
                if (now > state.ExpireAt)
                {
                    state.Pending = null;
                    continue;
                }

                if (IsAttacking(boss))
                {
                    state.ReadyAt = now + FollowUpDelay;
                    continue;
                }

                if (now >= state.ReadyAt) FollowUp(boss, state);
            }
        }

        public void Cleanup()
        {
            chainStates.Clear();
            replayingChain = false;
            Extras.Clear();
            replayingRock = false;
        }

        private static bool IsAttacking(Unit_MoleChieftain boss)
        {
            return boss.IsAttackingServerside;
        }

        private static string Next(string started)
        {
            switch (started)
            {
                case "BasicAttack": return Random.value < 0.5f ? "BasicAttack_Double" : "DashAttack";
                case "BasicAttack_Double": return Random.value < 0.5f ? "Windmill" : "DashAttack";
                case "ThrowDynamite": return "ThrowDynamite";
                case "HandStomp": return "Windmill";
                default: return null;
            }
        }

        private static bool TryGetTarget(Unit_MoleChieftain boss, string pattern, out UnitAvatar target)
        {
            UnitAI_MoleChieftain ai = boss.GetComponent<UnitAI_MoleChieftain>();
            target = null;
            if (ai == null || !IsEngaged(ai, boss) || !boss.TopdownRigidbody.IsGrounded) return false;

            target = ai.CurrentTarget;
            return Vector2.Distance(boss.transform.position, target.transform.position) <= Reach(pattern);
        }

        private static void Invoke(Unit_MoleChieftain boss, string pattern, UnitAvatar target)
        {
            boss.Aim(target.transform.position);
            switch (pattern)
            {
                case "BasicAttack": boss.BasicAttack(); break;
                case "BasicAttack_Double": boss.BasicAttack_Double(); break;
                case "DashAttack": boss.DashAttack(0); break;
                case "Windmill": boss.Windmill(); break;
                case "ThrowDynamite": boss.ThrowDynamite(); break;
            }
        }

        private void OnPatternStarted(Unit_MoleChieftain boss, string pattern, bool wasAttacking)
        {
            if (wasAttacking || replayingChain || !IsAttacking(boss)) return;

            Schedule(boss, pattern, false);
        }

        private ChainState GetChainState(Unit_MoleChieftain boss)
        {
            ChainState state;
            if (!chainStates.TryGetValue(boss, out state))
            {
                state = new ChainState();
                chainStates[boss] = state;
            }

            return state;
        }

        private void Schedule(Unit_MoleChieftain boss, string started, bool followUp)
        {
            if (Rules.Level < 1) return;

            ChainState state = GetChainState(boss);
            if (!followUp)
            {
                if (Time.unscaledTime < state.CooldownUntil) return;

                state.Chain = 0;
            }

            string next = Next(started);
            if (string.IsNullOrEmpty(next) || state.Chain >= Rules.MaxChain || Random.value >= Rules.ChainChance) return;

            if (!followUp) state.CooldownUntil = Time.unscaledTime + Rules.ChainCooldown;

            state.Pending = next;
            state.ReadyAt = Time.unscaledTime + FollowUpDelay;
            state.ExpireAt = Time.unscaledTime + PendingLifetime;
        }

        private void FollowUp(Unit_MoleChieftain boss, ChainState state)
        {
            string pattern = state.Pending;
            state.Pending = null;

            UnitAvatar target;
            if (!TryGetTarget(boss, pattern, out target)) return;

            state.Chain++;
            replayingChain = true;
            try
            {
                Invoke(boss, pattern, target);
            }
            finally
            {
                replayingChain = false;
            }

            if (IsAttacking(boss)) Schedule(boss, pattern, true);
        }

        private static float Reach(string pattern)
        {
            switch (pattern)
            {
                case "BasicAttack": return 3.5f;
                case "BasicAttack_Double": return 6f;
                case "Windmill": return 4f;
                default: return float.MaxValue;
            }
        }

        private static void Prune()
        {
            if (Extras.Count == 0) return;

            Snapshot.Clear();
            Snapshot.AddRange(Extras.Keys);
            foreach (Unit_MoleChieftain boss in Snapshot)
                if (boss == null) Extras.Remove(boss);
        }

        private static bool IsEngaged(UnitAI_MoleChieftain ai, Unit_MoleChieftain boss)
        {
            return boss != null && !boss.IsDead && ai.CurrentTarget != null && !ai.CurrentTarget.IsDead && !ai.IsLifeDecreased && !GoToDrumStage(ai) && !NeedToEscape(ai);
        }
        private static Extra GetExtra(Unit_MoleChieftain boss)
        {
            Extra extra;
            if (!Extras.TryGetValue(boss, out extra))
            {
                extra = new Extra();
                Extras[boss] = extra;
            }

            return extra;
        }

        private static Vector2 RandomIn(Vector2 bottomLeft, Vector2 topRight)
        {
            return new Vector2(Random.Range(bottomLeft.x, topRight.x), Random.Range(bottomLeft.y, topRight.y));
        }

        private static void ThrowDynamite(Unit_MoleChieftain boss, Vector3 to)
        {
            boss.throwDynamiteFireData.CreateAttack(EDamageFromType.DirectAttack, (float)boss.RequestStandardDamage() * DynamiteDamageRate, "BasicAttack", false, boss, boss.transform.position, to, boss.TopdownActor.size.y * 0.5f, null);
        }

        private static void Burst(Unit_MoleChieftain boss, int count, float minDistance, float maxDistance)
        {
            float step = 360f / count;
            float offset = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = HorayUtility.GetVector3FromAngle(i * step + offset + Random.Range(-15, 15));
                ThrowDynamite(boss, boss.transform.position + direction * Random.Range(minDistance, maxDistance));
            }
        }

        private static IEnumerator StompRoutine(Unit_MoleChieftain boss, Extra extra)
        {
            Vector3 origin = boss.transform.position;
            List<CombatBehaviour> shared = HandStompHit(boss);
            bool[] blocked = new bool[StompRays];
            for (int step = 0; step < StompSteps; step++)
            {
                if (boss == null || boss.IsDead) break;

                for (int ray = 0; ray < StompRays; ray++)
                {
                    if (blocked[ray] || (step == 0 && ray > 0)) continue;

                    Vector3 position = origin + (Vector3)(HorayUtility.GetVector2FromAngle(ray * (360f / StompRays)) * StompStepDistance * step);
                    if (step > 0 && Physics2D.OverlapPoint(position, CombatManager.TileLayerMask))
                    {
                        blocked[ray] = true;
                        continue;
                    }

                    Bullet.Pool.Spawn(boss.handStompPrefab, position, false, EDamageFromType.DirectAttack, "BasicAttack", boss.RequestStandardDamage(), 0, 0f, boss, boss.GetHostileFactionLayers(EDamageFromType.DirectAttack), 0f, position, position, shared, null);
                }

                yield return new WaitForSeconds(StompInterval);
            }

            extra.StompRoutine = null;
        }
        private static void DropRock(Unit_MoleChieftain boss, MoleDrumStage stage, Vector2 position)
        {
            stage.Hit((float)boss.RequestStandardDamage() * RockDamageRate, boss, position, position);
        }

        private static void Patterns(Unit_MoleChieftain boss, Extra extra, bool hovering)
        {
            if (extra.Ai == null) extra.Ai = boss.GetComponent<UnitAI_MoleChieftain>();

            UnitAI_MoleChieftain ai = extra.Ai;
            MoleDrumStage stage = ai == null ? null : AiDrumStage(ai);
            if (stage == null) return;

            bool resting = boss.DrumStageAnimation || DrumAttacking(boss);
            if (extra.WasResting && !resting) extra.BlockUntil = Time.time + RestGrace;
            extra.WasResting = resting;

            bool ready = !hovering && !resting && Time.time >= extra.BlockUntil && IsEngaged(ai, boss);
            bool secondPhase = AiPhase(ai) >= 1;
            Rain(boss, stage, ai, extra, ready);
            if (Rules.Level >= SweepLevel) Sweep(boss, stage, ai, extra, ready, secondPhase);
        }

        private static void Rain(Unit_MoleChieftain boss, MoleDrumStage stage, UnitAI_MoleChieftain ai, Extra extra, bool ready)
        {
            if (!extra.RainRunning)
            {
                if (extra.NextRain <= 0f || Time.time < extra.NextRain || !ready || extra.SweepRunning) return;

                extra.RainRunning = true;
                BeginRain(extra.RainHorizontal);
            }

            StepRain(boss, stage, ai, extra.RainHorizontal);
            if (extra.RainHorizontal.Active) return;

            extra.RainRunning = false;
            extra.NextRain = Time.time + RainInterval;
            extra.BlockUntil = Time.time + PatternGap;
        }

        private static void BeginRain(Sweeper sweeper)
        {
            sweeper.Index = 0;
            sweeper.Active = true;
            sweeper.NextStep = 0f;
        }

        private static void Begin(Sweeper sweeper, UnitAI_MoleChieftain ai)
        {
            Vector2 bottomLeft = AiStageBottomLeft(ai);
            Vector2 topRight = AiStageTopRight(ai);
            sweeper.Direction = Random.value < 0.5f ? 1 : -1;
            sweeper.Active = true;
            sweeper.NextStep = 0f;
            sweeper.Returned = false;
            sweeper.Position = sweeper.Vertical ? (sweeper.Direction > 0 ? bottomLeft.y : topRight.y) : (sweeper.Direction > 0 ? bottomLeft.x : topRight.x);
            sweeper.Fixed = sweeper.Vertical ? Random.Range(bottomLeft.x, topRight.x) : Random.Range(bottomLeft.y, topRight.y);
        }

        private static void StepRain(Unit_MoleChieftain boss, MoleDrumStage stage, UnitAI_MoleChieftain ai, Sweeper sweeper)
        {
            if (!sweeper.Active || Time.time < sweeper.NextStep) return;

            Vector2 bottomLeft = AiStageBottomLeft(ai);
            Vector2 topRight = AiStageTopRight(ai);
            float centerX = (bottomLeft.x + topRight.x) * 0.5f;
            float halfWidth = (topRight.x - bottomLeft.x) * 0.5f;
            int columns = Mathf.Max(1, Mathf.RoundToInt(halfWidth / RainStep));
            float stepX = halfWidth / columns;
            float height = topRight.y - bottomLeft.y;
            int rows = Mathf.Max(1, Mathf.RoundToInt(height / (RainStep * 2f)));
            float stepY = height / rows;

            if (sweeper.Index <= columns)
            {
                float offset = sweeper.Index * stepX;
                float start = bottomLeft.y + (sweeper.Index & 1) * stepY * 0.5f;
                for (int side = -1; side <= 1; side += 2)
                {
                    if (sweeper.Index == 0 && side > 0) continue;

                    for (float y = start; y <= topRight.y + 0.01f; y += stepY)
                        DropRock(boss, stage, new Vector2(centerX + side * offset, Mathf.Min(y, topRight.y)));
                }
            }

            sweeper.Index++;
            sweeper.NextStep = Time.time + RainDelay;
            if (sweeper.Index > columns) sweeper.Active = false;
        }
        private static void Advance(Sweeper sweeper, Vector2 bottomLeft, Vector2 topRight, float step, float delay)
        {
            sweeper.Position += step * sweeper.Direction;
            sweeper.NextStep = Time.time + delay;
            float min = sweeper.Vertical ? bottomLeft.y : bottomLeft.x;
            float max = sweeper.Vertical ? topRight.y : topRight.x;
            if (sweeper.Position >= min && sweeper.Position <= max) return;

            if (sweeper.ReturnOnEnd && !sweeper.Returned)
            {
                sweeper.Returned = true;
                sweeper.Direction = -sweeper.Direction;
                sweeper.Position = Mathf.Clamp(sweeper.Position, min, max);
                return;
            }

            sweeper.Active = false;
        }
        private static void Sweep(Unit_MoleChieftain boss, MoleDrumStage stage, UnitAI_MoleChieftain ai, Extra extra, bool ready, bool secondPhase)
        {
            if (!extra.SweepRunning)
            {
                if (extra.NextSweep <= 0f || Time.time < extra.NextSweep || !ready || extra.RainRunning) return;

                extra.SweepRunning = true;
                extra.Lines[0].Vertical = Random.value < 0.5f;
                Begin(extra.Lines[0], ai);
                BeginSecondLine(extra.Lines[1], extra.Lines[0], ai);
                extra.Lines[0].ReturnOnEnd = secondPhase;
                extra.Lines[1].ReturnOnEnd = secondPhase;
            }

            foreach (Sweeper line in extra.Lines) StepLine(boss, stage, ai, line);
            if (extra.Lines[0].Active || extra.Lines[1].Active) return;

            extra.SweepRunning = false;
            extra.NextSweep = Time.time + SweepInterval;
            extra.BlockUntil = Time.time + PatternGap;
        }

        private static void BeginSecondLine(Sweeper line, Sweeper first, UnitAI_MoleChieftain ai)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                line.Vertical = Random.value < 0.5f;
                Begin(line, ai);
                if (line.Vertical != first.Vertical || Mathf.Abs(line.Fixed - first.Fixed) >= SweepMinGap) return;
            }
        }

        private static void StepLine(Unit_MoleChieftain boss, MoleDrumStage stage, UnitAI_MoleChieftain ai, Sweeper line)
        {
            if (!line.Active || Time.time < line.NextStep) return;

            Vector2 bottomLeft = AiStageBottomLeft(ai);
            Vector2 topRight = AiStageTopRight(ai);
            for (int i = 0; i < 2; i++)
            {
                float cross = line.Fixed + (i - 0.5f) * SweepRowSpacing;
                DropRock(boss, stage, line.Vertical ? new Vector2(Mathf.Clamp(cross, bottomLeft.x, topRight.x), line.Position) : new Vector2(line.Position, Mathf.Clamp(cross, bottomLeft.y, topRight.y)));
            }

            Advance(line, bottomLeft, topRight, SweepStep, SweepDelay);
        }
        [HarmonyPatch(typeof(UnitAI_BossBasic), nameof(UnitAI_BossBasic.OnBattleStartServer))]
        private static class BattleStart
        {
            [HarmonyPostfix]
            private static void After(UnitAI_BossBasic __instance)
            {
                if (Rules.Level < ExtraPatternLevel || !(__instance is UnitAI_MoleChieftain mole)) return;

                Unit_MoleChieftain boss = __instance.GetComponent<Unit_MoleChieftain>();
                if (boss == null) return;

                Extra extra = GetExtra(boss);
                extra.NextRain = Time.time + RainInterval;
                extra.NextSweep = Time.time + SweepInterval;
                extra.NextPhaseRock = Time.time + PhaseRockInterval;
                extra.RainRunning = false;
                extra.SweepRunning = false;
                extra.RainHorizontal.Active = false;
                extra.Lines[0].Active = false;
                extra.Lines[1].Active = false;
                extra.LandingLineShown = false;
                extra.PendingLandingLine = false;

                BossDialogue.ClearThresholds(mole);
                BossDialogue.ResetDeathLine(mole);
                BossDialogue.RegisterDeathOverride(typeof(UnitAI_MoleChieftain), BossName,
                    new[] { mole.tempStageDeadly1Speech.key, mole.tempStageDeadly2Speech.key }, DeathLine, () => Rules.Level);
                BossDialogue.SayEncounter(mole, BossName, EncounterLine, Rules.Level);
            }
        }

        [HarmonyPatch(typeof(Unit_MoleChieftain), nameof(Unit_MoleChieftain.SuperJump_Move))]
        private static class DrumEscapeJumpStart
        {
            [HarmonyPostfix]
            private static void After(Unit_MoleChieftain __instance)
            {
                if (Rules.Level < ExtraPatternLevel || !__instance.isServer) return;

                UnitAI_MoleChieftain ai = __instance.GetComponent<UnitAI_MoleChieftain>();
                if (ai == null || ai.Life - ai.CurrentLife < 1) return;

                GetExtra(__instance).PendingLandingLine = true;
                if (!__instance.IsDead && __instance.hp < __instance.MaxHp) __instance.Networkhp = __instance.MaxHp;
            }
        }

        [HarmonyPatch(typeof(Unit_MoleChieftain), "EndOfSuperJump_JustLand")]
        private static class LandingLineTrigger
        {
            [HarmonyPostfix]
            private static void After(Unit_MoleChieftain __instance)
            {
                if (Rules.Level < ExtraPatternLevel || !__instance.isServer) return;

                Extra extra = GetExtra(__instance);
                if (!extra.PendingLandingLine) return;

                extra.PendingLandingLine = false;
                if (extra.LandingLineShown) return;

                extra.LandingLineShown = true;
                UnitAI_MoleChieftain ai = __instance.GetComponent<UnitAI_MoleChieftain>();
                if (ai != null) BossDialogue.Say(ai, BossName, LandingLine, Rules.Level);
            }
        }
        [HarmonyPatch(typeof(UnitAI_MoleChieftain), "OnAIUpdate_FoundEnemy")]
        private static class Pulse
        {
            [HarmonyPostfix]
            private static void After(UnitAI_MoleChieftain __instance)
            {
                int level = Rules.Level;
                if (level < ExtraPatternLevel) return;

                Unit_MoleChieftain boss = BossOf(__instance);
                if (boss != null && !LifeDecreased(__instance) && boss.MaxHp > 0f && boss.hp / boss.MaxHp < 0.5f)
                {
                    int phase = __instance.Life - __instance.CurrentLife;
                    if (BossDialogue.TryFireThreshold(__instance, "LowHp_Phase" + phase))
                    {
                        string[] lines = phase >= 1 ? Phase2LowHpLines : Phase1LowHpLines;
                        BossDialogue.Say(__instance, BossName, lines[Random.Range(0, lines.Length)], Rules.Level);
                    }
                }

                MoleDrumStage stage = AiDrumStage(__instance);
                if (stage == null || !IsEngaged(__instance, boss) || boss.DrumStageAnimation) return;

                Extra extra = GetExtra(boss);
                if (Time.time < extra.NextPhaseRock) return;

                extra.NextPhaseRock = Time.time + PhaseRockInterval;
                DropRock(boss, stage, (Vector2)__instance.CurrentTarget.transform.position + Random.insideUnitCircle * PhaseRockRadius);
            }
        }

        [HarmonyPatch(typeof(Unit_MoleChieftain), "Update")]
        private static class HoverPulse
        {
            [HarmonyPostfix]
            private static void After(Unit_MoleChieftain __instance)
            {
                if (Rules.Level < ExtraPatternLevel || __instance.IsDead) return;

                Extra extra = GetExtra(__instance);
                bool hovering = Hovering(__instance);
                if (hovering && Time.time >= extra.NextHoverRock)
                {
                    extra.NextHoverRock = Time.time + HoverRockInterval;
                    UnitAI_MoleChieftain ai = __instance.GetComponent<UnitAI_MoleChieftain>();
                    MoleDrumStage stage = ai != null ? AiDrumStage(ai) : null;
                    if (stage != null)
                        for (int i = 0; i < HoverRockCount; i++) DropRock(__instance, stage, RandomIn(AiStageBottomLeft(ai), AiStageTopRight(ai)));
                }

                if (extra.WasHovering && !hovering) Burst(__instance, Rules.Pick(6, 12), JumpBurstMin, JumpBurstMax);
                extra.WasHovering = hovering;

                Patterns(__instance, extra, hovering);
            }
        }

        [HarmonyPatch(typeof(Unit_MoleChieftain), nameof(Unit_MoleChieftain.SuperJump_Dynamite))]
        private static class JumpStart
        {
            [HarmonyPostfix]
            private static void After(Unit_MoleChieftain __instance)
            {
                if (Rules.Level < ExtraPatternLevel) return;

                GetExtra(__instance).NextHoverRock = Time.time + HoverRockInterval;
            }
        }

        [HarmonyPatch(typeof(Unit_MoleChieftain), nameof(Unit_MoleChieftain.JumpUpAnimation))]
        private static class JumpBurst
        {
            [HarmonyPrefix]
            private static void Before(Unit_MoleChieftain __instance)
            {
                if (Rules.Level < ExtraPatternLevel || !__instance.isServer || !JumpDynamiteTrigger(__instance).IsTriggered()) return;

                Burst(__instance, Rules.Pick(6, 12), JumpBurstMin, JumpBurstMax);
            }
        }
        [HarmonyPatch(typeof(Unit_MoleChieftain), nameof(Unit_MoleChieftain.SuperJump_Drill))]
        private static class DrillDynamite
        {
            [HarmonyPostfix]
            private static void After(Unit_MoleChieftain __instance)
            {
                if (Rules.Level < ExtraPatternLevel) return;

                __instance.drillDynamiteFireTimer.time *= 0.5f;
            }
        }

        [HarmonyPatch(typeof(Unit_MoleChieftain), nameof(Unit_MoleChieftain.ThrowDynamiteAnimation))]
        private static class SideDynamite
        {
            [HarmonyPostfix]
            private static void After(Unit_MoleChieftain __instance)
            {
                if (Rules.Level < ExtraPatternLevel || !__instance.isServer) return;

                Vector3 origin = __instance.transform.position;
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < SideDynamiteCount; i++)
                        ThrowDynamite(__instance, origin + new Vector3(side * Random.Range(SideDynamiteMin, SideDynamiteMax), Random.Range(-1.5f, 1.5f), 0f));
            }
        }

        [HarmonyPatch(typeof(Unit_MoleChieftain), nameof(Unit_MoleChieftain.FireWindmillAnimation))]
        private static class WindmillLine
        {
            [HarmonyPostfix]
            private static void After(Unit_MoleChieftain __instance)
            {
                if (Rules.Level < ExtraPatternLevel || !__instance.isServer) return;

                UnitAI_MoleChieftain ai = __instance.GetComponent<UnitAI_MoleChieftain>();
                if (ai != null) BossDialogue.SayUnused(ai, BossName, DynamiteLines, Rules.Level);
            }
        }

        [HarmonyPatch(typeof(Unit_MoleChieftain), nameof(Unit_MoleChieftain.FireHandStompAnimation))]
        private static class RadialStomp
        {
            [HarmonyPostfix]
            private static void After(Unit_MoleChieftain __instance)
            {
                if (Rules.Level < ExtraPatternLevel || !__instance.isServer || !HandStompFiring(__instance)) return;

                HandStompFiring(__instance) = false;
                Extra extra = GetExtra(__instance);
                if (extra.StompRoutine != null) __instance.StopCoroutine(extra.StompRoutine);

                extra.StompRoutine = __instance.StartCoroutine(StompRoutine(__instance, extra));
            }
        }

        [HarmonyPatch(typeof(Unit_MoleChieftain), "UserCode_RpcSetHandStompAnimationTrigger__Vector3__Vector3")]
        private static class RadialStompWarning
        {
            [HarmonyPostfix]
            private static void After(Unit_MoleChieftain __instance, Vector3 pos)
            {
                if (BossChallengeNet.GetDisplayLevel(typeof(Unit_MoleChieftain)) < ExtraPatternLevel || (CombatManager.Instance && !CombatManager.Instance.CheckAttackWarningVisible(__instance))) return;

                UI_AOEWarning_RangeAttackLine vanilla = HandStompWarning(__instance);
                if (vanilla != null) vanilla.Despawn();

                float length = (StompSteps - 1) * StompStepDistance;
                for (int ray = 0; ray < StompRays; ray++)
                {
                    Vector3 to = pos + (Vector3)(HorayUtility.GetVector2FromAngle(ray * (360f / StompRays)) * length);
                    UI_AOEWarning_RangeAttackLine line = AOEWarningFactory.CreateAoe_RangeAttackLine(pos, to, AOEWarningFactory.HostileWarningColor, StompWarningTime);
                    if (ray == 0) HandStompWarning(__instance) = line;
                }
            }
        }
        [HarmonyPatch(typeof(UnitAvatar), nameof(UnitAvatar.CancelCurrentAction))]
        private static class StompCancel
        {
            [HarmonyPostfix]
            private static void After(UnitAvatar __instance)
            {
                Unit_MoleChieftain boss = __instance as Unit_MoleChieftain;
                Extra extra;
                if (boss == null || !Extras.TryGetValue(boss, out extra) || extra.StompRoutine == null) return;

                boss.StopCoroutine(extra.StompRoutine);
                extra.StompRoutine = null;
            }
        }
        [HarmonyPatch(typeof(MoleDrumStage), nameof(MoleDrumStage.Hit))]
        private static class Rocks
        {
            [HarmonyPostfix]
            private static void After(MoleDrumStage __instance, float damage, UnitAvatar owner)
            {
                if (replayingRock || Rules.Level < ExtraPatternLevel) return;

                Unit_MoleChieftain boss = owner as Unit_MoleChieftain;
                if (boss == null || !DrumAttacking(boss)) return;

                replayingRock = true;
                try
                {
                    for (int i = 0; i < Rules.Pick(2, 3); i++)
                        __instance.Hit(damage, owner, boss.transform.position, RandomIn(StageBottomLeft(boss), StageTopRight(boss)));
                }
                finally
                {
                    replayingRock = false;
                }
            }
        }

        [HarmonyPatch]
        private static class Started
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                foreach (string name in new[] { "BasicAttack", "BasicAttack_Double", "DashAttack", "Windmill", "HandStomp", "ThrowDynamite" })
                {
                    MethodBase method = AccessTools.Method(typeof(Unit_MoleChieftain), name);
                    if (method != null) yield return method;
                }
            }

            [HarmonyPrefix]
            private static void Before(Unit_MoleChieftain __instance, out bool __state)
            {
                __state = __instance.IsAttackingServerside;
            }

            [HarmonyPostfix]
            private static void After(Unit_MoleChieftain __instance, bool __state, MethodBase __originalMethod)
            {
                current.OnPatternStarted(__instance, __originalMethod.Name, __state);
            }
        }
    }
}
