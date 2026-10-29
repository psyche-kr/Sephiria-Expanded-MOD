using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal interface IBossChallenge
    {
        void Tick();
        void Cleanup();
    }

    internal sealed class BossChallengeRules
    {
        private readonly Func<int> selectedLevel;

        internal BossChallengeRules(Func<int> selectedLevel)
        {
            this.selectedLevel = selectedLevel;
        }

        internal int SettingLevel => Mathf.Clamp(Difficulty.BossLevel(selectedLevel()), 0, 2);
        internal int Level => NetworkServer.active ? SettingLevel : 0;
        internal float ChainChance => Pick(0.5f, 1f);
        internal int MaxChain => 2;
        internal float ChainCooldown => Pick(5f, 2.5f);
        internal float HpMultiplier => Pick(2f, 4f);
        internal float DamageBonusPercent => Pick(NormalDamageBonus, HardDamageBonus);

        private const float NormalDamageBonus = 15f;
        private const float HardDamageBonus = 25f;

        internal static float DamageRate(int level)
        {
            return level >= 2 ? 1f + HardDamageBonus / 100f : level >= 1 ? 1f + NormalDamageBonus / 100f : 1f;
        }

        internal T Pick<T>(T normal, T hard)
        {
            return Level >= 2 ? hard : normal;
        }
    }

    internal static class BossChallenge
    {
        private static readonly IBossChallenge[] Challenges = { new BossChallenge_MoleChieftain(), new BossChallenge_MoleBigBomb(), new BossChallenge_RootDemon(), new BossChallenge_OinkKing(), new BossChallenge_PantherRogueKing(), new BossChallenge_MadArmadillo(), new BossChallenge_LibraryGuard(), new BossChallenge_Askard(), new BossChallenge_BirdDemon(), new BossChallenge_LizardDemon(), new BossChallenge_QBoss(), new BossBreak() };

        private const int AoePoolSize = 100;
        private static readonly Type[] EnlargedAoeTypes = { typeof(UI_AOEWarning_Ellipse), typeof(UI_AOEWarning_PantherArc), typeof(UI_AOEWarning_Rectangle) };
        private const float NormalRegenPercentPerSecond = 0.0005f;
        private const float HardRegenPercentPerSecond = 0.001f;
        private const int SilhouettePoolSize = 400;
        private static readonly FieldInfo SilhouettePoolMax = AccessTools.Field(typeof(SingleObjectPoolingFactory<SilhouetteFx>), "poolMax");

        private static readonly FieldInfo PoolMax = AccessTools.Field(typeof(UIObjectPoolingFactory), "poolMaxByObject");
        private static readonly Dictionary<UnitAvatar, float> HpScaled = new Dictionary<UnitAvatar, float>();

        internal static float HpScaleOf(UnitAvatar unit)
        {
            float scale;
            return unit != null && HpScaled.TryGetValue(unit, out scale) ? scale : 1f;
        }

        internal static readonly BossChallengeRules MoleChieftain = new BossChallengeRules(() => ModSettings.MoleChieftainBossChallengeLevel);
        internal static readonly BossChallengeRules MoleBigBomb = new BossChallengeRules(() => ModSettings.MoleBigBombBossChallengeLevel);
        internal static readonly BossChallengeRules RootDemon = new BossChallengeRules(() => ModSettings.RootDemonBossChallengeLevel);
        internal static readonly BossChallengeRules OinkKing = new BossChallengeRules(() => ModSettings.OinkKingBossChallengeLevel);
        internal static readonly BossChallengeRules PantherRogueKing = new BossChallengeRules(() => ModSettings.PantherRogueKingBossChallengeLevel);
        internal static readonly BossChallengeRules MadArmadillo = new BossChallengeRules(() => ModSettings.MadArmadilloBossChallengeLevel);
        internal static readonly BossChallengeRules LibraryGuard = new BossChallengeRules(() => ModSettings.LibraryGuardBossChallengeLevel);
        internal static readonly BossChallengeRules Askard = new BossChallengeRules(() => ModSettings.AskardBossChallengeLevel);
        internal static readonly BossChallengeRules BirdDemon = new BossChallengeRules(() => ModSettings.BirdDemonBossChallengeLevel);
        internal static readonly BossChallengeRules LizardDemon = new BossChallengeRules(() => ModSettings.LizardDemonBossChallengeLevel);
        internal static readonly BossChallengeRules QBoss = new BossChallengeRules(() => ModSettings.QBossChallengeLevel);
        internal static readonly BossChallengeRules QQBoss = new BossChallengeRules(() => ModSettings.QQBossChallengeLevel);
        internal static readonly BossChallengeRules[] AllRules = { MoleChieftain, MoleBigBomb, RootDemon, OinkKing, PantherRogueKing, MadArmadillo, LibraryGuard, Askard, BirdDemon, LizardDemon, QBoss, QQBoss };

        internal static BossChallengeRules For(Type unitType)
        {
            if (typeof(Unit_MoleChieftain).IsAssignableFrom(unitType)) return MoleChieftain;
            if (typeof(Unit_MoleBigBomb).IsAssignableFrom(unitType)) return MoleBigBomb;
            if (typeof(Unit_RootDemon).IsAssignableFrom(unitType)) return RootDemon;
            if (typeof(Unit_OinkKing).IsAssignableFrom(unitType)) return OinkKing;
            if (typeof(Unit_PantherRogueKing).IsAssignableFrom(unitType)) return PantherRogueKing;
            if (typeof(Unit_MadArmadillo).IsAssignableFrom(unitType)) return MadArmadillo;
            if (typeof(Unit_LibraryGuard).IsAssignableFrom(unitType)) return LibraryGuard;
            if (typeof(Unit_Askard).IsAssignableFrom(unitType) || typeof(Unit_Askard_Chapter2).IsAssignableFrom(unitType) || typeof(Unit_Askard_Chapter3).IsAssignableFrom(unitType)) return Askard;
            if (typeof(Unit_BirdDemon).IsAssignableFrom(unitType)) return BirdDemon;
            if (typeof(Unit_LizardDemon).IsAssignableFrom(unitType)) return LizardDemon;
            if (typeof(Unit_QBoss).IsAssignableFrom(unitType)) return QBoss;
            if (typeof(Unit_QQBoss).IsAssignableFrom(unitType) || typeof(Unit_QQQBoss).IsAssignableFrom(unitType)) return QQBoss;
            return null;
        }

        internal static string DisplayName(Type unitType)
        {
            if (typeof(Unit_MoleChieftain).IsAssignableFrom(unitType)) return "모그디";
            if (typeof(Unit_MoleBigBomb).IsAssignableFrom(unitType)) return "오드너";
            if (typeof(Unit_RootDemon).IsAssignableFrom(unitType)) return "하즈";
            if (typeof(Unit_OinkKing).IsAssignableFrom(unitType)) return "라타카";
            if (typeof(Unit_PantherRogueKing).IsAssignableFrom(unitType)) return "레리드";
            if (typeof(Unit_MadArmadillo).IsAssignableFrom(unitType)) return "에르마";
            if (typeof(Unit_LibraryGuard).IsAssignableFrom(unitType)) return "팬택시스";
            if (typeof(Unit_Askard).IsAssignableFrom(unitType) || typeof(Unit_Askard_Chapter2).IsAssignableFrom(unitType) || typeof(Unit_Askard_Chapter3).IsAssignableFrom(unitType)) return "아스카드";
            if (typeof(Unit_BirdDemon).IsAssignableFrom(unitType)) return "코베스";
            if (typeof(Unit_LizardDemon).IsAssignableFrom(unitType)) return "크라즈";
            if (typeof(Unit_QBoss).IsAssignableFrom(unitType) || typeof(Unit_QQBoss).IsAssignableFrom(unitType) || typeof(Unit_QQQBoss).IsAssignableFrom(unitType)) return "클리포트";
            return null;
        }

        internal static void Tick()
        {
            if (MoleChieftain.Level > 0 || MoleBigBomb.Level > 0 || RootDemon.Level > 0 || OinkKing.Level > 0 || PantherRogueKing.Level > 0 || MadArmadillo.Level > 0 ||
                LibraryGuard.Level > 0 || Askard.Level > 0 || BirdDemon.Level > 0 || LizardDemon.Level > 0 || QBoss.Level > 0 || QQBoss.Level > 0)
            {
                EnlargeAoePool();
                EnlargeSilhouettePool();
            }

            RegenScaledBosses();

            foreach (IBossChallenge challenge in Challenges)
                challenge.Tick();
        }

        private static void EnlargeSilhouettePool()
        {
            if (SilhouetteFx.Pool == null || SilhouettePoolMax == null) return;

            if ((int)SilhouettePoolMax.GetValue(SilhouetteFx.Pool) < SilhouettePoolSize) SilhouettePoolMax.SetValue(SilhouetteFx.Pool, SilhouettePoolSize);
        }

        private static void RegenScaledBosses()
        {
            if (!NetworkServer.active) return;

            foreach (KeyValuePair<UnitAvatar, float> pair in HpScaled)
            {
                UnitAvatar avatar = pair.Key;
                if (avatar == null || avatar.IsDead || avatar.hp >= avatar.MaxHp || IsDramaticDying(avatar)) continue;

                BossChallengeRules rules = For(avatar.GetType());
                float regen = rules != null ? rules.Pick(NormalRegenPercentPerSecond, HardRegenPercentPerSecond) : NormalRegenPercentPerSecond;
                avatar.Networkhp = Mathf.Min(avatar.MaxHp, avatar.hp + avatar.MaxHp * regen * Time.deltaTime);
            }
        }

        internal static bool IsDramaticDying(UnitAvatar avatar)
        {
            UnitAI_BossBasic ai = avatar.GetComponent<UnitAI_BossBasic>();
            return ai != null && ai.IsDramaticDying;
        }

        private static void EnlargeAoePool()
        {
            if (UIManager.Pool == null) return;

            Dictionary<Type, int> max = PoolMax.GetValue(UIManager.Pool) as Dictionary<Type, int>;
            if (max == null) return;

            foreach (Type type in EnlargedAoeTypes)
            {
                int current;
                if (max.TryGetValue(type, out current) && current < AoePoolSize) max[type] = AoePoolSize;
            }
        }

        internal static void Cleanup()
        {
            foreach (IBossChallenge challenge in Challenges)
                challenge.Cleanup();

            HpScaled.Clear();
        }

        [HarmonyPatch(typeof(UnitAI_BossBasic), nameof(UnitAI_BossBasic.OnBattleStartServer))]
        private static class HardPattern
        {
            [HarmonyPostfix]
            private static void After(UnitAI_BossBasic __instance)
            {
                BossChallengeRules rules = For(__instance.Avatar.GetType());
                if (rules == null || rules.Level < 1) return;

                UnitAvatar avatar = __instance.Avatar;
                if (avatar != null && !HpScaled.ContainsKey(avatar))
                {
                    float multiplier = RecommendedHpFactor(avatar, rules);
                    HpScaled[avatar] = multiplier;
                    if (multiplier > 1f)
                    {
                        avatar.NetworkmaxHp = avatar.maxHp * multiplier;
                        avatar.Networkhp = Math.Min(avatar.MaxHp, avatar.hp * multiplier);
                    }

                    BossChallengeNotice.Announce(avatar, rules.Level);
                }

                if (__instance is UnitAI_RootDemon) return;

                __instance.NetworkhardPatternActivated = true;
                IHardPatternUnit unit = __instance.GetComponent<IHardPatternUnit>();
                if (unit != null) unit.HardPatternActivated = true;
            }
        }

        private static float RecommendedHpFactor(UnitAvatar avatar, BossChallengeRules rules)
        {
            if (!Difficulty.Recommended) return 1f;

            int floor = MonsterScaler.FloorAt(avatar.transform.position);
            float manual = floor == 0 ? 1f : ModSettings.GetMonsterScalerHpMultiplier(floor, avatar.monsterType);
            float player = MonsterScaler.PlayerHpFactorOf(avatar);
            float applied = MonsterScaler.ExcludingPlayerBonus(manual, player);
            float target = MonsterScaler.ExcludingPlayerBonus(Difficulty.Effective(manual, rules.HpMultiplier), player);
            return applied > 0f ? target / applied : 1f;
        }

        [HarmonyPatch(typeof(UnitAvatar), "ApplyDamage")]
        private static class DamageBonus
        {
            [HarmonyPrefix]
            private static void Before(UnitAvatar __instance, DamageInstance __0, out float __state)
            {
                __state = float.NaN;
                if (!NetworkServer.active || !Difficulty.Recommended || __0 == null || __0.isSystemDamage || __0.damage <= 0f) return;

                UnitAvatar attacker = __0.origin as UnitAvatar;
                if (attacker == null) return;

                BossChallengeRules rules = For(attacker.GetType());
                if (rules == null || rules.Level < 1) return;

                float manual = MonsterScaler.ManualDamageMultiplier(__instance, attacker);
                float factor = Difficulty.Effective(manual, 1f + rules.DamageBonusPercent / 100f) / manual;
                if (factor <= 1f) return;

                __state = __0.damage;
                __0.damage *= factor;
            }

            [HarmonyFinalizer]
            private static Exception After(Exception __exception, DamageInstance __0, float __state)
            {
                if (__0 != null && !float.IsNaN(__state)) __0.damage = __state;
                return __exception;
            }
        }

        [HarmonyPatch(typeof(UnitAvatar), "ApplyDamage")]
        private static class HpStealPenalty
        {
            private const float PenaltyRate = 0.3f;

            private sealed class StealState
            {
                internal PlayerAvatar Attacker;
                internal int OriginalStat;
                internal float OriginalHpSteal;
            }

            [HarmonyPrefix]
            private static void Before(UnitAvatar __instance, DamageInstance __0, out StealState __state)
            {
                __state = null;
                if (!NetworkServer.active || __0 == null || __0.isSystemDamage) return;

                PlayerAvatar attacker = __0.origin as PlayerAvatar;
                if (attacker == null) return;

                BossChallengeRules rules = For(__instance.GetType());
                if (rules == null || rules.Level < 1) return;

                int stat = attacker.GetCustomStat(ECustomStat.HPSteal);
                if (stat <= 0 && __0.hpSteal <= 0f) return;

                __state = new StealState { Attacker = attacker, OriginalStat = stat, OriginalHpSteal = __0.hpSteal };
                if (stat > 0) attacker.AddCustomStat(ECustomStat.HPSteal, -Mathf.RoundToInt(stat * PenaltyRate));
                __0.hpSteal *= 1f - PenaltyRate;
            }

            [HarmonyFinalizer]
            private static Exception After(Exception __exception, DamageInstance __0, StealState __state)
            {
                if (__state != null)
                {
                    int current = __state.Attacker.GetCustomStat(ECustomStat.HPSteal);
                    __state.Attacker.AddCustomStat(ECustomStat.HPSteal, __state.OriginalStat - current);
                    if (__0 != null) __0.hpSteal = __state.OriginalHpSteal;
                }
                return __exception;
            }
        }
    }
}
