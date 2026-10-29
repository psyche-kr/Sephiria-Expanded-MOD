using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class Difficulty
    {
        private const string SapphireStat = "SAPPHIREBONUS";
        private static readonly Dictionary<PlayerAvatar, int> SapphireApplied = new Dictionary<PlayerAvatar, int>();
        private static readonly List<PlayerAvatar> SapphireStale = new List<PlayerAvatar>();

        internal const int ModeOff = 0;
        internal const int ModeDetail = 1;
        internal const int ModeGlobal = 2;

        private const float HpNormal = 2f;
        private const float HpHard = 4f;
        private const float SpawnScale = 2f;
        private const float SpeedScale = 1.2f;
        private const int SapphireNormalPercent = 300;
        private const int SapphireHardPercent = 500;

        internal static int BossLevel(int detailLevel)
        {
            switch (ModSettings.DifficultyMode)
            {
                case ModeDetail: return Mathf.Clamp(detailLevel, 0, 2);
                case ModeGlobal: return Mathf.Clamp(ModSettings.DifficultyGlobalLevel, 1, 2);
                default: return 0;
            }
        }

        internal static int FloorLevel(int floor)
        {
            return NetworkServer.active ? SettingFloorLevel(floor) : 0;
        }

        internal static float HpRate(int level)
        {
            return level >= 2 ? HpHard : level >= 1 ? HpNormal : 1f;
        }

        internal static float SpawnRate(int level)
        {
            return level >= 1 ? SpawnScale : 1f;
        }

        internal static float SpeedRate(int level)
        {
            return level >= 1 ? SpeedScale : 1f;
        }

        internal static float RecommendedFloorRate(int floor, System.Func<int, float> byLevel)
        {
            if (!Recommended) return 1f;
            if (floor >= 1) return byLevel(SettingFloorLevel(floor));

            float result = float.MaxValue;
            for (int i = 1; i <= 6; i++)
                result = Mathf.Min(result, byLevel(SettingFloorLevel(i)));

            return result;
        }

        internal static float RecommendedBossRate(System.Func<int, float> byLevel)
        {
            if (!Recommended) return 1f;

            float result = float.MaxValue;
            foreach (BossChallengeRules rules in BossChallenge.AllRules)
                result = Mathf.Min(result, byLevel(rules.SettingLevel));

            return result;
        }

        private static int SettingFloorLevel(int floor)
        {
            switch (ModSettings.DifficultyMode)
            {
                case ModeGlobal:
                    return Mathf.Clamp(ModSettings.DifficultyGlobalLevel, 1, 2);
                case ModeDetail:
                    if (floor == 1) return Mathf.Clamp(ModSettings.DifficultyFloor1Level, 0, 2);
                    if (floor == 2) return Mathf.Clamp(ModSettings.DifficultyFloor2Level, 0, 2);
                    if (floor == 3) return Mathf.Clamp(ModSettings.DifficultyFloor3Level, 0, 2);
                    if (floor == 4) return Mathf.Clamp(ModSettings.DifficultyFloor4Level, 0, 2);
                    if (floor == 5) return Mathf.Clamp(ModSettings.DifficultyFloor5Level, 0, 2);
                    if (floor == 6) return Mathf.Clamp(ModSettings.DifficultyFloor6Level, 0, 2);
                    return 0;
                default:
                    return 0;
            }
        }

        private static int SapphireLevel()
        {
            return ModSettings.DifficultyMode == ModeGlobal ? Mathf.Clamp(ModSettings.DifficultyGlobalLevel, 1, 2) : 0;
        }

        internal static void Tick()
        {
            if (!NetworkServer.active)
            {
                if (SapphireApplied.Count > 0) SapphireApplied.Clear();
                return;
            }

            SapphireStale.Clear();
            foreach (PlayerAvatar avatar in SapphireApplied.Keys)
                if (avatar == null) SapphireStale.Add(avatar);

            foreach (PlayerAvatar avatar in SapphireStale)
                SapphireApplied.Remove(avatar);

            int desiredSapphire = SapphireBonusPercent(SapphireLevel());

            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                PlayerAvatar avatar = spawner == null ? null : spawner.PlayerAvatar;
                if (avatar == null) continue;

                int current;
                SapphireApplied.TryGetValue(avatar, out current);
                if (current != desiredSapphire)
                {
                    avatar.AddCustomStat(SapphireStat, desiredSapphire - current);
                    SapphireApplied[avatar] = desiredSapphire;
                }
            }
        }

        internal static void Cleanup()
        {
            SapphireApplied.Clear();
        }

        private static bool IsScaledType(EMonsterType type)
        {
            return type == EMonsterType.Normal || type == EMonsterType.Miniboss;
        }

        internal static bool Recommended
        {
            get { return ModSettings.DifficultyRecommendedEnabled; }
        }

        internal static float Effective(float manual, float recommended)
        {
            return recommended > 1f ? Mathf.Max(manual, recommended) : manual;
        }

        internal static float MonsterHpMultiplier(int level, EMonsterType type)
        {
            return Recommended && IsScaledType(type) ? HpRate(level) : 1f;
        }

        internal static float MonsterSpeedMultiplier(int level, EMonsterType type)
        {
            return Recommended && IsScaledType(type) ? SpeedRate(level) : 1f;
        }

        internal static float SpawnMultiplier(int level)
        {
            return Recommended ? SpawnRate(level) : 1f;
        }

        private static int SapphireBonusPercent(int level)
        {
            if (level >= 2) return SapphireHardPercent;
            return level >= 1 ? SapphireNormalPercent : 0;
        }
    }
}
