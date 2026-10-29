using UnityEngine;

namespace SephiriaExpanded
{
    internal static class ModSettings
    {
        private const string Prefix = "SephiriaExpanded/";

        internal static int RandomEventMultiplier = 1;
        internal static bool CostumeFrameWatcherEnabled;
        internal static bool WeaponShineEnabled = true;
        internal static bool MoreDiceHostEnabled;
        internal static bool MoreDiceGuestEnabled;
        internal static bool FountainArtifactUnlockEnabled;
        internal static bool FountainArtifactShareEnabled;
        internal static bool MonsterScalerMiniBossSpawnEnabled;
        internal static bool MonsterScalerSpawnUsePerFloor;
        internal static bool MonsterScalerHpUsePerFloor;
        internal static bool MonsterScalerSpawnExpBalanceEnabled;
        internal static bool MonsterScalerDamageUsePerType;
        internal static bool MonsterScalerSpawnUsePerType;
        internal static bool MonsterScalerHpUsePerType;
        internal static bool MonsterScalerDamageUsePerFloor;
        internal static bool MonsterScalerSpeedUsePerFloor;
        internal static bool MonsterScalerSpeedUsePerType;
        internal static bool TradeMoneyShareEnabled;
        internal static bool FountainCapacityEnabled;

        internal static bool ChatConvenienceEnabled;
        internal static int ChatTimeMode;
        internal static bool ShopFavoriteEnabled;
        internal static bool EnchantMarkEnabled;
        internal static int JournalWeaponOutlineMode;
        internal static bool MorePresetEnabled;
        internal static bool RightClickHoldEnabled;
        internal static bool ChatConvenienceItemRarityColorEnabled;
        internal static bool Multiplayer16Enabled;
        internal static bool InstantLeafExpEnabled;
        internal static bool MysteryPotGuaranteedEnabled;
        internal static bool FirstArtifactLegendaryEnabled;
        internal static bool InventorySellEnabled;
        internal static bool SellStartItemEnabled;
        internal static bool LevelUpItemCountEnabled;
        internal static bool MinimapOpenEnabled;
        internal static bool MinimapOpenTeleportEnabled;
        internal static bool EnchantTableEnabled;
        internal static bool FirstAnvilEnabled;
        internal static bool MoreMapEnabled;
        internal static bool PotionShareEnabled;
        internal static bool FriendlyFireEnabled;
        internal static bool KillLogEnabled = true;
        internal static bool MoreMultiplayerInfoEnabled;
        internal static bool GuestReturnToTownEnabled;
        internal static int PingSoundMode;
        internal static bool PingIgnoreEnabled;
        internal static bool WalletShareEnabled;
        internal static bool WalletShareLeaf = true;
        internal static bool WalletShareDice = true;
        internal const int FriendlyFireStepMin = 1;
        internal const int FriendlyFireStepMax = 10;
        internal const int FriendlyFireStepDefault = 10;
        internal static int FriendlyFireMultiplierStep = FriendlyFireStepDefault;

        internal static float FriendlyFireMultiplier => StepToRate(FriendlyFireMultiplierStep);
        internal static bool BossHpTextEnabled;
        internal static int BossHpTextFormat;
        internal static bool BossAggroEnabled;
        internal static bool CutsceneSkipEnabled;
        internal static bool BloodFestivalEnabled;
        internal static bool AnytimeOpenInventoryEnabled;
        internal static bool MoreInventoryEnabled;
        internal static bool PlayerLevelCapEnabled;
        internal static bool MoreDummyHpEnabled;
        internal static bool WhichIsThisModFromEnabled;
        internal static bool FPSMeterEnabled;
        internal static bool CursorOutlineEnabled;
        internal static int CursorOutlineColor;
        internal static bool HiddenRoomFinderEnabled;
        internal static int MoleChieftainBossChallengeLevel;
        internal static int MoleBigBombBossChallengeLevel;
        internal static int RootDemonBossChallengeLevel;
        internal static int OinkKingBossChallengeLevel;
        internal static int PantherRogueKingBossChallengeLevel;
        internal static int MadArmadilloBossChallengeLevel;
        internal static int LibraryGuardBossChallengeLevel;
        internal static int AskardBossChallengeLevel;
        internal static int BirdDemonBossChallengeLevel;
        internal static int LizardDemonBossChallengeLevel;
        internal static int QBossChallengeLevel;
        internal static int QQBossChallengeLevel;
        internal static int DifficultyMode;
        internal static bool BossBreakEnabled = true;
        internal static bool DifficultyRecommendedEnabled;
        internal static int DifficultyGlobalLevel;
        internal static int DifficultyFloor1Level;
        internal static int DifficultyFloor2Level;
        internal static int DifficultyFloor3Level;
        internal static int DifficultyFloor4Level;
        internal static int DifficultyFloor5Level;
        internal static int DifficultyFloor6Level;
        internal static bool LegacyAlwaysRunEnabled;
        internal static bool AlwaysRunActive;
        internal static string AlwaysRunBindings = string.Empty;
        internal static int NameColorStart;
        internal static int NameColorA;
        internal static int NameColorB;
        internal static bool DamageMeterEnabled;
        internal static string DamageMeterBindings = string.Empty;
        internal const float DamageMeterDefaultX = 21f;
        internal const float DamageMeterDefaultY = -73f;
        internal static float DamageMeterX = DamageMeterDefaultX;
        internal static float DamageMeterY = DamageMeterDefaultY;
        internal const float DamageMeterDefaultScale = 0.7f;
        internal const int DamageMeterDefaultRows = 4;
        internal static float DamageMeterScale = DamageMeterDefaultScale;
        internal static int DamageMeterRows = DamageMeterDefaultRows;
        internal static bool DamageMeterMinimized;
        internal static bool DamageMeterMiniTotal;
        internal static int MoreInventoryLevelInterval;
        internal static int MoreInventorySlots;

        private const int RateScale = 10;
        internal const int MiniBossSpawnByTypeMin = RateScale;
        internal const int BossSpawnByTypeFixed = RateScale;
        internal const int RateDefault = RateScale;
        private const int RateMin = 1;
        private const int RateMax = 19;

        internal static float StepToRate(int step)
        {
            step = Mathf.Clamp(step, RateMin, RateMax);
            return step <= 9 ? step / 10f : step - 9;
        }

        private static int LegacyMultiplierToStep(int oldMultiplier)
        {
            return 9 + Mathf.Clamp(oldMultiplier, 1, 10);
        }

        internal static int MonsterScalerSpawnMultiplier = RateScale;
        internal static int MonsterScalerHpMultiplier = RateScale;
        internal static int MonsterScalerDamageMultiplier = RateScale;
        internal static int MonsterScalerNormalDamageMultiplier = RateScale;
        internal static int MonsterScalerMiniBossDamageMultiplier = RateScale;
        internal static int MonsterScalerBossDamageMultiplier = RateScale;
        internal static int MonsterScalerSpeedMultiplier = RateScale;
        internal static readonly int[] MonsterScalerSpawnMultiplierByType = { RateScale, RateScale, RateScale };
        internal static readonly int[] MonsterScalerHpMultiplierByType = { RateScale, RateScale, RateScale };
        internal static readonly int[] MonsterScalerSpeedMultiplierByType = { RateScale, RateScale, RateScale };
        internal static readonly int[] MonsterScalerDamageMultiplierByFloor = { RateScale, RateScale, RateScale, RateScale, RateScale, RateScale };
        internal static readonly int[] MonsterScalerSpawnMultiplierByFloor = { RateScale, RateScale, RateScale, RateScale, RateScale, RateScale };
        internal static readonly int[] MonsterScalerHpMultiplierByFloor = { RateScale, RateScale, RateScale, RateScale, RateScale, RateScale };
        internal static readonly int[] MonsterScalerSpeedMultiplierByFloor = { RateScale, RateScale, RateScale, RateScale, RateScale, RateScale };

        internal static int FountainCapacityBonus;
        internal const int StealMax = 100;
        internal static int DevHostHpSteal;
        internal static int DevHostMpSteal;
        internal static int DevGuestHpSteal;
        internal static int DevGuestMpSteal;

        internal static bool MonsterScalerSpawnEnabled
        {
            get => true;
            set { }
        }

        internal static bool MonsterScalerHpEnabled
        {
            get => true;
            set { }
        }

        internal static float GetMonsterScalerSpawnMultiplier(int floor, EMonsterType type = EMonsterType.Normal)
        {
            if (MonsterScalerSpawnUsePerType) return GetTypeMultiplier(MonsterScalerSpawnMultiplierByType, type);
            return GetMultiplier(MonsterScalerSpawnUsePerFloor, MonsterScalerSpawnMultiplier, MonsterScalerSpawnMultiplierByFloor, floor);
        }

        internal static float GetMonsterScalerHpMultiplier(int floor, EMonsterType type = EMonsterType.Normal)
        {
            if (MonsterScalerHpUsePerType) return GetTypeMultiplier(MonsterScalerHpMultiplierByType, type);
            return GetMultiplier(MonsterScalerHpUsePerFloor, MonsterScalerHpMultiplier, MonsterScalerHpMultiplierByFloor, floor);
        }

        internal static float GetMonsterScalerSpeedMultiplier(int floor, EMonsterType type = EMonsterType.Normal)
        {
            if (MonsterScalerSpeedUsePerType) return GetTypeMultiplier(MonsterScalerSpeedMultiplierByType, type);
            return GetMultiplier(MonsterScalerSpeedUsePerFloor, MonsterScalerSpeedMultiplier, MonsterScalerSpeedMultiplierByFloor, floor);
        }

        private static float GetMultiplier(bool perFloor, int all, int[] each, int floor)
        {
            if (!perFloor) return StepToRate(all);
            if (floor >= 1 && floor <= 6) return StepToRate(each[floor - 1]);
            return 1f;
        }

        internal static float GetMonsterScalerDamageMultiplier(EMonsterType type, int floor = 0)
        {
            if (MonsterScalerDamageUsePerFloor && floor >= 1 && floor <= 6)
                return StepToRate(MonsterScalerDamageMultiplierByFloor[floor - 1]);
            if (!MonsterScalerDamageUsePerType) return StepToRate(MonsterScalerDamageMultiplier);
            switch (type)
            {
                case EMonsterType.Normal: return StepToRate(MonsterScalerNormalDamageMultiplier);
                case EMonsterType.Miniboss: return StepToRate(MonsterScalerMiniBossDamageMultiplier);
                case EMonsterType.Boss: return StepToRate(MonsterScalerBossDamageMultiplier);
                default: return 1f;
            }
        }

        private static float GetTypeMultiplier(int[] values, EMonsterType type)
        {
            int index = type == EMonsterType.Miniboss ? 1 : type == EMonsterType.Boss ? 2 : 0;
            return StepToRate(values[index]);
        }

        internal static void ResetDevCheats()
        {
            MoreDiceHostEnabled = false;
            MoreDiceGuestEnabled = false;
            DevHostHpSteal = 0;
            DevHostMpSteal = 0;
            DevGuestHpSteal = 0;
            DevGuestMpSteal = 0;
        }

        internal static void ResetAll()
        {
            RandomEventMultiplier = 1;
            CostumeFrameWatcherEnabled = false;
            WeaponShineEnabled = true;
            MoreDiceHostEnabled = false;
            MoreDiceGuestEnabled = false;
            FountainArtifactUnlockEnabled = false;
            FountainArtifactShareEnabled = false;
            MonsterScalerMiniBossSpawnEnabled = false;
            MonsterScalerSpawnUsePerFloor = false;
            MonsterScalerHpUsePerFloor = false;
            MonsterScalerSpawnExpBalanceEnabled = false;
            MonsterScalerDamageUsePerType = false;
            MonsterScalerSpawnUsePerType = false;
            MonsterScalerHpUsePerType = false;
            MonsterScalerDamageUsePerFloor = false;
            MonsterScalerSpeedUsePerFloor = false;
            MonsterScalerSpeedUsePerType = false;
            TradeMoneyShareEnabled = false;
            FountainCapacityEnabled = false;
            ChatConvenienceEnabled = false;
            ChatTimeMode = 1;
            ShopFavoriteEnabled = false;
            EnchantMarkEnabled = false;
            JournalWeaponOutlineMode = 0;
            MorePresetEnabled = false;
            RightClickHoldEnabled = false;
            ChatConvenienceItemRarityColorEnabled = false;
            Multiplayer16Enabled = false;
            InstantLeafExpEnabled = false;
            MysteryPotGuaranteedEnabled = false;
            FirstArtifactLegendaryEnabled = false;
            InventorySellEnabled = false;
            SellStartItemEnabled = false;
            LevelUpItemCountEnabled = false;
            MinimapOpenEnabled = false;
            MinimapOpenTeleportEnabled = false;
            EnchantTableEnabled = false;
            FirstAnvilEnabled = false;
            MoreMapEnabled = false;
            PotionShareEnabled = false;
            FriendlyFireEnabled = false;
            KillLogEnabled = true;
            MoreMultiplayerInfoEnabled = false;
            GuestReturnToTownEnabled = false;
            PingSoundMode = 0;
            PingIgnoreEnabled = false;
            WalletShareEnabled = false;
            WalletShareLeaf = true;
            WalletShareDice = true;
            FriendlyFireMultiplierStep = FriendlyFireStepDefault;
            BossHpTextEnabled = false;
            BossHpTextFormat = 0;
            BossAggroEnabled = false;
            CutsceneSkipEnabled = false;
            BloodFestivalEnabled = false;
            AnytimeOpenInventoryEnabled = false;
            MoreInventoryEnabled = false;
            PlayerLevelCapEnabled = false;
            MoreDummyHpEnabled = false;
            WhichIsThisModFromEnabled = false;
            FPSMeterEnabled = false;
            CursorOutlineEnabled = false;
            CursorOutlineColor = 0;
            HiddenRoomFinderEnabled = false;
            MoleChieftainBossChallengeLevel = 0;
            MoleBigBombBossChallengeLevel = 0;
            RootDemonBossChallengeLevel = 0;
            OinkKingBossChallengeLevel = 0;
            PantherRogueKingBossChallengeLevel = 0;
            MadArmadilloBossChallengeLevel = 0;
            LibraryGuardBossChallengeLevel = 0;
            AskardBossChallengeLevel = 0;
            BirdDemonBossChallengeLevel = 0;
            LizardDemonBossChallengeLevel = 0;
            QBossChallengeLevel = 0;
            QQBossChallengeLevel = 0;
            DifficultyMode = 0;
            BossBreakEnabled = true;
            DifficultyRecommendedEnabled = false;
            ResetDifficultyDetail();
            LegacyAlwaysRunEnabled = false;
            AlwaysRunActive = false;
            AlwaysRunBindings = string.Empty;
            AlwaysRun.ResetBinding();
            NameColorStart = 0;
            NameColorA = 0;
            NameColorB = 0;
            DamageMeterEnabled = false;
            DamageMeterBindings = string.Empty;
            DamageMeterX = DamageMeterDefaultX;
            DamageMeterY = DamageMeterDefaultY;
            DamageMeterScale = DamageMeterDefaultScale;
            DamageMeterRows = DamageMeterDefaultRows;
            DamageMeterMinimized = false;
            DamageMeterMiniTotal = false;
            if (DamageMeter.Key != null) DamageMeter.Key.Reset();
            MoreInventoryLevelInterval = 1;
            MoreInventorySlots = 1;
            MonsterScalerSpawnMultiplier = RateScale;
            MonsterScalerHpMultiplier = RateScale;
            ResetMonsterScalerDamageMultipliers();
            ResetMonsterScalerSpeedMultipliers();
            FountainCapacityBonus = 0;
            ResetDevCheats();
            for (int i = 0; i < 6; i++)
            {
                MonsterScalerSpawnMultiplierByFloor[i] = RateScale;
                MonsterScalerHpMultiplierByFloor[i] = RateScale;
                MonsterScalerDamageMultiplierByFloor[i] = RateScale;
            }
            for (int i = 0; i < 3; i++) { MonsterScalerSpawnMultiplierByType[i] = RateScale; MonsterScalerHpMultiplierByType[i] = RateScale; }
            MorePlayerLevel.Restore();
            Save();
        }

        internal static void ResetMonsterScalerSpawnMultipliers()
        {
            MonsterScalerSpawnMultiplier = RateScale;

            for (int i = 0; i < MonsterScalerSpawnMultiplierByFloor.Length; i++)
                MonsterScalerSpawnMultiplierByFloor[i] = RateScale;
        }

        internal static void ResetDifficultyDetail()
        {
            DifficultyGlobalLevel = 1;
            DifficultyFloor1Level = 0;
            DifficultyFloor2Level = 0;
            DifficultyFloor3Level = 0;
            DifficultyFloor4Level = 0;
            DifficultyFloor5Level = 0;
            DifficultyFloor6Level = 0;
            MoleChieftainBossChallengeLevel = 0;
            MoleBigBombBossChallengeLevel = 0;
            RootDemonBossChallengeLevel = 0;
            OinkKingBossChallengeLevel = 0;
            PantherRogueKingBossChallengeLevel = 0;
            MadArmadilloBossChallengeLevel = 0;
            LibraryGuardBossChallengeLevel = 0;
            AskardBossChallengeLevel = 0;
            BirdDemonBossChallengeLevel = 0;
            LizardDemonBossChallengeLevel = 0;
            QBossChallengeLevel = 0;
            QQBossChallengeLevel = 0;
        }

        internal static void ResetMonsterScalerHpMultipliers()
        {
            MonsterScalerHpMultiplier = RateScale;

            for (int i = 0; i < MonsterScalerHpMultiplierByFloor.Length; i++)
                MonsterScalerHpMultiplierByFloor[i] = RateScale;
        }

        internal static void ResetMonsterScalerDamageMultipliers()
        {
            MonsterScalerDamageMultiplier = RateScale;
            MonsterScalerNormalDamageMultiplier = RateScale;
            MonsterScalerMiniBossDamageMultiplier = RateScale;
            MonsterScalerBossDamageMultiplier = RateScale;
        }

        internal static void ResetMonsterScalerSpeedMultipliers()
        {
            MonsterScalerSpeedMultiplier = RateScale;

            for (int i = 0; i < MonsterScalerSpeedMultiplierByFloor.Length; i++)
                MonsterScalerSpeedMultiplierByFloor[i] = RateScale;
            for (int i = 0; i < MonsterScalerSpeedMultiplierByType.Length; i++)
                MonsterScalerSpeedMultiplierByType[i] = RateScale;
        }

        internal static void Load()
        {
            if (PlayerPrefs.HasKey(Prefix + nameof(RandomEventMultiplier)))
                RandomEventMultiplier = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + nameof(RandomEventMultiplier), 1), 1, 10);
            else
                RandomEventMultiplier = GetBool("RandomEventEnabled", false, "RandomEvent") ? 2 : 1;
            CostumeFrameWatcherEnabled = GetBool(nameof(CostumeFrameWatcherEnabled), false, "Root60Effect");
            WeaponShineEnabled = GetBool(nameof(WeaponShineEnabled), true);
            FountainArtifactUnlockEnabled = GetBool(nameof(FountainArtifactUnlockEnabled), false, "FountainArtifactUnlock");
            FountainArtifactShareEnabled = GetBool(nameof(FountainArtifactShareEnabled), false, "FountainArtifactShare");
            MonsterScalerMiniBossSpawnEnabled = GetBool(nameof(MonsterScalerMiniBossSpawnEnabled), false, "MiniBossSpawn");
            MonsterScalerSpawnUsePerFloor = GetBool(nameof(MonsterScalerSpawnUsePerFloor), false, "SpawnPerFloor");
            MonsterScalerHpUsePerFloor = GetBool(nameof(MonsterScalerHpUsePerFloor), false, "HpPerFloor");
            MonsterScalerSpawnExpBalanceEnabled = GetBool(nameof(MonsterScalerSpawnExpBalanceEnabled), false);
            MonsterScalerDamageUsePerType = GetBool(nameof(MonsterScalerDamageUsePerType), false);
            MonsterScalerSpawnUsePerType = GetBool(nameof(MonsterScalerSpawnUsePerType), false);
            MonsterScalerHpUsePerType = GetBool(nameof(MonsterScalerHpUsePerType), false);
            MonsterScalerDamageUsePerFloor = GetBool(nameof(MonsterScalerDamageUsePerFloor), false);
            MonsterScalerSpeedUsePerFloor = GetBool(nameof(MonsterScalerSpeedUsePerFloor), false);
            MonsterScalerSpeedUsePerType = GetBool(nameof(MonsterScalerSpeedUsePerType), false);
            TradeMoneyShareEnabled = GetBool(nameof(TradeMoneyShareEnabled), false, "LuckyMedalShare");
            FountainCapacityEnabled = GetBool(nameof(FountainCapacityEnabled), false, "FountainEnabled");

            ChatConvenienceEnabled = GetBool(nameof(ChatConvenienceEnabled), false, "ChatPersistence");
            ChatTimeMode = GetInt(nameof(ChatTimeMode), 1, 0, 1, string.Empty);
            ShopFavoriteEnabled = GetBool(nameof(ShopFavoriteEnabled), false);
            EnchantMarkEnabled = GetBool(nameof(EnchantMarkEnabled), false);
            JournalWeaponOutlineMode = GetInt(nameof(JournalWeaponOutlineMode), 0, 0, 3, "JournalWeaponOutline");
            MorePresetEnabled = GetBool(nameof(MorePresetEnabled), false);
            RightClickHoldEnabled = GetBool(nameof(RightClickHoldEnabled), false);
            ChatConvenienceItemRarityColorEnabled = GetBool(nameof(ChatConvenienceItemRarityColorEnabled), false, "ChatItemRarityColor");
            Multiplayer16Enabled = GetBool(nameof(Multiplayer16Enabled), false, "Multiplayer16");
            InstantLeafExpEnabled = GetBool(nameof(InstantLeafExpEnabled), false, "InstantLeafExp");
            MysteryPotGuaranteedEnabled = GetBool(nameof(MysteryPotGuaranteedEnabled), false, "MysteryPotGuaranteed");
            FirstArtifactLegendaryEnabled = GetBool(nameof(FirstArtifactLegendaryEnabled), false, "FirstArtifactLegendary");
            InventorySellEnabled = GetBool(nameof(InventorySellEnabled), false, "InventorySell");
            SellStartItemEnabled = GetBool(nameof(SellStartItemEnabled), false, "SellStartItem");
            LevelUpItemCountEnabled = GetBool(nameof(LevelUpItemCountEnabled), false, "LevelUpItemCount");
            MinimapOpenEnabled = GetBool(nameof(MinimapOpenEnabled), false, "MinimapRevealEnabled", "MinimapReveal");
            MinimapOpenTeleportEnabled = GetBool(nameof(MinimapOpenTeleportEnabled), false, "MinimapOpenTeleport");
            EnchantTableEnabled = GetBool(nameof(EnchantTableEnabled), false, "EnchantMin3");
            FirstAnvilEnabled = GetBool(nameof(FirstAnvilEnabled), false, "FirstAnvilAllWeaponsEnabled", "FirstAnvilAllWeapons");
            MoreMapEnabled = GetBool(nameof(MoreMapEnabled), false);
            PotionShareEnabled = GetBool(nameof(PotionShareEnabled), false);
            FriendlyFireEnabled = GetBool(nameof(FriendlyFireEnabled), false);
            KillLogEnabled = GetBool(nameof(KillLogEnabled), true);
            MoreMultiplayerInfoEnabled = GetBool(nameof(MoreMultiplayerInfoEnabled), false);
            GuestReturnToTownEnabled = GetBool(nameof(GuestReturnToTownEnabled), false);
            PingSoundMode = GetInt(nameof(PingSoundMode), 0, 0, PingSound.Labels.Length - 1, string.Empty);
            PingIgnoreEnabled = GetBool(nameof(PingIgnoreEnabled), false);
            WalletShareEnabled = GetBool(nameof(WalletShareEnabled), false);
            WalletShareLeaf = GetBool(nameof(WalletShareLeaf), true);
            WalletShareDice = GetBool(nameof(WalletShareDice), true);
            FriendlyFireMultiplierStep = GetInt(nameof(FriendlyFireMultiplierStep), FriendlyFireStepDefault, FriendlyFireStepMin, FriendlyFireStepMax, string.Empty);
            BossHpTextEnabled = GetBool(nameof(BossHpTextEnabled), false);
            BossHpTextFormat = GetInt(nameof(BossHpTextFormat), 0, 0, 4, string.Empty);
            BossAggroEnabled = GetBool(nameof(BossAggroEnabled), false);
            CutsceneSkipEnabled = GetBool(nameof(CutsceneSkipEnabled), false);
            BloodFestivalEnabled = GetBool(nameof(BloodFestivalEnabled), false);
            AnytimeOpenInventoryEnabled = GetBool(nameof(AnytimeOpenInventoryEnabled), false);
            MoreInventoryEnabled = GetBool(nameof(MoreInventoryEnabled), false);
            PlayerLevelCapEnabled = GetBool(nameof(PlayerLevelCapEnabled), false);
            MoreDummyHpEnabled = GetBool(nameof(MoreDummyHpEnabled), false);
            WhichIsThisModFromEnabled = GetBool(nameof(WhichIsThisModFromEnabled), false);
            FPSMeterEnabled = GetBool(nameof(FPSMeterEnabled), false);
            CursorOutlineEnabled = GetBool(nameof(CursorOutlineEnabled), false);
            CursorOutlineColor = GetInt(nameof(CursorOutlineColor), 0, 0, 9, string.Empty);
            HiddenRoomFinderEnabled = GetBool(nameof(HiddenRoomFinderEnabled), false);
            int legacyBossChallengeLevel = GetInt("BossChallengeLevel", 0, 0, 2, string.Empty);
            MoleChieftainBossChallengeLevel = GetInt(nameof(MoleChieftainBossChallengeLevel), legacyBossChallengeLevel, 0, 2, string.Empty);
            MoleBigBombBossChallengeLevel = GetInt(nameof(MoleBigBombBossChallengeLevel), legacyBossChallengeLevel, 0, 2, string.Empty);
            RootDemonBossChallengeLevel = GetInt(nameof(RootDemonBossChallengeLevel), 0, 0, 2, string.Empty);
            OinkKingBossChallengeLevel = GetInt(nameof(OinkKingBossChallengeLevel), 0, 0, 2, string.Empty);
            PantherRogueKingBossChallengeLevel = GetInt(nameof(PantherRogueKingBossChallengeLevel), 0, 0, 2, string.Empty);
            MadArmadilloBossChallengeLevel = GetInt(nameof(MadArmadilloBossChallengeLevel), 0, 0, 2, string.Empty);
            LibraryGuardBossChallengeLevel = GetInt(nameof(LibraryGuardBossChallengeLevel), 0, 0, 2, string.Empty);
            AskardBossChallengeLevel = GetInt(nameof(AskardBossChallengeLevel), 0, 0, 2, string.Empty);
            BirdDemonBossChallengeLevel = GetInt(nameof(BirdDemonBossChallengeLevel), 0, 0, 2, string.Empty);
            LizardDemonBossChallengeLevel = GetInt(nameof(LizardDemonBossChallengeLevel), 0, 0, 2, string.Empty);
            QBossChallengeLevel = GetInt(nameof(QBossChallengeLevel), 0, 0, 2, string.Empty);
            QQBossChallengeLevel = GetInt(nameof(QQBossChallengeLevel), 0, 0, 2, string.Empty);
            bool anyBossLevel = MoleChieftainBossChallengeLevel > 0 || MoleBigBombBossChallengeLevel > 0 || RootDemonBossChallengeLevel > 0 || OinkKingBossChallengeLevel > 0 || PantherRogueKingBossChallengeLevel > 0 || MadArmadilloBossChallengeLevel > 0 ||
                                 LibraryGuardBossChallengeLevel > 0 || AskardBossChallengeLevel > 0 || BirdDemonBossChallengeLevel > 0 || LizardDemonBossChallengeLevel > 0 || QBossChallengeLevel > 0 || QQBossChallengeLevel > 0;
            DifficultyMode = GetInt(nameof(DifficultyMode), anyBossLevel ? 1 : 0, 0, 2, string.Empty);
            BossBreakEnabled = GetBool(nameof(BossBreakEnabled), true);
            DifficultyRecommendedEnabled = GetBool(nameof(DifficultyRecommendedEnabled), false);
            DifficultyGlobalLevel = GetInt(nameof(DifficultyGlobalLevel), 1, 1, 2, string.Empty);
            DifficultyFloor1Level = GetInt(nameof(DifficultyFloor1Level), 0, 0, 2, string.Empty);
            DifficultyFloor2Level = GetInt(nameof(DifficultyFloor2Level), 0, 0, 2, string.Empty);
            DifficultyFloor3Level = GetInt(nameof(DifficultyFloor3Level), 0, 0, 2, string.Empty);
            DifficultyFloor4Level = GetInt(nameof(DifficultyFloor4Level), 0, 0, 2, string.Empty);
            DifficultyFloor5Level = GetInt(nameof(DifficultyFloor5Level), 0, 0, 2, string.Empty);
            DifficultyFloor6Level = GetInt(nameof(DifficultyFloor6Level), 0, 0, 2, string.Empty);
            LegacyAlwaysRunEnabled = GetBool("AlwaysRunEnabled", false);
            AlwaysRunActive = GetBool(nameof(AlwaysRunActive), false);
            AlwaysRunBindings = PlayerPrefs.GetString(Prefix + nameof(AlwaysRunBindings), string.Empty);
            NameColorStart = GetInt(nameof(NameColorStart), 0, 0, NameColor.PaletteSize, string.Empty);
            NameColorA = GetInt(nameof(NameColorA), 0, 0, NameColor.PaletteSize, string.Empty);
            NameColorB = GetInt(nameof(NameColorB), 0, 0, NameColor.PaletteSize, string.Empty);
            DamageMeterEnabled = GetBool(nameof(DamageMeterEnabled), false);
            DamageMeterBindings = PlayerPrefs.GetString(Prefix + nameof(DamageMeterBindings), string.Empty);
            DamageMeterX = PlayerPrefs.GetFloat(Prefix + nameof(DamageMeterX), DamageMeterDefaultX);
            DamageMeterY = PlayerPrefs.GetFloat(Prefix + nameof(DamageMeterY), DamageMeterDefaultY);
            DamageMeterScale = PlayerPrefs.GetFloat(Prefix + nameof(DamageMeterScale), DamageMeterDefaultScale);
            DamageMeterRows = PlayerPrefs.GetInt(Prefix + nameof(DamageMeterRows), DamageMeterDefaultRows);
            DamageMeterMinimized = GetBool(nameof(DamageMeterMinimized), false);
            DamageMeterMiniTotal = GetBool(nameof(DamageMeterMiniTotal), false);

            MonsterScalerSpawnMultiplier = GetRateInt(nameof(MonsterScalerSpawnMultiplier), nameof(MonsterScalerSpawnMultiplier), "SpawnAll");
            MonsterScalerHpMultiplier = GetRateInt(nameof(MonsterScalerHpMultiplier), nameof(MonsterScalerHpMultiplier), "HpAll");
            MonsterScalerDamageMultiplier = GetRateInt(nameof(MonsterScalerDamageMultiplier), nameof(MonsterScalerDamageMultiplier), "DamageAll");
            MonsterScalerNormalDamageMultiplier = GetRateInt(nameof(MonsterScalerNormalDamageMultiplier), nameof(MonsterScalerNormalDamageMultiplier), "DamageNormal");
            MonsterScalerMiniBossDamageMultiplier = GetRateInt(nameof(MonsterScalerMiniBossDamageMultiplier), nameof(MonsterScalerMiniBossDamageMultiplier), "DamageMiniBoss");
            MonsterScalerBossDamageMultiplier = GetRateInt(nameof(MonsterScalerBossDamageMultiplier), nameof(MonsterScalerBossDamageMultiplier), "DamageBoss");
            MonsterScalerSpeedMultiplier = GetRateInt(nameof(MonsterScalerSpeedMultiplier));
            for (int i = 0; i < 3; i++)
            {
                MonsterScalerSpawnMultiplierByType[i] = GetRateInt(nameof(MonsterScalerSpawnMultiplierByType) + i, nameof(MonsterScalerSpawnMultiplierByType) + i, "SpawnType" + i);
                if (i == 1) MonsterScalerSpawnMultiplierByType[i] = UnityEngine.Mathf.Max(MiniBossSpawnByTypeMin, MonsterScalerSpawnMultiplierByType[i]);
                else if (i == 2) MonsterScalerSpawnMultiplierByType[i] = BossSpawnByTypeFixed;
                MonsterScalerHpMultiplierByType[i] = GetRateInt(nameof(MonsterScalerHpMultiplierByType) + i, nameof(MonsterScalerHpMultiplierByType) + i, "HpType" + i);
                MonsterScalerSpeedMultiplierByType[i] = GetRateInt(nameof(MonsterScalerSpeedMultiplierByType) + i);
            }
            FountainCapacityBonus = GetInt(nameof(FountainCapacityBonus), 0, 0, 30, "FountainCapacity");
            MoreInventoryLevelInterval = GetInt(nameof(MoreInventoryLevelInterval), 1, 1, 10, "MoreInventoryLevelInterval");
            MoreInventorySlots = GetInt(nameof(MoreInventorySlots), 1, 1, 10, "MoreInventorySlots");

            for (int i = 0; i < 6; i++)
            {
                MonsterScalerSpawnMultiplierByFloor[i] = GetRateInt(nameof(MonsterScalerSpawnMultiplierByFloor) + (i + 1), nameof(MonsterScalerSpawnMultiplierByFloor) + (i + 1), "SpawnFloor" + (i + 1));
                MonsterScalerHpMultiplierByFloor[i] = GetRateInt(nameof(MonsterScalerHpMultiplierByFloor) + (i + 1), nameof(MonsterScalerHpMultiplierByFloor) + (i + 1), "HpFloor" + (i + 1));
                MonsterScalerDamageMultiplierByFloor[i] = GetRateInt(nameof(MonsterScalerDamageMultiplierByFloor) + (i + 1), nameof(MonsterScalerDamageMultiplierByFloor) + (i + 1), "DamageFloor" + (i + 1));
                MonsterScalerSpeedMultiplierByFloor[i] = GetRateInt(nameof(MonsterScalerSpeedMultiplierByFloor) + (i + 1));
            }
        }

        internal static void Save()
        {
            SetInt(nameof(RandomEventMultiplier), RandomEventMultiplier);
            SetBool(nameof(CostumeFrameWatcherEnabled), CostumeFrameWatcherEnabled);
            SetBool(nameof(WeaponShineEnabled), WeaponShineEnabled);
            SetBool(nameof(FountainArtifactUnlockEnabled), FountainArtifactUnlockEnabled);
            SetBool(nameof(FountainArtifactShareEnabled), FountainArtifactShareEnabled);
            SetBool(nameof(MonsterScalerMiniBossSpawnEnabled), MonsterScalerMiniBossSpawnEnabled);
            SetBool(nameof(MonsterScalerSpawnUsePerFloor), MonsterScalerSpawnUsePerFloor);
            SetBool(nameof(MonsterScalerHpUsePerFloor), MonsterScalerHpUsePerFloor);
            SetBool(nameof(MonsterScalerSpawnExpBalanceEnabled), MonsterScalerSpawnExpBalanceEnabled);
            SetBool(nameof(MonsterScalerDamageUsePerType), MonsterScalerDamageUsePerType);
            SetBool(nameof(MonsterScalerSpawnUsePerType), MonsterScalerSpawnUsePerType);
            SetBool(nameof(MonsterScalerHpUsePerType), MonsterScalerHpUsePerType);
            SetBool(nameof(MonsterScalerDamageUsePerFloor), MonsterScalerDamageUsePerFloor);
            SetBool(nameof(TradeMoneyShareEnabled), TradeMoneyShareEnabled);
            SetBool(nameof(FountainCapacityEnabled), FountainCapacityEnabled);

            SetBool(nameof(ChatConvenienceEnabled), ChatConvenienceEnabled);
            SetInt(nameof(ChatTimeMode), ChatTimeMode);
            SetBool(nameof(ShopFavoriteEnabled), ShopFavoriteEnabled);
            SetBool(nameof(EnchantMarkEnabled), EnchantMarkEnabled);
            SetInt(nameof(JournalWeaponOutlineMode), JournalWeaponOutlineMode);
            SetBool(nameof(MorePresetEnabled), MorePresetEnabled);
            SetBool(nameof(RightClickHoldEnabled), RightClickHoldEnabled);
            SetBool(nameof(ChatConvenienceItemRarityColorEnabled), ChatConvenienceItemRarityColorEnabled);
            SetBool(nameof(Multiplayer16Enabled), Multiplayer16Enabled);
            SetBool(nameof(InstantLeafExpEnabled), InstantLeafExpEnabled);
            SetBool(nameof(MysteryPotGuaranteedEnabled), MysteryPotGuaranteedEnabled);
            SetBool(nameof(FirstArtifactLegendaryEnabled), FirstArtifactLegendaryEnabled);
            SetBool(nameof(InventorySellEnabled), InventorySellEnabled);
            SetBool(nameof(SellStartItemEnabled), SellStartItemEnabled);
            SetBool(nameof(LevelUpItemCountEnabled), LevelUpItemCountEnabled);
            SetBool(nameof(MinimapOpenEnabled), MinimapOpenEnabled);
            SetBool(nameof(MinimapOpenTeleportEnabled), MinimapOpenTeleportEnabled);
            SetBool(nameof(EnchantTableEnabled), EnchantTableEnabled);
            SetBool(nameof(FirstAnvilEnabled), FirstAnvilEnabled);
            SetBool(nameof(MoreMapEnabled), MoreMapEnabled);
            SetBool(nameof(PotionShareEnabled), PotionShareEnabled);
            SetBool(nameof(FriendlyFireEnabled), FriendlyFireEnabled);
            SetBool(nameof(KillLogEnabled), KillLogEnabled);
            SetBool(nameof(MoreMultiplayerInfoEnabled), MoreMultiplayerInfoEnabled);
            SetBool(nameof(GuestReturnToTownEnabled), GuestReturnToTownEnabled);
            SetInt(nameof(PingSoundMode), PingSoundMode);
            SetBool(nameof(PingIgnoreEnabled), PingIgnoreEnabled);
            SetBool(nameof(WalletShareEnabled), WalletShareEnabled);
            SetBool(nameof(WalletShareLeaf), WalletShareLeaf);
            SetBool(nameof(WalletShareDice), WalletShareDice);
            SetInt(nameof(FriendlyFireMultiplierStep), FriendlyFireMultiplierStep);
            SetBool(nameof(BossHpTextEnabled), BossHpTextEnabled);
            SetInt(nameof(BossHpTextFormat), BossHpTextFormat);
            SetBool(nameof(BossAggroEnabled), BossAggroEnabled);
            SetBool(nameof(CutsceneSkipEnabled), CutsceneSkipEnabled);
            SetBool(nameof(BloodFestivalEnabled), BloodFestivalEnabled);
            SetBool(nameof(AnytimeOpenInventoryEnabled), AnytimeOpenInventoryEnabled);
            SetBool(nameof(MoreInventoryEnabled), MoreInventoryEnabled);
            SetBool(nameof(PlayerLevelCapEnabled), PlayerLevelCapEnabled);
            SetBool(nameof(MoreDummyHpEnabled), MoreDummyHpEnabled);
            SetBool(nameof(WhichIsThisModFromEnabled), WhichIsThisModFromEnabled);
            SetBool(nameof(FPSMeterEnabled), FPSMeterEnabled);
            SetBool(nameof(CursorOutlineEnabled), CursorOutlineEnabled);
            SetInt(nameof(CursorOutlineColor), CursorOutlineColor);
            SetBool(nameof(HiddenRoomFinderEnabled), HiddenRoomFinderEnabled);
            SetInt(nameof(MoleChieftainBossChallengeLevel), MoleChieftainBossChallengeLevel);
            SetInt(nameof(MoleBigBombBossChallengeLevel), MoleBigBombBossChallengeLevel);
            SetInt(nameof(RootDemonBossChallengeLevel), RootDemonBossChallengeLevel);
            SetInt(nameof(OinkKingBossChallengeLevel), OinkKingBossChallengeLevel);
            SetInt(nameof(PantherRogueKingBossChallengeLevel), PantherRogueKingBossChallengeLevel);
            SetInt(nameof(MadArmadilloBossChallengeLevel), MadArmadilloBossChallengeLevel);
            SetInt(nameof(LibraryGuardBossChallengeLevel), LibraryGuardBossChallengeLevel);
            SetInt(nameof(AskardBossChallengeLevel), AskardBossChallengeLevel);
            SetInt(nameof(BirdDemonBossChallengeLevel), BirdDemonBossChallengeLevel);
            SetInt(nameof(LizardDemonBossChallengeLevel), LizardDemonBossChallengeLevel);
            SetInt(nameof(QBossChallengeLevel), QBossChallengeLevel);
            SetInt(nameof(QQBossChallengeLevel), QQBossChallengeLevel);
            SetInt(nameof(DifficultyMode), DifficultyMode);
            SetBool(nameof(BossBreakEnabled), BossBreakEnabled);
            SetBool(nameof(DifficultyRecommendedEnabled), DifficultyRecommendedEnabled);
            SetInt(nameof(DifficultyGlobalLevel), DifficultyGlobalLevel);
            SetInt(nameof(DifficultyFloor1Level), DifficultyFloor1Level);
            SetInt(nameof(DifficultyFloor2Level), DifficultyFloor2Level);
            SetInt(nameof(DifficultyFloor3Level), DifficultyFloor3Level);
            SetInt(nameof(DifficultyFloor4Level), DifficultyFloor4Level);
            SetInt(nameof(DifficultyFloor5Level), DifficultyFloor5Level);
            SetInt(nameof(DifficultyFloor6Level), DifficultyFloor6Level);
            SetBool(nameof(AlwaysRunActive), AlwaysRunActive);
            PlayerPrefs.SetString(Prefix + nameof(AlwaysRunBindings), AlwaysRunBindings);
            SetInt(nameof(NameColorStart), NameColorStart);
            SetInt(nameof(NameColorA), NameColorA);
            SetInt(nameof(NameColorB), NameColorB);
            SetBool(nameof(DamageMeterEnabled), DamageMeterEnabled);
            PlayerPrefs.SetString(Prefix + nameof(DamageMeterBindings), DamageMeterBindings);
            PlayerPrefs.SetFloat(Prefix + nameof(DamageMeterX), DamageMeterX);
            PlayerPrefs.SetFloat(Prefix + nameof(DamageMeterY), DamageMeterY);
            PlayerPrefs.SetFloat(Prefix + nameof(DamageMeterScale), DamageMeterScale);
            PlayerPrefs.SetInt(Prefix + nameof(DamageMeterRows), DamageMeterRows);
            SetBool(nameof(DamageMeterMinimized), DamageMeterMinimized);
            SetBool(nameof(DamageMeterMiniTotal), DamageMeterMiniTotal);

            SetRateInt(nameof(MonsterScalerSpawnMultiplier), MonsterScalerSpawnMultiplier);
            SetRateInt(nameof(MonsterScalerHpMultiplier), MonsterScalerHpMultiplier);
            SetRateInt(nameof(MonsterScalerDamageMultiplier), MonsterScalerDamageMultiplier);
            SetRateInt(nameof(MonsterScalerNormalDamageMultiplier), MonsterScalerNormalDamageMultiplier);
            SetRateInt(nameof(MonsterScalerMiniBossDamageMultiplier), MonsterScalerMiniBossDamageMultiplier);
            SetRateInt(nameof(MonsterScalerBossDamageMultiplier), MonsterScalerBossDamageMultiplier);
            SetRateInt(nameof(MonsterScalerSpeedMultiplier), MonsterScalerSpeedMultiplier);
            SetInt(nameof(FountainCapacityBonus), FountainCapacityBonus);
            SetInt(nameof(MoreInventoryLevelInterval), MoreInventoryLevelInterval);
            SetInt(nameof(MoreInventorySlots), MoreInventorySlots);

            for (int i = 0; i < 6; i++)
            {
                SetRateInt(nameof(MonsterScalerSpawnMultiplierByFloor) + (i + 1), MonsterScalerSpawnMultiplierByFloor[i]);
                SetRateInt(nameof(MonsterScalerHpMultiplierByFloor) + (i + 1), MonsterScalerHpMultiplierByFloor[i]);
                SetRateInt(nameof(MonsterScalerDamageMultiplierByFloor) + (i + 1), MonsterScalerDamageMultiplierByFloor[i]);
                SetRateInt(nameof(MonsterScalerSpeedMultiplierByFloor) + (i + 1), MonsterScalerSpeedMultiplierByFloor[i]);
            }
            for (int i = 0; i < 3; i++)
            {
                SetRateInt(nameof(MonsterScalerSpawnMultiplierByType) + i, MonsterScalerSpawnMultiplierByType[i]);
                SetRateInt(nameof(MonsterScalerHpMultiplierByType) + i, MonsterScalerHpMultiplierByType[i]);
                SetRateInt(nameof(MonsterScalerSpeedMultiplierByType) + i, MonsterScalerSpeedMultiplierByType[i]);
            }

            PlayerPrefs.Save();
        }

        private static bool GetBool(string key, bool fallback, params string[] legacyKeys)
        {
            return GetStoredInt(key, fallback ? 1 : 0, legacyKeys) != 0;
        }

        private static int GetInt(string key, int fallback, int min, int max, string legacyKey)
        {
            return Mathf.Clamp(GetStoredInt(key, fallback, legacyKey), min, max);
        }

        private static int GetStoredInt(string key, int fallback, params string[] legacyKeys)
        {
            if (PlayerPrefs.HasKey(Prefix + key))
                return PlayerPrefs.GetInt(Prefix + key, fallback);

            foreach (string legacyKey in legacyKeys)
                if (PlayerPrefs.HasKey(Prefix + legacyKey))
                    return PlayerPrefs.GetInt(Prefix + legacyKey, fallback);

            return fallback;
        }

        private static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
        }

        private static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(Prefix + key, value);
        }

        private static int GetRateInt(string key, params string[] legacyIntKeys)
        {
            string rateKey = key + "Rate";
            if (PlayerPrefs.HasKey(Prefix + rateKey))
                return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + rateKey, RateScale), RateMin, RateMax);

            foreach (string legacyKey in legacyIntKeys)
                if (PlayerPrefs.HasKey(Prefix + legacyKey))
                    return LegacyMultiplierToStep(PlayerPrefs.GetInt(Prefix + legacyKey, 1));

            return RateScale;
        }

        private static void SetRateInt(string key, int value)
        {
            PlayerPrefs.SetInt(Prefix + key + "Rate", Mathf.Clamp(value, RateMin, RateMax));
        }
    }
}
