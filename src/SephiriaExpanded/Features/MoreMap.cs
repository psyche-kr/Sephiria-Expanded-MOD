using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace SephiriaExpanded
{
    internal static class MoreMap
    {
        private const int CombatPurpose = 2;
        private const int MinimumAdditionalCombatRooms = 4;
        private const int MaximumAdditionalCombatRooms = 6;
        private static readonly ConditionalWeakTable<object, LoadState> LoadStates = new ConditionalWeakTable<object, LoadState>();

        private sealed class LoadState
        {
            internal bool Expanded;
        }

        [HarmonyPatch]
        private static class LoadRoomData
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                string[] names =
                {
                    "EnhancedProceduralFloorGenerator",
                    "LibraryFloorGenerator"
                };

                foreach (string name in names)
                {
                    Type type = AccessTools.TypeByName(name);
                    MethodInfo method = type == null
                        ? null
                        : AccessTools.Method(type, "LoadAllRoomPresetData", new[] { typeof(Random) });
                    if (method != null)
                        yield return method;
                }
            }

            [HarmonyPostfix]
            private static void After(object __instance, Random __0)
            {
                if (__instance == null)
                    return;

                try
                {
                    Type generatorType = __instance.GetType();
                    if (!MoreMapCompatibility.TryLockBeforeExpansion())
                        return;
                    if (IsFirstStage(generatorType, __instance))
                        return;
                    FieldInfo countField = AccessTools.Field(generatorType, "combatRoomCount");
                    FieldInfo directoryField = AccessTools.Field(generatorType, "presetDirectory_Combat");
                    if (countField == null || directoryField == null)
                        return;

                    int originalCount = (int)countField.GetValue(__instance);
                    if (originalCount <= 0)
                        return;

                    ProceduralFloorMetadataBaker baker = ProceduralFloorMetadataBaker.Instance;
                    if (baker == null)
                        return;

                    string directory = directoryField.GetValue(__instance) as string;
                    if (string.IsNullOrEmpty(directory))
                        return;

                    System.Collections.IList presets = baker.GetPreset(directory) as System.Collections.IList;
                    if (presets == null || presets.Count == 0)
                        return;

                    MethodInfo loadMethod = FindLoadMethod(generatorType, presets[0].GetType());
                    MethodInfo cloneMethod = presets[0].GetType().GetMethod(
                        "MemberwiseClone",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (loadMethod == null || cloneMethod == null)
                        return;

                    LoadState loadState = LoadStates.GetOrCreateValue(__instance);
                    if (loadState.Expanded)
                        return;

                    int requestedCount = __0 == null
                        ? MinimumAdditionalCombatRooms
                        : __0.Next(MinimumAdditionalCombatRooms, MaximumAdditionalCombatRooms + 1);
                    int extraCount = Math.Min(requestedCount, presets.Count);
                    int loaded = 0;
                    for (int i = 0; i < extraCount; i++)
                    {
                        object clone = cloneMethod.Invoke(presets[i], null);
                        object purpose = Enum.ToObject(loadMethod.GetParameters()[1].ParameterType, CombatPurpose);
                        loadMethod.Invoke(__instance, new[] { clone, purpose });
                        loaded++;
                    }

                    if (loaded > 0)
                    {
                        countField.SetValue(__instance, originalCount + loaded);
                        loadState.Expanded = true;
                    }
                }
                catch
                {
                }
            }

            private static bool IsFirstStage(Type generatorType, object generator)
            {
                FieldInfo trainingField = AccessTools.Field(generatorType, "isTrainingFloor");
                if (trainingField != null && trainingField.GetValue(generator) is bool training && training)
                    return true;

                FieldInfo dataField = AccessTools.Field(generatorType, "data");
                FloorData data = dataField == null ? null : dataField.GetValue(generator) as FloorData;
                return data != null && data.nodeProgress <= 0;
            }

            private static MethodInfo FindLoadMethod(Type generatorType, Type metadataType)
            {
                MethodInfo[] methods = generatorType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (MethodInfo method in methods)
                {
                    if (method.Name != "LoadRoomPresetData")
                        continue;

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 2 && parameters[0].ParameterType == metadataType)
                        return method;
                }

                return null;
            }
        }
    }
}
