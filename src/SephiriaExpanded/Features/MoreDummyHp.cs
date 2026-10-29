using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class MoreDummyHp
    {
        private const float Hp = 10000001f;

        private static readonly Dictionary<UnitAvatar, float> Originals = new Dictionary<UnitAvatar, float>();

        internal static void Refresh()
        {
            if (!NetworkServer.active) return;

            Prune();
            foreach (UnitAvatar unit in Object.FindObjectsByType<UnitAvatar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (unit.monsterType == EMonsterType.Dummy) Apply(unit);
        }

        internal static void Cleanup()
        {
            if (NetworkServer.active)
            {
                foreach (KeyValuePair<UnitAvatar, float> pair in Originals)
                    if (pair.Key != null && !pair.Key.IsDead) Restore(pair.Key, pair.Value);
            }

            Originals.Clear();
        }

        private static void Apply(UnitAvatar unit)
        {
            if (unit == null || unit.IsDead) return;

            float original;
            if (ModSettings.MoreDummyHpEnabled)
            {
                if (unit.maxHp == Hp) return;

                Originals[unit] = unit.maxHp;
                unit.NetworkmaxHp = Hp;
                unit.SetHp(unit.MaxHp);
            }
            else if (Originals.TryGetValue(unit, out original))
            {
                Originals.Remove(unit);
                Restore(unit, original);
            }
        }

        private static void Restore(UnitAvatar unit, float original)
        {
            unit.NetworkmaxHp = original;
            unit.SetHp(unit.MaxHp);
        }

        private static void Prune()
        {
            List<UnitAvatar> destroyed = null;
            foreach (UnitAvatar unit in Originals.Keys)
            {
                if (unit != null) continue;

                if (destroyed == null) destroyed = new List<UnitAvatar>();
                destroyed.Add(unit);
            }

            if (destroyed != null)
                foreach (UnitAvatar unit in destroyed) Originals.Remove(unit);
        }

        [HarmonyPatch(typeof(UnitAvatar), "ApplyDamage")]
        private static class Hit
        {
            [HarmonyPrefix]
            private static void Before(UnitAvatar __instance)
            {
                if (ModSettings.MoreDummyHpEnabled && NetworkServer.active && __instance.monsterType == EMonsterType.Dummy) Apply(__instance);
            }
        }
    }
}
