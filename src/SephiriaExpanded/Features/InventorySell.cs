using HarmonyLib;
using Mirror;

namespace SephiriaExpanded
{
    [HarmonyPatch(typeof(InventoryShop), "OnStartServer")]
    internal static class InventorySell
    {
        [HarmonyPrefix]
        private static void Before(InventoryShop __instance, ref float __state)
        {
            if (__instance == null)
                return;

            __state = __instance.appearRate;
            if (NetworkServer.active && ModSettings.InventorySellEnabled)
                __instance.appearRate = 1f;
        }

        [HarmonyPostfix]
        private static void After(InventoryShop __instance, float __state)
        {
            if (__instance != null)
                __instance.appearRate = __state;
        }
    }
}
