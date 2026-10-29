using System;
using System.Collections.Generic;
using HarmonyLib;
using Mirror;

namespace SephiriaExpanded
{
    internal static class RandomEvent
    {
        private static EFloorMainEventType[] Weighted(EFloorMainEventType[] source, int multiplier)
        {
            if (source == null) return null;
            multiplier = Math.Max(1, Math.Min(10, multiplier));
            var result = new List<EFloorMainEventType>(source.Length * multiplier);
            foreach (var item in source)
            {
                int count = item == EFloorMainEventType.RandomEncounter ? multiplier : 1;
                for (int i = 0; i < count; i++) result.Add(item);
            }
            return result.ToArray();
        }

        [HarmonyPatch(typeof(StageEntity_Choice), nameof(StageEntity_Choice.GenerateStage))]
        private static class Choice
        {
            [HarmonyPrefix]
            private static void Before(StageEntity_Choice __instance, out Action __state)
            {
                __state = null;
                if (!NetworkServer.active || ModSettings.RandomEventMultiplier <= 1 || __instance.steps == null) return;
                var undo = new List<Action>();
                __state = () => { foreach (var restore in undo) restore(); };
                foreach (var step in __instance.steps)
                {
                    if (step == null) continue;
                    var original = step.possibleMainEvents;
                    undo.Add(() => step.possibleMainEvents = original);
                    step.possibleMainEvents = Weighted(original, ModSettings.RandomEventMultiplier);
                }
            }
            [HarmonyFinalizer]
            private static Exception After(Exception __exception, Action __state)
            { __state?.Invoke(); return __exception; }
        }

        [HarmonyPatch(typeof(StageEntity_Infinity), nameof(StageEntity_Infinity.GenerateStage))]
        private static class Infinity
        {
            [HarmonyPrefix]
            private static void Before(StageEntity_Infinity __instance, out Action __state)
            {
                __state = null;
                if (!NetworkServer.active || ModSettings.RandomEventMultiplier <= 1 || __instance.steps == null) return;
                var undo = new List<Action>();
                __state = () => { foreach (var restore in undo) restore(); };
                foreach (var step in __instance.steps)
                {
                    if (step == null) continue;
                    var original = step.possibleMainEvents;
                    undo.Add(() => step.possibleMainEvents = original);
                    step.possibleMainEvents = Weighted(original, ModSettings.RandomEventMultiplier);
                }
            }
            [HarmonyFinalizer]
            private static Exception After(Exception __exception, Action __state)
            { __state?.Invoke(); return __exception; }
        }
    }
}
