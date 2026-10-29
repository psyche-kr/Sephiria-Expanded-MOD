using HarmonyLib;
using Mirror;

namespace SephiriaExpanded
{
    internal static class BloodFestival
    {
        private static UnitAvatar healingTarget;

        [HarmonyPatch(typeof(CombatManager), "AttackEvent",
            new[] { typeof(UnitAvatar), typeof(UnitAvatar), typeof(DamageData) })]
        private static class AttackEvent
        {
            [HarmonyPrefix]
            private static void Before(UnitAvatar __1)
            {
                healingTarget = null;

                if (!NetworkServer.active || __1 == null || __1 is PlayerAvatar)
                    return;

                if (!ModSettings.BloodFestivalEnabled && BossChallenge.HpScaleOf(__1) <= 1f)
                    return;

                DungeonManager manager = DungeonManager.Instance;
                int value;
                if (manager != null && manager.hardModeEnvironment.TryGetValue("BLOODFESTIVAL", out value) && value > 0)
                    healingTarget = __1;
            }

            [HarmonyFinalizer]
            private static System.Exception After(System.Exception __exception)
            {
                healingTarget = null;
                return __exception;
            }
        }

        [HarmonyPatch(typeof(UnitAvatar), "HealPercent", new[] { typeof(float) })]
        private static class HealPercent
        {
            [HarmonyPrefix]
            private static void Before(UnitAvatar __instance, ref float __0)
            {
                if (__instance == null || __instance != healingTarget)
                    return;

                healingTarget = null;

                float divisor = BossChallenge.HpScaleOf(__instance);
                if (ModSettings.BloodFestivalEnabled)
                {
                    float multiplier = MonsterScaler.HpFactorOf(__instance);
                    if (multiplier > 1f)
                        divisor *= multiplier;
                }

                if (divisor > 1f)
                    __0 /= divisor;
            }
        }
    }
}
