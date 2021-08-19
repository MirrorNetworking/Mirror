using System;
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    public static class PropertySiteProcessor
    {
        public static void Process(ModuleDefinition moduleDef, WeaverLists weaverLists)
        {
            DateTime startTime = DateTime.Now;

            //Search through the types
            foreach (TypeDefinition td in moduleDef.Types)
            {
                if (td.IsClass)
                {
                    ProcessSiteClass(weaverLists, td);
                }
            }

            Console.WriteLine("  ProcessSitesModule " + moduleDef.Name + " elapsed time:" + (DateTime.Now - startTime));
        }

        static void ProcessSiteClass(WeaverLists weaverLists, TypeDefinition td)
        {
            //Console.WriteLine("    ProcessSiteClass " + td);
            foreach (MethodDefinition md in td.Methods)
            {
                ProcessSiteMethod(weaverLists, md);
            }

            foreach (TypeDefinition nested in td.NestedTypes)
            {
                ProcessSiteClass(weaverLists, nested);
            }
        }

        static void ProcessSiteMethod(WeaverLists weaverLists, MethodDefinition md)
        {
            // process all references to replaced members with properties
            //Weaver.DLog(td, "      ProcessSiteMethod " + md);

            if (md.Name == ".cctor" ||
                md.Name == NetworkBehaviourProcessor.ProcessedFunctionName ||
                md.Name.StartsWith(Weaver.InvokeRpcPrefix))
                return;

            if (md.IsAbstract)
            {
                return;
            }

            if (md.Body != null && md.Body.Instructions != null)
            {
                for (int iCount = 0; iCount < md.Body.Instructions.Count;)
                {
                    Instruction instr = md.Body.Instructions[iCount];
                    iCount += ProcessInstruction(weaverLists, md, instr, iCount);
                }
            }
        }

        // replaces syncvar write access with the NetworkXYZ.get property calls
        static void ProcessInstructionSetterField(WeaverLists weaverLists, MethodDefinition md, Instruction i, FieldDefinition opField)
        {
            // don't replace property call sites in constructors
            if (md.Name == ".ctor")
                return;

            // does it set a field that we replaced?
            if (weaverLists.replacementSetterProperties.TryGetValue(opField, out MethodDefinition replacement))
            {
                //replace with property
                //DLog(td, "    replacing "  + md.Name + ":" + i);
                i.OpCode = OpCodes.Call;
                i.Operand = replacement;
                //DLog(td, "    replaced  "  + md.Name + ":" + i);
            }
        }

        // replaces syncvar read access with the NetworkXYZ.get property calls
        static void ProcessInstructionGetterField(WeaverLists weaverLists, MethodDefinition md, Instruction i, FieldDefinition opField)
        {
            // don't replace property call sites in constructors
            if (md.Name == ".ctor")
                return;

            // does it set a field that we replaced?
            if (weaverLists.replacementGetterProperties.TryGetValue(opField, out MethodDefinition replacement))
            {
                //replace with property
                //DLog(td, "    replacing "  + md.Name + ":" + i);
                i.OpCode = OpCodes.Call;
                i.Operand = replacement;
                //DLog(td, "    replaced  "  + md.Name + ":" + i);
            }
        }

        static int ProcessInstruction(WeaverLists weaverLists, MethodDefinition md, Instruction instr, int iCount)
        {
            if (instr.OpCode == OpCodes.Stfld && instr.Operand is FieldDefinition opFieldst)
            {
                // this instruction sets the value of a field. cache the field reference.
                ProcessInstructionSetterField(weaverLists, md, instr, opFieldst);
            }

            if (instr.OpCode == OpCodes.Ldfld && instr.Operand is FieldDefinition opFieldld)
            {
                // this instruction gets the value of a field. cache the field reference.
                ProcessInstructionGetterField(weaverLists, md, instr, opFieldld);
            }

            if (instr.OpCode == OpCodes.Ldflda && instr.Operand is FieldDefinition opFieldlda)
            {
                // loading a field by reference,  watch out for initobj instruction
                // see https://github.com/vis2k/Mirror/issues/696
                return ProcessInstructionLoadAddress(weaverLists, md, instr, opFieldlda, iCount);
            }

            return 1;
        }

        static int ProcessInstructionLoadAddress(WeaverLists weaverLists, MethodDefinition md, Instruction instr, FieldDefinition opField, int iCount)
        {
            // don't replace property call sites in constructors
            if (md.Name == ".ctor")
                return 1;

            // does it set a field that we replaced?
            if (weaverLists.replacementSetterProperties.TryGetValue(opField, out MethodDefinition replacement))
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
