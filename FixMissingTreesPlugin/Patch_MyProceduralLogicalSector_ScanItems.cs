using HarmonyLib;
using Sandbox.Game.WorldEnvironment;
using System.Collections.Generic;

namespace DebugSETreesPlugin
{
    [HarmonyPatch(typeof(MyProceduralLogicalSector), "ScanItems")]

    public class Patch_MyProceduralLogicalSector_ScanItems
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);

            // -            array[num4 - targetLod] = num3 + m_totalSpawned;

            codeMatcher.MatchStartForward(
                CodeMatch.LoadsLocal(),
                CodeMatch.LoadsArgument(),
                CodeMatch.WithOpcodes(new HashSet<System.Reflection.Emit.OpCode> { System.Reflection.Emit.OpCodes.Sub }),
                CodeMatch.LoadsLocal(),
                CodeMatch.LoadsArgument(),
                CodeMatch.LoadsField(typeof(MyProceduralLogicalSector).GetField("m_totalSpawned", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)),
                CodeMatch.WithOpcodes(new HashSet<System.Reflection.Emit.OpCode> { System.Reflection.Emit.OpCodes.Add }),
                CodeMatch.WithOpcodes(new HashSet<System.Reflection.Emit.OpCode> { System.Reflection.Emit.OpCodes.Stelem_I4 })
            );

            // +            array[num4 - targetLod] = num3;

            if (codeMatcher.IsValid)
            {
                codeMatcher.Advance(4);
                codeMatcher.RemoveInstructions(3);
            }

            return codeMatcher.Instructions();
        }
    }
}
