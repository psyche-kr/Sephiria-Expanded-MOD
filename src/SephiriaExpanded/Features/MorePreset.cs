using HarmonyLib;

namespace SephiriaExpanded
{
    internal static class MorePreset
    {
        internal const int ExpandedLimit = 100;

        [HarmonyPatch(typeof(UI_PresetPanel), "GetSlotLimitCount")]
        private static class PresetSlotLimitPatch
        {
            private static bool Prefix(ref int __result)
            {
                if (!ModSettings.MorePresetEnabled)
                    return true;

                __result = ExpandedLimit;
                return false;
            }
        }
    }
}
