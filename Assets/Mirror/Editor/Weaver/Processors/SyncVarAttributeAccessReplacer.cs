// [SyncVar] int health;
// is replaced with:
// public int Networkhealth { get; set; } properties.
// this class processes all access to 'health' and replaces it with 'Networkhealth'
using System;
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    public static class SyncVarAttributeAccessReplacer
    {
        // process the module
        public static void Process(ModuleDefinition moduleDef, SyncVarAccessLists syncVarAccessLists)
        {
            DateTime startTime = DateTime.Now;

            // process all classes in this module
            foreach (TypeDefinition td in moduleDef.Types)
            {
                if (td.IsClass)
                {
                    ProcessClass(syncVarAccessLists, td);
                }
            }

            Console.WriteLine($"  ProcessSitesModule {moduleDef.Name} elapsed time:{(DateTime.Now - startTime)}");
        }

        static void ProcessClass(SyncVarAccessLists syncVarAccessLists, TypeDefinition td)
        {
            //Console.WriteLine($"    ProcessClass {td}");

            // process all methods in this class
            foreach (MethodDefinition md in td.Methods)
            {
                ProcessMethod(syncVarAccessLists, md);
            }

            // processes all nested classes in this class recursively
            foreach (TypeDefinition nested in td.NestedTypes)
            {
                ProcessClass(syncVarAccessLists, nested);
            }
        }

        static void ProcessMethod(SyncVarAccessLists syncVarAccessLists, MethodDefinition md)
        {
            // process all references to replaced members with properties
            //Log.Warning($"      ProcessSiteMethod {md}");

            // skip static constructor, "MirrorProcessed", "InvokeUserCode_"
            if (md.Name == ".cctor" ||
                md.Name == NetworkBehaviourProcessor.ProcessedFunctionName ||
                md.Name.StartsWith(Weaver.InvokeRpcPrefix))
                return;

            // skip abstract
            if (md.IsAbstract)
            {
                return;
            }

            // go through all instructions of this method
            if (md.Body != null && md.Body.Instructions != null)
            {
                for (int i = 0; i < md.Body.Instructions.Count;)
                {
                    Instruction instr = md.Body.Instructions[i];
                    i += ProcessInstruction(syncVarAccessLists, md, instr, i);
                }
            }
        }

        static int ProcessInstruction(SyncVarAccessLists syncVarAccessLists, MethodDefinition md, Instruction instr, int iCount)
        {
            // stfld (sets value of a field)?
            if (instr.OpCode == OpCodes.Stfld && instr.Operand is FieldDefinition opFieldst)
            {
                ProcessSetInstruction(syncVarAccessLists, md, instr, opFieldst);
            }

            // ldfld (load value of a field)?
            if (instr.OpCode == OpCodes.Ldfld && instr.Operand is FieldDefinition opFieldld)
            {
                // this instruction gets the value of a field. cache the field reference.
                ProcessGetInstruction(syncVarAccessLists, md, instr, opFieldld);
            }

            // ldflda (load field address aka reference)
            if (instr.OpCode == OpCodes.Ldflda && instr.Operand is FieldDefinition opFieldlda)
            {
                // watch out for initobj instruction
                // see https://github.com/vis2k/Mirror/issues/696
                return ProcessLoadAddressInstruction(syncVarAccessLists, md, instr, opFieldlda, iCount);
            }

            // we processed one instruction (instr)
            return 1;
        }

        // replaces syncvar write access with the NetworkXYZ.set property calls
        static void ProcessSetInstruction(SyncVarAccessLists syncVarAccessLists, MethodDefinition md, Instruction i, FieldDefinition opField)
        {
            // don't replace property call sites in constructors
            if (md.Name == ".ctor")
                return;

            // does it set a field that we replaced?
            if (syncVarAccessLists.replacementSetterProperties.TryGetValue(opField, out MethodDefinition replacement))
            {
                //replace with property
                //Log.Warning($"    replacing {md.Name}:{i}", opField);
                i.OpCode = OpCodes.Call;
                i.Operand = replacement;
                //Log.Warning($"    replaced {md.Name}:{i}", opField);
            }
        }

        // replaces syncvar read access with the NetworkXYZ.get property calls
        static void ProcessGetInstruction(SyncVarAccessLists syncVarAccessLists, MethodDefinition md, Instruction i, FieldDefinition opField)
        {
            // don't replace property call sites in constructors
            if (md.Name == ".ctor")
                return;

            // does it set a field that we replaced?
            if (syncVarAccessLists.replacementGetterProperties.TryGetValue(opField, out MethodDefinition replacement))
            {
                //replace with property
                //Log.Warning($"    replacing {md.Name}:{i}");
                i.OpCode = OpCodes.Call;
                i.Operand = replacement;
                //Log.Warning($"    replaced {md.Name}:{i}");
            }
        }

        static int ProcessLoadAddressInstruction(SyncVarAccessLists syncVarAccessLists, MethodDefinition md, Instruction instr, FieldDefinition opField, int iCount)
        {
            // don't replace property call sites in constructors
            if (md.Name == ".ctor")
                return 1;

            // does it set a field that we replaced?
            if (syncVarAccessLists.replacementSetterProperties.TryGetValue(opField, out MethodDefinition replacement))
            {
                // we have a replacement for this property
                // is the next instruction a initobj?
                Instruction nextInstr = md.Body.Instructions[iCount + 1];

                if (nextInstr.OpCode == OpCodes.Initobj)
                {
                    // we need to replace this code with:
                    //     var tmp = new MyStruct();
                    //     this.set_Networkxxxx(tmp);
                    ILProcessor worker = md.Body.GetILProcessor();
                    VariableDefinition tmpVariable = new VariableDefinition(opField.FieldType);
                    md.Body.Variables.Add(tmpVariable);

                    worker.InsertBefore(instr, worker.Create(OpCodes.Ldloca, tmpVariable));
                    worker.InsertBefore(instr, worker.Create(OpCodes.Initobj, opField.FieldType));
                    worker.InsertBefore(instr, worker.Create(OpCodes.Ldloc, tmpVariable));
                    worker.InsertBefore(instr, worker.Create(OpCodes.Call, replacement));

                    worker.Remove(instr);
                    worker.Remove(nextInstr);
                    return 4;
                }
            }

            return 1;
        }
    }
}
