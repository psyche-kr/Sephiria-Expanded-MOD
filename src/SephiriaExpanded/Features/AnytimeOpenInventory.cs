using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class AnytimeOpenInventory
    {
        private static readonly FieldInfo InputAvatar = typeof(PlayerInputController).GetField(
            "avatar", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo MenuAvatar = typeof(UI_HUDMenu).GetField(
            "avatar", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private struct BattleState
        {
            internal UnitAvatar Avatar;
            internal sbyte Value;
        }

        private static void Suppress(UnitAvatar avatar, ref BattleState state)
        {
            if (!ModSettings.AnytimeOpenInventoryEnabled || avatar == null || avatar.isInBattle == 0)
                return;

            state.Avatar = avatar;
            state.Value = avatar.isInBattle;
            avatar.isInBattle = 0;
        }

        private static Exception Restore(Exception exception, BattleState state)
        {
            if (state.Avatar != null)
                state.Avatar.isInBattle = state.Value;

            return exception;
        }

        [HarmonyPatch(typeof(PlayerInputController), "HandleOnOpenCharacterStatusPanel")]
        private static class OpenCharacterStatusPanel
        {
            [HarmonyPrefix]
            private static void Before(PlayerInputController __instance, ref BattleState __state)
            {
                Suppress(InputAvatar != null && __instance != null
                    ? InputAvatar.GetValue(__instance) as UnitAvatar
                    : null, ref __state);
            }

            [HarmonyFinalizer]
            private static Exception After(Exception __exception, BattleState __state)
            {
                return Restore(__exception, __state);
            }
        }

        [HarmonyPatch(typeof(PlayerInputController), "HandleOpenStatsPanel")]
        private static class OpenStatsPanelHotkey
        {
            [HarmonyPrefix]
            private static void Before(PlayerInputController __instance, ref BattleState __state)
            {
                Suppress(InputAvatar != null && __instance != null
                    ? InputAvatar.GetValue(__instance) as UnitAvatar
                    : null, ref __state);
            }

            [HarmonyFinalizer]
            private static Exception After(Exception __exception, BattleState __state)
            {
                return Restore(__exception, __state);
            }
        }

        [HarmonyPatch(typeof(UI_HUDMenu), "OpenInventoryUI")]
        private static class OpenInventoryButton
        {
            [HarmonyPrefix]
            private static void Before(UI_HUDMenu __instance, ref BattleState __state)
            {
                Suppress(MenuAvatar != null && __instance != null
                    ? MenuAvatar.GetValue(__instance) as UnitAvatar
                    : null, ref __state);
            }

            [HarmonyFinalizer]
            private static Exception After(Exception __exception, BattleState __state)
            {
                return Restore(__exception, __state);
            }
        }

        [HarmonyPatch(typeof(UI_HUDMenu), "OpenStatsUI")]
        private static class OpenStatsButton
        {
            [HarmonyPrefix]
            private static void Before(UI_HUDMenu __instance, ref BattleState __state)
            {
                Suppress(MenuAvatar != null && __instance != null
                    ? MenuAvatar.GetValue(__instance) as UnitAvatar
                    : null, ref __state);
            }

            [HarmonyFinalizer]
            private static Exception After(Exception __exception, BattleState __state)
            {
                return Restore(__exception, __state);
            }
        }

        [HarmonyPatch(typeof(UI_HUDMenu), "Update")]
        private static class MenuUpdate
        {
            [HarmonyPostfix]
            private static void After(UI_HUDMenu __instance)
            {
                if (!ModSettings.AnytimeOpenInventoryEnabled || __instance == null || __instance.canvasGroup == null)
                    return;

                __instance.canvasGroup.blocksRaycasts = true;
                __instance.canvasGroup.interactable = true;
            }
        }
    }
}
