using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SephiriaExpanded
{
    internal static class RightClickHold
    {
        private const float InitialDelay = 0.12f;
        private const float RepeatInterval = 0.2f;

        private static readonly AccessTools.FieldRef<PlayerInputController, bool> SubFirePressed = AccessTools.FieldRefAccess<PlayerInputController, bool>("subFirePressed");
        private static readonly AccessTools.FieldRef<PlayerInputController, bool> IsAnyUIHovered = AccessTools.FieldRefAccess<PlayerInputController, bool>("isAnyUIHovered");
        private static readonly AccessTools.FieldRef<PlayerInputController, IntegratedActionController> ActionController = AccessTools.FieldRefAccess<PlayerInputController, IntegratedActionController>("integratedActionController");
        private static readonly AccessTools.FieldRef<PlayerInputController, WeaponControllerSimple> WeaponController = AccessTools.FieldRefAccess<PlayerInputController, WeaponControllerSimple>("weaponController");
        private static readonly AccessTools.FieldRef<PlayerInputController, PlayerAvatar> Avatar = AccessTools.FieldRefAccess<PlayerInputController, PlayerAvatar>("avatar");
        private static readonly AccessTools.FieldRef<PlayerInputController, SkillController> Skills = AccessTools.FieldRefAccess<PlayerInputController, SkillController>("skillController");
        private static readonly Dictionary<Type, FieldInfo> SpecialAttackTypes = new Dictionary<Type, FieldInfo>();
        private static readonly MethodInfo GetAimedPosition = AccessTools.Method(typeof(PlayerInputController), "GetAimedPosition");
        private static readonly Func<PlayerInputController, Vector2> GetAimedPositionDelegate = CreateAimedPositionDelegate();

        private static PlayerInputController armedController;
        private static float nextCastTime;
        private static bool armed;
        private static bool mustRelease = true;
        private static bool wasEnabled;
        private static InputAction fireAction;
        private static PlayerInput fireActionOwner;
        private static bool magicArmed;
        private static float nextMagicCastTime;

        private static Func<PlayerInputController, Vector2> CreateAimedPositionDelegate()
        {
            if (GetAimedPosition == null)
                return null;
            return (Func<PlayerInputController, Vector2>)GetAimedPosition.CreateDelegate(typeof(Func<PlayerInputController, Vector2>));
        }

        private static Vector2 AimedPosition(PlayerInputController controller)
        {
            return GetAimedPositionDelegate != null
                ? GetAimedPositionDelegate(controller)
                : GetAimedPosition == null
                    ? Vector2.zero
                    : (Vector2)GetAimedPosition.Invoke(controller, null);
        }

        [HarmonyPatch(typeof(PlayerInputController), "Update")]
        private static class Repeat
        {
            [HarmonyPostfix]
            private static void After(PlayerInputController __instance)
            {
                if (!ModSettings.RightClickHoldEnabled)
                {
                    Reset();
                    return;
                }

                if (__instance == null || !__instance.enabled)
                    return;

                if (armedController != __instance)
                {
                    Reset();
                    armedController = __instance;
                }

                bool pressed = SubFirePressed(__instance);
                if (!wasEnabled)
                {
                    wasEnabled = true;
                    mustRelease = pressed;
                }

                if (!CanReadGameplayInput(__instance))
                {
                    armed = false;
                    mustRelease = true;
                    magicArmed = false;
                    return;
                }

                RepeatMagic(__instance);

                if (mustRelease)
                {
                    if (!pressed)
                        mustRelease = false;
                    return;
                }

                if (!pressed)
                {
                    armed = false;
                    return;
                }

                if (!armed)
                {
                    armed = true;
                    nextCastTime = Time.unscaledTime + InitialDelay;
                    return;
                }

                if (Time.unscaledTime < nextCastTime)
                    return;

                IntegratedActionController controller = ActionController(__instance);
                WeaponControllerSimple weaponController = WeaponController(__instance);
                if (!CanRepeatSpecial(__instance, controller, weaponController))
                    return;

                controller.Cast(101, AimedPosition(__instance), __instance.autoAimedTarget);
                nextCastTime = Time.unscaledTime + RepeatInterval;
            }
        }

        private static void RepeatMagic(PlayerInputController controller)
        {
            PlayerInput playerInput = controller.playerInput;
            if (playerInput == null)
            {
                magicArmed = false;
                return;
            }

            if (fireActionOwner != playerInput)
            {
                fireActionOwner = playerInput;
                fireAction = FindFireAction(playerInput);
                magicArmed = false;
            }

            if (fireAction == null || !fireAction.enabled)
            {
                magicArmed = false;
                return;
            }

            if (!magicArmed)
            {
                if (!fireAction.WasPressedThisFrame())
                    return;
                magicArmed = true;
                nextMagicCastTime = Time.unscaledTime + InitialDelay;
                return;
            }

            if (!fireAction.IsPressed())
            {
                magicArmed = false;
                return;
            }

            if (Time.unscaledTime < nextMagicCastTime)
                return;

            IntegratedActionController actions = ActionController(controller);
            if (!CanRepeatMagic(controller, actions, Avatar(controller)))
                return;

            actions.Cast(controller.currentQuickSlotIdx, AimedPosition(controller), controller.autoAimedTarget);
            nextMagicCastTime = Time.unscaledTime + RepeatInterval;
        }

        private static InputAction FindFireAction(PlayerInput playerInput)
        {
            InputActionMap map = playerInput.actions == null ? null : playerInput.actions.FindActionMap("Player", false);
            return map == null ? null : map.FindAction("Fire", false);
        }

        private static bool CanRepeatMagic(PlayerInputController controller, IntegratedActionController actions, PlayerAvatar avatar)
        {
            if (actions == null || avatar == null || !avatar.activeMagicCastModeClientside || avatar.NetworkIsDead || !avatar.CanMove)
                return false;

            int slot = controller.currentQuickSlotIdx;
            QuickSlotData[] slots = actions.quickSlots;
            if (slots == null || slot < 0 || slot >= slots.Length)
                return false;

            QuickSlotData data = slots[slot];
            if (data == null)
                return false;

            if (data.Type == QuickSlotType.Active)
                return CanRepeatActive(data.active);

            if (data.Type != QuickSlotType.Magic)
                return false;

            SkillController skillController = Skills(controller);
            if (skillController == null || !skillController.CanCast || skillController.IsInGlobalCooldown ||
                avatar.GetCustomStatUnsafe("BLOCKCASTMAGIC") > 0)
                return false;

            Charm_Magic magic = data.magic;
            ActiveSkillEntity activeSkill = magic == null ? null : magic.ContainedMagic;
            return magic != null && activeSkill != null && magic.skill != null &&
                   activeSkill.castingType != ECastingType.Charge &&
                   magic.CanCast(avatar, true, true) == ECanUseSkillResult.Succeeded;
        }

        private static bool CanRepeatActive(IActiveCasting active)
        {
            if (active == null || active.GetCooldownRatio() > 0f)
                return false;

            Charm_Basic charm = active as Charm_Basic;
            return charm == null || charm.IsEffectEnabled;
        }

        private static bool CanReadGameplayInput(PlayerInputController controller)
        {
            if (controller == null || !controller.enabled || controller.BlockAvatarInput || !Application.isFocused)
                return false;

            UIManager manager = UIManager.Instance;
            if (manager != null && manager.CurrentControlStack != null)
                return false;

            return !IsAnyUIHovered(controller);
        }

        private static bool CanRepeatSpecial(PlayerInputController controller, IntegratedActionController actions, WeaponControllerSimple weaponController)
        {
            if (actions == null || weaponController == null || !weaponController.DoCastValidation())
                return false;

            PlayerAvatar avatar = Avatar(controller);
            if (avatar == null || avatar.NetworkIsDead || !avatar.CanMove)
                return false;

            WeaponSimple weapon = weaponController.currentWeapon;
            if (weapon == null)
                return false;

            string weaponName = weapon.GetType().Name;
            if (weaponName == "WeaponSimple_QuartterStaff" || weaponName == "WeaponSimple_Dagger" || weaponName == "WeaponSimple_Katana")
                return true;

            if (weaponName != "WeaponSimple_Crossbow")
                return false;

            FieldInfo attackType = GetSpecialAttackType(weapon.GetType());
            return attackType == null || !string.Equals(Convert.ToString(attackType.GetValue(weapon)), "Minigun", StringComparison.OrdinalIgnoreCase);
        }

        private static FieldInfo GetSpecialAttackType(Type weaponType)
        {
            FieldInfo field;
            if (!SpecialAttackTypes.TryGetValue(weaponType, out field))
            {
                field = AccessTools.Field(weaponType, "specialAttackType");
                SpecialAttackTypes[weaponType] = field;
            }

            return field;
        }

        private static void Reset()
        {
            armedController = null;
            nextCastTime = 0f;
            armed = false;
            mustRelease = true;
            wasEnabled = false;
            fireAction = null;
            fireActionOwner = null;
            magicArmed = false;
            nextMagicCastTime = 0f;
        }
    }
}
