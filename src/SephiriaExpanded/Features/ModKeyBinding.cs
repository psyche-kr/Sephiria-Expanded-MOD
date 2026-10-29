using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SephiriaExpanded
{
    internal sealed class ModKeyBinding
    {
        private const string DefaultKey = "<Keyboard>/f10";
        private const string ConflictKey = "UI_Options_Controls_Conflict";

        private static readonly Regex IdPattern = new Regex("\"id\"\\s*:\\s*\"[^\"]*\"");
        private static readonly List<ModKeyBinding> Instances = new List<ModKeyBinding>();

        private readonly string name;
        private readonly Func<string> load;
        private readonly Action<string> save;
        private InputActionAsset asset;

        internal ModKeyBinding(string name, Func<string> load, Action<string> save)
        {
            this.name = name;
            this.load = load;
            this.save = save;
        }

        internal InputAction Action { get; private set; }

        internal bool HasBinding
        {
            get { return Action != null && Action.bindings.Count > 0 && !string.IsNullOrEmpty(Action.bindings[0].effectivePath); }
        }

        internal bool Pressed
        {
            get { return HasBinding && Action.WasPressedThisFrame(); }
        }

        internal void Initialize()
        {
            if (asset != null) return;

            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "SephiriaExpanded_" + name;
            asset.hideFlags = HideFlags.HideAndDontSave;

            InputActionMap map = asset.AddActionMap("SephiriaExpanded");
            Action = map.AddAction("Key", InputActionType.Button, DefaultKey);

            string json = load();
            if (!string.IsNullOrEmpty(json)) asset.LoadBindingOverridesFromJson(WithCurrentId(json, Action));
            else Action.ApplyBindingOverride(0, string.Empty);

            map.Enable();
            Instances.Add(this);
            InputSystem.onActionChange += OnActionChange;
        }

        internal static string WithCurrentId(string json, InputAction action)
        {
            return IdPattern.Replace(json, "\"id\":\"" + action.bindings[0].id + "\"");
        }

        internal void Cleanup()
        {
            Instances.Remove(this);
            InputSystem.onActionChange -= OnActionChange;

            if (asset != null) UnityEngine.Object.Destroy(asset);

            asset = null;
            Action = null;
        }

        internal void Reset()
        {
            if (Action != null) Action.ApplyBindingOverride(0, string.Empty);
        }

        internal void Validate(RebindActionUI ui, InputActionRebindingExtensions.RebindingOperation operation)
        {
            if (operation == null || !operation.completed || operation.selectedControl == null) return;
            if (!IsUsedByGame(operation.selectedControl.path)) return;

            Reset();
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

        private static ModKeyBinding Find(InputAction action)
        {
            if (action == null) return null;

            foreach (ModKeyBinding binding in Instances)
                if (binding.Action == action) return binding;

            return null;
        }

        private void OnActionChange(object target, InputActionChange change)
        {
            if (asset == null || change != InputActionChange.BoundControlsChanged && change != InputActionChange.ActionMapEnabled) return;

            InputActionMap map = (target as InputAction)?.actionMap ?? (target as InputActionMap);
            if ((map?.asset ?? (target as InputActionAsset)) != asset) return;

            ExpandedOptionRows.RefreshTitles();

            string json = asset.SaveBindingOverridesAsJson();
            if (json == load()) return;

            save(json);
            ModSettings.Save();
        }

        [HarmonyPatch(typeof(RebindActionUI), nameof(RebindActionUI.ResetToDefault))]
        private static class ResetRebindPatch
        {
            [HarmonyPrefix]
            private static bool Before(RebindActionUI __instance)
            {
                ModKeyBinding binding = __instance.actionReference == null ? null : Find(__instance.actionReference.action);
                if (binding == null) return true;

                binding.Reset();
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
                ModKeyBinding binding = __instance.input == null ? null : Find(__instance.input.action);
                if (binding == null) return true;

                binding.Reset();
                return false;
            }
        }
    }
}
