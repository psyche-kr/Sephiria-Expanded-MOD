using System;
using HarmonyLib;
using Mirror;

namespace SephiriaExpanded
{
    [HarmonyPatch(typeof(UnitAvatar), nameof(UnitAvatar.GetCustomStat), new[] { typeof(ECustomStat) })]
    internal static class MoreHpSteal
    {
        [HarmonyPostfix]
        private static void AddSteal(UnitAvatar __instance, ECustomStat id, ref int __result)
        {
            PlayerAvatar player = __instance as PlayerAvatar;
            if (!NetworkServer.active || player == null) return;
            bool host = player.spawner != null && player.spawner.isHost;

            int bonus = 0;
            if (id == ECustomStat.HPSteal) bonus = host ? ModSettings.DevHostHpSteal : ModSettings.DevGuestHpSteal;
            else if (id == ECustomStat.MPSteal) bonus = host ? ModSettings.DevHostMpSteal : ModSettings.DevGuestMpSteal;
            if (bonus <= 0) return;

            __result = (int)Math.Min(int.MaxValue, (long)__result + bonus);
        }
    }
}
