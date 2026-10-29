using System;
using HarmonyLib;
using Mirror;

namespace SephiriaExpanded
{
    internal static class FriendlyFire
    {
        [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.CalculateHostileFactionLayers))]
        private static class HostileLayers
        {
            [HarmonyPostfix]
            private static void After(string faction, EDamageFromType fromType, ref long __result)
            {
                if (!ModSettings.FriendlyFireEnabled || fromType == EDamageFromType.None || !NetworkServer.active) return;

                __result |= RuntimeFactionManager.Instance.FindFactionLayer(faction);
            }
        }

        [HarmonyPatch(typeof(PlayerAvatar), "HandleBeforeAttack")]
        private static class AllowPlayerTarget
        {
            [HarmonyPrefix]
            private static bool Before(UnitAvatar target)
            {
                if (!ModSettings.FriendlyFireEnabled || !NetworkServer.active) return true;

                return !(target is PlayerAvatar);
            }
        }

        [HarmonyPatch(typeof(UnitAvatar), "ApplyDamage")]
        private static class Damage
        {
            [HarmonyPrefix]
            private static bool Before(UnitAvatar __instance, DamageInstance __0, ref EApplyDamageResult __result, out float __state)
            {
                __state = float.NaN;
                if (!ModSettings.FriendlyFireEnabled || !NetworkServer.active || __0 == null) return true;

                PlayerAvatar victim = __instance as PlayerAvatar;
                PlayerAvatar attacker = __0.origin as PlayerAvatar;
                if (victim == null || attacker == null) return true;

                if (attacker == victim)
                {
                    if (__0.isSystemDamage) return true;

                    __result = EApplyDamageResult.Fail_Absolute;
                    return false;
                }

                __0.targetFactionLayers |= RuntimeFactionManager.Instance.FindFactionLayer(victim.faction);
                __state = __0.damage;
                __0.damage *= ModSettings.FriendlyFireMultiplier;
                return true;
            }

            [HarmonyFinalizer]
            private static Exception After(Exception __exception, DamageInstance __0, float __state)
            {
                if (__0 != null && !float.IsNaN(__state)) __0.damage = __state;
                return __exception;
            }
        }
    }
}
