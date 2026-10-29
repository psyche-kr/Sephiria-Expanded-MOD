using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace SephiriaExpanded
{
    internal static class MoreMultiplayer
    {
        private const int MinPlayers = 2;
        private const int MaxPlayers = 16;
        private const int MultiplayerSelectionCount = MaxPlayers - MinPlayers + 1;
        private const int VanillaSelectionCount = 3;
        private const float TickInterval = 0.25f;

        private static float nextTick;

        internal static void Tick()
        {
            if (UnityEngine.Time.unscaledTime < nextTick) return;

            nextTick = UnityEngine.Time.unscaledTime + TickInterval;
            int selectionCount = ModSettings.Multiplayer16Enabled ? MultiplayerSelectionCount : VanillaSelectionCount;
            foreach (UI_HorizontalSelectionBox_MultiplayerNumber selector in
                     UnityEngine.Object.FindObjectsByType<UI_HorizontalSelectionBox_MultiplayerNumber>(
                         UnityEngine.FindObjectsInactive.Include,
                         UnityEngine.FindObjectsSortMode.None))
            {
                if (selector != null && selector.box != null)
                {
                    if (selector.box.numberOfElements != selectionCount)
                        selector.box.numberOfElements = selectionCount;
                    int selection = Math.Max(0, Math.Min(selector.box.CurrentSelection, selectionCount - 1));
                    if (selector.box.CurrentSelection != selection)
                        selector.box.ChangeValueWithoutNotify(selection);
                }
            }
        }

        private static void SetSteamLobbySlots(object lobbyManager)
        {
            if (lobbyManager == null) return;

            FieldInfo createArgumentsField = AccessTools.Field(lobbyManager.GetType(), "createArguments");
            object createArguments = createArgumentsField != null ? createArgumentsField.GetValue(lobbyManager) : null;
            if (createArguments == null) return;

            FieldInfo slotsField = AccessTools.Field(createArguments.GetType(), "slots");
            if (slotsField == null) return;

            slotsField.SetValue(createArguments, MaxPlayers);
            if (createArguments.GetType().IsValueType)
                createArgumentsField.SetValue(lobbyManager, createArguments);
        }

        private static void ExpandPlayerSprites(object instance, string fieldName)
        {
            if (!ModSettings.Multiplayer16Enabled || instance == null) return;

            FieldInfo field = AccessTools.Field(instance.GetType(), fieldName);
            Array source = field != null ? field.GetValue(instance) as Array : null;
            if (source == null || source.Length == 0 || source.Length >= MaxPlayers) return;

            Array expanded = Array.CreateInstance(source.GetType().GetElementType(), MaxPlayers);
            for (int i = 0; i < MaxPlayers; i++)
                expanded.SetValue(source.GetValue(i % source.Length), i);

            field.SetValue(instance, expanded);
        }

        [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStartServer))]
        private static class StartServer
        {
            [HarmonyPrefix]
            private static void Before(HorayNetworkManager __instance)
            {
                if (ModSettings.Multiplayer16Enabled && __instance != null)
                    __instance.maxConnections = MaxPlayers;
            }
        }

        [HarmonyPatch(typeof(EOSLobbyManager), "Create",
            new[] { typeof(string), typeof(int), typeof(bool), typeof(string) })]
        private static class CreateEosLobby
        {
            [HarmonyPrefix]
            private static void Before(ref int maxMembers)
            {
                if (ModSettings.Multiplayer16Enabled)
                    maxMembers = MaxPlayers;
            }
        }

        [HarmonyPatch]
        private static class CreateSteamLobby
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                Type type = AccessTools.TypeByName("HeathenEngineering.SteamworksIntegration.LobbyManager");
                return type == null
                    ? Enumerable.Empty<MethodBase>()
                    : AccessTools.GetDeclaredMethods(type).Where(method => method.Name == "Create");
            }

            [HarmonyPrefix]
            private static void Before(object __instance, object[] __args)
            {
                if (!ModSettings.Multiplayer16Enabled) return;

                SetSteamLobbySlots(__instance);
                if (__args == null) return;

                for (int i = 0; i < __args.Length; i++)
                    if (__args[i] is int)
                        __args[i] = MaxPlayers;
            }
        }

        [HarmonyPatch]
        private static class PlayerSpriteArrays
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                return new MethodBase[]
                {
                    AccessTools.Method(typeof(MultiplayerCursor), "SetCursor"),
                    AccessTools.Method(typeof(MultiplayerCursor_Transform), "SetCursor"),
                    AccessTools.Method(typeof(MultiplayerPing), "Start"),
                    AccessTools.Method(typeof(MultiplayerPing), "SetCursor"),
                    AccessTools.Method(typeof(UI_MapPanelPlayerIcon), "SetPlayerIdx"),
                    AccessTools.Method(typeof(UI_MultiplayerHPBar), "SetSpawner")
                }.Where(method => method != null);
            }

            [HarmonyPrefix]
            private static void Before(object __instance, MethodBase __originalMethod)
            {
                if (__originalMethod.DeclaringType == typeof(MultiplayerCursor) ||
                    __originalMethod.DeclaringType == typeof(MultiplayerCursor_Transform) ||
                    __originalMethod.DeclaringType == typeof(MultiplayerPing))
                    ExpandPlayerSprites(__instance, "playerCursorByIdx");
                else if (__originalMethod.DeclaringType == typeof(UI_MapPanelPlayerIcon))
                    ExpandPlayerSprites(__instance, "playerIcons");
                else if (__originalMethod.DeclaringType == typeof(UI_MultiplayerHPBar))
                    ExpandPlayerSprites(__instance, "multiCursorByIdx");
            }
        }

        [HarmonyPatch(typeof(UI_HorizontalSelectionBox_MultiplayerNumber), "HandleValueChanged")]
        private static class MultiplayerNumberSelector
        {
            [HarmonyPostfix]
            private static void AfterEnable(UI_HorizontalSelectionBox_MultiplayerNumber __instance)
            {
                if (__instance == null || __instance.box == null)
                    return;

                int selectionCount = ModSettings.Multiplayer16Enabled ? MultiplayerSelectionCount : VanillaSelectionCount;
                if (__instance.box.numberOfElements != selectionCount)
                    __instance.box.numberOfElements = selectionCount;
                int selection = Math.Max(0, Math.Min(__instance.box.CurrentSelection, selectionCount - 1));
                if (__instance.box.CurrentSelection != selection)
                    __instance.box.ChangeValueWithoutNotify(selection);
            }

            [HarmonyPrefix]
            private static void Before(UI_HorizontalSelectionBox_MultiplayerNumber __instance)
            {
                if (ModSettings.Multiplayer16Enabled && __instance != null && __instance.box != null)
                {
                    __instance.box.numberOfElements = MultiplayerSelectionCount;
                    __instance.box.ChangeValueWithoutNotify(Math.Max(0, Math.Min(__instance.box.CurrentSelection, MultiplayerSelectionCount - 1)));
                }
            }
        }
    }
}
