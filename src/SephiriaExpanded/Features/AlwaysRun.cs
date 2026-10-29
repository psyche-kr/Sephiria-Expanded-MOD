using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SephiriaExpanded
{
    internal static class AlwaysRun
    {
        private const string DefaultKey = "<Keyboard>/leftCtrl";
        private const string ConflictKey = "UI_Options_Controls_Conflict";

        private static readonly AccessTools.FieldRef<PlayerLocalDataStorage, Timer> RunningTimer = AccessTools.FieldRefAccess<PlayerLocalDataStorage, Timer>("runningCountdownTimer");

        private static InputActionAsset asset;
        private static PlayerLocalDataStorage storage;

        internal static InputAction ToggleKey { get; private set; }

        internal static bool HasBinding
        {
            get { return ToggleKey != null && ToggleKey.bindings.Count > 0 && !string.IsNullOrEmpty(ToggleKey.bindings[0].effectivePath); }
        }

        internal static void Initialize()
        {
            if (asset != null) return;

            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "SephiriaExpanded_AlwaysRun";
            asset.hideFlags = HideFlags.HideAndDontSave;

            InputActionMap map = asset.AddActionMap("SephiriaExpanded");
            ToggleKey = map.AddAction("Toggle", InputActionType.Button, DefaultKey);

            if (!string.IsNullOrEmpty(ModSettings.AlwaysRunBindings)) asset.LoadBindingOverridesFromJson(ModKeyBinding.WithCurrentId(ModSettings.AlwaysRunBindings, ToggleKey));
            else if (!ModSettings.LegacyAlwaysRunEnabled) ToggleKey.ApplyBindingOverride(0, string.Empty);

            map.Enable();
            InputSystem.onActionChange += OnActionChange;
        }

        internal static void Cleanup()
        {
            Release();
            InputSystem.onActionChange -= OnActionChange;

            if (asset != null) Object.Destroy(asset);

            asset = null;
            ToggleKey = null;
        }

        internal static void ResetBinding()
        {
            if (ToggleKey != null) ToggleKey.ApplyBindingOverride(0, string.Empty);
        }

        [HarmonyPatch(typeof(PlayerInputController), nameof(PlayerInputController.HandleOnDash))]
        private static class KeepRunningPatch
        {
            [HarmonyPrefix]
            private static bool Before(InputAction.CallbackContext input)
            {
                bool released = input.action == null || !input.action.IsPressed();
                return !released || !IsAutoRunning();
            }
        }

        private static bool IsAutoRunning()
        {
            if (!HasBinding || !ModSettings.AlwaysRunActive) return false;

            PlayerAvatar player = LocalPlayer.Find();
            return player != null && CanRun(player);
        }

        [HarmonyPatch(typeof(RebindActionUI), nameof(RebindActionUI.ResetToDefault))]
        private static class ResetRebindPatch
        {
            [HarmonyPrefix]
            private static bool Before(RebindActionUI __instance)
            {
                if (ToggleKey == null || __instance.actionReference == null || __instance.actionReference.action != ToggleKey) return true;

                ResetBinding();
                __instance.UpdateBindingDisplay();
                return false;
            }
        }

        [HarmonyPatch(typeof(UI_ActionRemap_Keyboard), nameof(UI_ActionRemap_Keyboard.ResetKeybindingOverride))]
        private static class ResetRemapPatch
        {
            [HarmonyPrefix]
            private static bool Before(UI_ActionRemap_Keyboard __instance)
            {
                if (ToggleKey == null || __instance.input == null || __instance.input.action != ToggleKey) return true;

                ResetBinding();
                return false;
            }
        }

        internal static void Tick()
        {
            PlayerAvatar player = HasBinding ? LocalPlayer.Find() : null;
            if (player == null)
            {
                Release();
                return;
            }

            if (ToggleKey.WasPressedThisFrame() && UIManager.Instance != null && UIManager.Instance.CurrentControlStack == null)
            {
                ModSettings.AlwaysRunActive = !ModSettings.AlwaysRunActive;
                ModSettings.Save();
                Notify(ModSettings.AlwaysRunActive ? "자동 달리기 활성화" : "자동 달리기 비활성화");
            }

            if (ModSettings.AlwaysRunActive && CanRun(player) && player.localDataStorage.isInputReceived)
            {
                storage = player.localDataStorage;
                storage.StartRunningCountdown();
                if (!storage.isRunning) RunningTimer(storage).Ratio = 1f;
                return;
            }

            Release();
        }

        private static void Notify(string message)
        {
            if (GameLogWriter.Instance != null) GameLogWriter.Instance.WriteLog(message, Color.green);
        }

        internal static void ValidateRebind(RebindActionUI ui, InputActionRebindingExtensions.RebindingOperation operation)
        {
            if (operation == null || !operation.completed || operation.selectedControl == null) return;
            if (!IsUsedByGame(operation.selectedControl.path)) return;

            ResetBinding();
            UIManager.Instance.GetElement<UI_SystemMessage>().Open(new LocalizedString(ConflictKey).ToString(), 3f);
        }

        private static bool IsUsedByGame(string path)
        {
            InputActionAsset game = OptionsBinding.Instance == null ? null : OptionsBinding.Instance.actionAsset;
            if (game == null) return false;

            foreach (InputActionMap map in game.actionMaps)
            {
                if (map.name == "UI" || map.name == "Magic_Joystick") continue;

                foreach (InputAction action in map)
                    foreach (InputControl control in action.controls)
                        if (control.path == path) return true;
            }

            return false;
        }

        private static void Release()
        {
            if (storage == null) return;

            storage.StopRunningCountdown();
            storage.StopRun(true);
            storage = null;
        }

        private static bool CanRun(PlayerAvatar player)
        {
            return !player.IsDead && !player.IsInBattle && player.CanMove && player.CanRun.IsTrue() && player.localDataStorage != null &&
                UIManager.Instance != null && UIManager.Instance.CurrentControlStack == null;
        }

        private static void OnActionChange(object target, InputActionChange change)
        {
            if (change != InputActionChange.BoundControlsChanged && change != InputActionChange.ActionMapEnabled) return;

            InputActionMap map = (target as InputAction)?.actionMap ?? (target as InputActionMap);
            if ((map?.asset ?? (target as InputActionAsset)) != asset) return;

            ExpandedOptionRows.RefreshTitles();

            string json = asset.SaveBindingOverridesAsJson();
            if (json == ModSettings.AlwaysRunBindings) return;

            ModSettings.AlwaysRunBindings = json;
            ModSettings.Save();
        }
    }
}
