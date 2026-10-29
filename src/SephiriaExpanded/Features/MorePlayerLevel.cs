using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaExpanded
{
    internal static class MorePlayerLevel
    {
        private const int ExtendedMaxLevel = 60;
        private static readonly int[] DesignedExperienceTable =
        {
            0, 800, 2600, 4000, 5700, 7400, 9400, 11600, 13800, 16000,
            18400, 20600, 23600, 26600, 29600, 32600, 35600, 38600, 42000, 45600,
            49000, 52600, 56000, 59600, 63800, 68000, 72200, 76600, 80800, 85200,
            89800, 94400, 99000, 103600, 108200, 112800, 117600, 122600, 127400, 132400,
            137200, 142200, 147400, 152600, 157800, 163200, 168800, 174200, 179800, 185200,
            190800, 196600, 202400, 208200, 214200, 220400, 226400, 232600, 238600, 244800
        };
        private static readonly FieldInfo ExpTableField = AccessTools.Field(typeof(LevelController), "ExpTableByLevel");
        private static int[] originalTable;
        private static int[] extendedTable;

        internal static void Refresh()
        {
            if (ExpTableField == null)
                return;

            if (originalTable == null)
                originalTable = ExpTableField.GetValue(null) as int[];

            if (originalTable == null || originalTable.Length >= ExtendedMaxLevel)
                return;

            if (ModSettings.PlayerLevelCapEnabled && extendedTable == null)
                extendedTable = BuildExtendedTable(originalTable);
        }

        internal static int[] GetTable()
        {
            Refresh();
            return ModSettings.PlayerLevelCapEnabled && extendedTable != null
                ? extendedTable
                : originalTable;
        }

        internal static void Restore()
        {
            extendedTable = null;
        }

        private static int[] BuildExtendedTable(int[] source)
        {
            int[] result = (int[])DesignedExperienceTable.Clone();
            if (source != null)
                Array.Copy(source, result, Math.Min(source.Length, result.Length));

            return result;
        }

        private static IEnumerable<CodeInstruction> ReplaceTable(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getter = AccessTools.Method(typeof(MorePlayerLevel), nameof(GetTable));
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldsfld && instruction.operand is FieldInfo field && field == ExpTableField)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = getter;
                }

                yield return instruction;
            }
        }

        [HarmonyPatch(typeof(LevelController), "Awake")]
        private static class AwakePatch
        {
            [HarmonyPostfix]
            private static void After()
            {
                Refresh();
            }
        }

        [HarmonyPatch(typeof(LevelController), "AddExp")]
        private static class AddExpPatch
        {
            [HarmonyPrefix]
            private static void Before()
            {
                Refresh();
            }
        }

        [HarmonyPatch(typeof(LevelController), "LocalAddExp")]
        private static class LocalAddExpPatch
        {
            [HarmonyPrefix]
            private static void Before()
            {
                Refresh();
            }

            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> ReplaceTable(IEnumerable<CodeInstruction> instructions)
            {
                return MorePlayerLevel.ReplaceTable(instructions);
            }
        }

        [HarmonyPatch(typeof(LevelController), "get_Ratio")]
        private static class RatioPatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> ReplaceTable(IEnumerable<CodeInstruction> instructions)
            {
                return MorePlayerLevel.ReplaceTable(instructions);
            }
        }

        [HarmonyPatch(typeof(LevelController), "get_Context")]
        private static class ContextPatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> ReplaceTable(IEnumerable<CodeInstruction> instructions)
            {
                return MorePlayerLevel.ReplaceTable(instructions);
            }
        }

        [HarmonyPatch(typeof(EXPOrb), "LocalCreateExp")]
        private static class ExpOrbPatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> ReplaceTable(IEnumerable<CodeInstruction> instructions)
            {
                return MorePlayerLevel.ReplaceTable(instructions);
            }
        }

        [HarmonyPatch(typeof(ExpShop), "ServerCreateExp")]
        private static class ExpShopPatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> ReplaceTable(IEnumerable<CodeInstruction> instructions)
            {
                return MorePlayerLevel.ReplaceTable(instructions);
            }
        }
    }
}
