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
        private const int EventPurpose = 4;
        private static readonly ConditionalWeakTable<object, EventState> EventStates = new ConditionalWeakTable<object, EventState>();
        private static readonly ConditionalWeakTable<object, LoadState> LoadStates = new ConditionalWeakTable<object, LoadState>();

        private sealed class EventState
        {
            internal int Count;
        }

        private sealed class LoadState
        {
            internal bool Expanded;
        }

        private static void MarkEvent(object state, bool value)
        {
            if (state == null || !value)
                return;

            FieldInfo field = AccessTools.Field(state.GetType(), "<eventRoom>5__16");
            if (field != null)
                field.SetValue(state, true);

            if (!MoreMapCompatibility.IsExpansionAllowed())
                return;

            EventState eventState = EventStates.GetOrCreateValue(state);
            eventState.Count++;
            if (eventState.Count < 2)
                return;

            if (field != null)
                field.SetValue(state, true);
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

                    if (loaded == MaximumAdditionalCombatRooms)
                        AddEventPreset(__instance, generatorType);
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

            private static void AddEventPreset(object generator, Type generatorType)
            {
                FieldInfo registeredField = AccessTools.Field(generatorType, "registeredRooms");
                if (registeredField == null)
                    return;

                System.Collections.IDictionary registered = registeredField.GetValue(generator) as System.Collections.IDictionary;
                if (registered == null || !registered.Contains(Enum.ToObject(registeredField.FieldType.GetGenericArguments()[0], EventPurpose)))
                    return;

                object eventList = registered[Enum.ToObject(registeredField.FieldType.GetGenericArguments()[0], EventPurpose)];
                System.Collections.IList list = eventList as System.Collections.IList;
                if (list == null || list.Count == 0)
                    return;

                object metadata = list[0];
                MethodInfo cloneMethod = metadata.GetType().GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo loadMethod = FindLoadMethod(generatorType, metadata.GetType());
                if (cloneMethod == null || loadMethod == null)
                    return;

                object clone = cloneMethod.Invoke(metadata, null);
                object purpose = Enum.ToObject(loadMethod.GetParameters()[1].ParameterType, EventPurpose);
                loadMethod.Invoke(generator, new[] { clone, purpose });
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

        [HarmonyPatch]
        private static class EventGeneration
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                string[] names =
                {
                    "EnhancedProceduralFloorGenerator+<GenerateInner>d__88",
                    "LibraryFloorGenerator+<GenerateInner>d__93"
                };

                foreach (string name in names)
                {
                    Type type = AccessTools.TypeByName(name);
                    MethodInfo method = type == null ? null : AccessTools.Method(type, "MoveNext");
                    if (method != null)
                        yield return method;
                }
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                MethodInfo marker = AccessTools.Method(typeof(MoreMap), nameof(MarkEvent));
                List<CodeInstruction> code = new List<CodeInstruction>(instructions);
                for (int i = 1; i < code.Count; i++)
                {
                    if (code[i].opcode != System.Reflection.Emit.OpCodes.Stfld ||
                        !(code[i].operand is FieldInfo field) ||
                        field.Name != "<eventRoom>5__16" ||
                        code[i - 1].opcode != System.Reflection.Emit.OpCodes.Ldc_I4_1)
                        continue;

                    code[i].opcode = System.Reflection.Emit.OpCodes.Call;
                    code[i].operand = marker;
                }

                return code;
            }
        }
    }
}
