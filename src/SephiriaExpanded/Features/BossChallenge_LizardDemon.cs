using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SephiriaExpanded
{
    internal sealed class BossChallenge_LizardDemon : IBossChallenge
    {
        private static BossChallengeRules Rules => BossChallenge.LizardDemon;

        public BossChallenge_LizardDemon()
        {
            BossDialogue.RegisterEncounter(typeof(UnitAI_LizardDemon), ai =>
            {
                if (IsEnhanced()) BossDialogue.SayEncounter(ai, BossName, EncounterLine, Rules.Level);
            });
        }

        private const string BossName = "크라즈";
        private const string EncounterLine = "이곳은 클리포트 님의 안식처. 지나가려면 저를 쓰러뜨리십시오.";
        private const string DeathLine = "죄송합니다. 클리포트 님...";
        private const string DeathKey = "Death";
        private static readonly float[] HpRatios = { 0.9f, 0.5f, 0.25f };
        private static readonly string[] HpLines = { "이 힘. 알 것 같군요.", "최선입니까?", "멈추십시오." };

        private const float FullCircle = 360f;
        private const float AngleEpsilon = 0.01f;
        private const float HeadSmashHeight = 0.5f;
        private const string HeadSmashDamageId = "HeadSmash";
        private const float DashSpeedRate = 1.15f;
        private const int HeadSmashVanillaFires = 2;
        private const float HeadSmashMinGap = 0.1f;
        private const int CircleRemovedBullets = 4;

        private sealed class State
        {
            internal float BaseDashTime;
            internal int SmashFires;
            internal float FirstFireTime;
            internal bool ExtraPending;
            internal bool EndDeferred;
        }

        private static readonly Dictionary<Unit_LizardDemon, State> States = new Dictionary<Unit_LizardDemon, State>();
        private static bool firingExtra;

        public void Tick()
        {
        }

        public void Cleanup()
        {
            States.Clear();
        }

        private static bool IsEnhanced()
        {
            return Rules.Level >= 1;
        }

        private static bool IsHard()
        {
            return Rules.Level >= 2;
        }

        private static State StateOf(Unit_LizardDemon unit)
        {
            State state;
            if (!States.TryGetValue(unit, out state))
            {
                state = new State { BaseDashTime = unit.dashTimer.time };
                States[unit] = state;
            }

            return state;
        }

        [HarmonyPatch(typeof(UnitAI_BossBasic), nameof(UnitAI_BossBasic.OnBattleStartServer))]
        private static class BattleStart
        {
            [HarmonyPostfix]
            private static void After(UnitAI_BossBasic __instance)
            {
                if (!IsEnhanced() || !(__instance is UnitAI_LizardDemon lizard)) return;

                BossDialogue.ClearThresholds(lizard);
                BossDialogue.SayEncounter(lizard, BossName, EncounterLine, Rules.Level);
            }
        }

        [HarmonyPatch(typeof(Unit_LizardDemon), "Update")]
        private static class UpdateWatch
        {
            [HarmonyPostfix]
            private static void After(Unit_LizardDemon __instance)
            {
                if (__instance.isServer && IsEnhanced()) BossDialogue.CheckHpLines(__instance.GetComponent<UnitAI_LizardDemon>(), BossName, HpRatios, HpLines, Rules.Level);
            }
        }

        [HarmonyPatch(typeof(UnitAI_LizardDemon), "BeginDramaticDie")]
        private static class DeathSpeech
        {
            [HarmonyPostfix]
            private static void After(UnitAI_LizardDemon __instance)
            {
                if (!IsEnhanced() || !__instance.isServer) return;

                if (BossDialogue.TryFireThreshold(__instance, DeathKey)) BossDialogue.Say(__instance, BossName, DeathLine, Rules.Level);
            }
        }

        [HarmonyPatch(typeof(Unit_LizardDemon), nameof(Unit_LizardDemon.Anim_HeadSmash_Start))]
        private static class SmashStart
        {
            [HarmonyPostfix]
            private static void After(Unit_LizardDemon __instance)
            {
                if (!__instance.isServer) return;

                State state = StateOf(__instance);
                state.SmashFires = 0;
                state.ExtraPending = false;
                state.EndDeferred = false;
            }
        }

        [HarmonyPatch(typeof(Unit_LizardDemon), nameof(Unit_LizardDemon.Anim_HeadSmash_Fire))]
        private static class SmashFire
        {
            [HarmonyPostfix]
            private static void After(Unit_LizardDemon __instance)
            {
                if (!__instance.isServer || !IsHard()) return;

                FillCircle(__instance);
                if (firingExtra || __instance.currentAction != Unit_LizardDemon.EAnimation.HeadSmash_Double) return;

                State state = StateOf(__instance);
                state.SmashFires++;
                if (state.SmashFires == 1) state.FirstFireTime = Time.time;
                if (state.SmashFires != HeadSmashVanillaFires || state.ExtraPending) return;

                state.ExtraPending = true;
                __instance.StartCoroutine(ExtraSmash(__instance, state, Mathf.Max(HeadSmashMinGap, Time.time - state.FirstFireTime)));
            }
        }

        private static void FillCircle(Unit_LizardDemon unit)
        {
            NewWeaponFireData_BulletSpread spread = unit.headSmashFireData as NewWeaponFireData_BulletSpread;
            if (spread == null || spread.bulletPrefab == null || spread.betweenAngleDegrees <= 0f || spread.spreadAngle >= FullCircle) return;

            Vector2 from = unit.transform.position;
            Vector2 toTarget = unit.headSmashTargetPosition - from;
            float baseAngle = HorayUtility.GetAngle(unit.headSmashTargetPosition, from);
            float half = spread.spreadAngle * 0.5f;
            float damage = unit.RequestStandardDamage() * spread.damageMultiplier;
            long faction = unit.GetHostileFactionLayers(EDamageFromType.DirectAttack);
            int vanillaCount = Mathf.CeilToInt(spread.spreadAngle / spread.betweenAngleDegrees);
            float lastVanilla = -half + (vanillaCount - 1) * spread.betweenAngleDegrees;
            float nextVanilla = FullCircle - half;
            int extraCount = 0;
            for (float angle = lastVanilla + spread.betweenAngleDegrees; angle < nextVanilla - AngleEpsilon; angle += spread.betweenAngleDegrees)
                extraCount++;

            int count = extraCount - CircleRemovedBullets;
            if (count <= 0) return;

            float step = (nextVanilla - lastVanilla) / (count + 1);
            for (int i = 1; i <= count; i++)
            {
                Vector2 direction = HorayUtility.GetVector2FromAngle(lastVanilla + step * i + baseAngle);
                Bullet.Pool.Spawn(spread.bulletPrefab, from, false, EDamageFromType.DirectAttack, HeadSmashDamageId, damage, spread.staggeringLevel, spread.externalForcePower, unit, faction, HeadSmashHeight, from, from + direction * toTarget.magnitude, null, null);
            }
        }

        [HarmonyPatch(typeof(Unit_LizardDemon), nameof(Unit_LizardDemon.DashStart))]
        private static class DashSpeed
        {
            [HarmonyPrefix]
            private static void Before(Unit_LizardDemon __instance, bool onDashSlashAreaCreate, out float __state)
            {
                __state = __instance.dashAttackSpeed;
                if (!__instance.isServer || __instance.isDashing) return;

                State state = StateOf(__instance);
                bool faster = onDashSlashAreaCreate && IsHard();
                __instance.dashTimer.time = faster ? state.BaseDashTime / DashSpeedRate : state.BaseDashTime;
                if (faster) __instance.dashAttackSpeed *= DashSpeedRate;
            }

            [HarmonyPostfix]
            private static void After(Unit_LizardDemon __instance, float __state)
            {
                __instance.dashAttackSpeed = __state;
            }
        }

        [HarmonyPatch(typeof(Unit_LizardDemon), nameof(Unit_LizardDemon.Anim_Dash_CreateWarning))]
        private static class DashWarning
        {
            [HarmonyPrefix]
            private static void Before(Unit_LizardDemon __instance, out float __state)
            {
                __state = __instance.dashTimer.time;
                if (__instance.isServer) __instance.dashTimer.time = StateOf(__instance).BaseDashTime;
            }

            [HarmonyPostfix]
            private static void After(Unit_LizardDemon __instance, float __state)
            {
                __instance.dashTimer.time = __state;
            }
        }

        [HarmonyPatch(typeof(Unit_LizardDemon), nameof(Unit_LizardDemon.Anim_HeadSmash_End))]
        private static class SmashEnd
        {
            [HarmonyPrefix]
            private static bool Before(Unit_LizardDemon __instance)
            {
                if (!__instance.isServer) return true;

                State state = StateOf(__instance);
                if (!state.ExtraPending) return true;

                state.EndDeferred = true;
                return false;
            }
        }

        private static IEnumerator ExtraSmash(Unit_LizardDemon unit, State state, float gap)
        {
            yield return new WaitForSeconds(gap);
            if (unit != null && !unit.IsDead && unit.currentAction == Unit_LizardDemon.EAnimation.HeadSmash_Double)
            {
                firingExtra = true;
                try
                {
                    unit.Anim_HeadSmash_Fire();
                }
                finally
                {
                    firingExtra = false;
                }
            }

            state.ExtraPending = false;
            if (unit != null && state.EndDeferred)
            {
                state.EndDeferred = false;
                unit.Anim_HeadSmash_End();
            }
        }
    }
}
