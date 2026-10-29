using HarmonyLib;
using Mirror;

namespace SephiriaExpanded
{
    [HarmonyPatch(typeof(MysticPot), "OnStartServer")]
    internal static class MysteryPot
    {
        [HarmonyPrefix]
        private static void Before(MysticPot __instance, ref float __state)
        {
            if (__instance == null)
                return;

            __state = __instance.appearRate;
            if (NetworkServer.active && ModSettings.MysteryPotGuaranteedEnabled &&
                __instance.useRandomAppear)
                __instance.appearRate = 1f;
        }

        [HarmonyPostfix]
        private static void After(MysticPot __instance, float __state)
        {
            if (__instance != null)
                __instance.appearRate = __state;
        }
    }
}
