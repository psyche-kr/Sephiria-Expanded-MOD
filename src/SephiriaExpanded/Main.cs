using HarmonyLib;
using UnityEngine;

namespace SephiriaExpanded
{
    public sealed class Main : HorayModBase
    {
        internal const string HarmonyId = "com.sephiria.expanded";

        private static bool initialized;
        private Harmony harmony;
        private GameObject runtimeObject;
        private bool ownsRuntime;

        protected override void OnModLoaded()
        {
            if (initialized) return;

            initialized = true;
            ownsRuntime = true;

            try
            {
                ModSettings.Load();
                AlwaysRun.Initialize();
                DamageMeter.Initialize();
                MoreMapCompatibility.Initialize();
                CutsceneSkip.Initialize();

                harmony = new Harmony(HarmonyId);
                harmony.PatchAll(typeof(Main).Assembly);

                AutoUpdate.Begin();

                runtimeObject = new GameObject("SephiriaExpanded.Runtime");
                Object.DontDestroyOnLoad(runtimeObject);

                runtimeObject.AddComponent<TradeMoneyShare>();
                runtimeObject.AddComponent<CostumeFrameWatcher>();
                runtimeObject.AddComponent<ShopFavorite>();
                runtimeObject.AddComponent<JournalWeaponOutline>();
                runtimeObject.AddComponent<DeveloperMode>();
                runtimeObject.AddComponent<ExpandedRuntime>();

                Debug.Log("[Sephiria Expanded] 통합 모드가 로드되었습니다.");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                Cleanup();
                throw;
            }
        }

        protected override void OnModUnloaded()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (!ownsRuntime) return;

            try
            {
                AlwaysRun.Cleanup();
                DamageMeter.Cleanup();
                MoreDummyHp.Cleanup();
                WhichIsThisModFrom.Cleanup();
                FPSMeter.Cleanup();
                CursorOutline.Cleanup();
                BossChallenge.Cleanup();
                Difficulty.Cleanup();
                MinimapOpen.Cleanup();
                MoreMapCompatibility.Cleanup();
                CutsceneSkip.Cleanup();
                EnchantMark.Cleanup();
                MorePlayerLevel.Restore();
                DevPanel.Cleanup();
                ExpandedSettingsPanel.Cleanup();
                LevelUpItemCount.Cleanup();

                if (runtimeObject != null) Object.DestroyImmediate(runtimeObject);

                FountainCapacity.Cleanup();
                ModSettings.Save();
            }
            finally
            {
                harmony?.UnpatchAll(HarmonyId);

                harmony = null;
                runtimeObject = null;
                ownsRuntime = false;
                initialized = false;
            }
        }
    }

    public sealed class ExpandedRuntime : MonoBehaviour
    {
        private float refreshTimer;

        private void Update()
        {
            ExpandedSettingsPanel.Tick();
            DevPanel.Tick();
            MoreMapCompatibility.Tick();
            CutsceneSkip.Tick();
            LevelUpItemCount.Tick();
            EnchantMark.Tick();
            AlwaysRun.Tick();
            DamageMeter.Tick();
            FPSMeter.Tick();
            HiddenRoomFinder.Sync(gameObject);
            BossChallenge.Tick();
            BossChallengeNet.Tick();
            Difficulty.Tick();
            WalletShare.Tick();

            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer > 0f) return;

            refreshTimer = 0.5f;
            FountainCapacity.Refresh();
            SellStartItem.Tick();
        }

        private void LateUpdate()
        {
            WeaponShine.Tick();
            CursorOutline.Tick();
        }
    }
}
