using System;
using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    [HarmonyPatch(typeof(PlayerAvatar), "LocalAddDice", new[] { typeof(int) })]
    internal static class MoreDice
    {
        [HarmonyPrefix]
        private static bool BeforeAdd(PlayerAvatar __instance, ref int dice)
        {
            if (!NetworkServer.active || __instance == null || dice >= 0) return true;
            bool enabled = __instance.spawner != null && __instance.spawner.isHost ? ModSettings.MoreDiceHostEnabled : ModSettings.MoreDiceGuestEnabled;
            if (!enabled) return true;
            if (__instance.rerollDice <= 1) return false;
            dice = Mathf.Max(dice, 1 - __instance.rerollDice);
            return true;
        }
    }

}
