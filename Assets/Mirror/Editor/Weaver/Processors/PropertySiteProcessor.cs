using System;
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    public static class PropertySiteProcessor
    {
        public static void ProcessSitesModule(ModuleDefinition moduleDef)
        {
            DateTime startTime = DateTime.Now;

            //Search through the types
            foreach (TypeDefinition td in moduleDef.Types)
            {
                if (td.IsClass)
                {
                    ProcessSiteClass(td);
                }
            }
            if (Weaver.WeaveLists.generateContainerClass != null)
            {
                moduleDef.Types.Add(Weaver.WeaveLists.generateContainerClass);
                Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.WeaveLists.generateContainerClass);

                foreach (MethodDefinition f in Weaver.WeaveLists.generatedReadFunctions)
                {
                    Weaver.CurrentAssembly.MainModule.ImportReference(f);
                }

                foreach (MethodDefinition f in Weaver.WeaveLists.generatedWriteFunctions)
                {
                    Weaver.CurrentAssembly.MainModule.ImportReference(f);
                }
            }
            Console.WriteLine("  ProcessSitesModule " + moduleDef.Name + " elapsed time:" + (DateTime.Now - startTime));
        }

        static void ProcessSiteClass(TypeDefinition td)
        {
            //Console.WriteLine("    ProcessSiteClass " + td);
            foreach (MethodDefinition md in td.Methods)
            {
                ProcessSiteMethod(td, md);
            }

            foreach (TypeDefinition nested in td.NestedTypes)
            {
                ProcessSiteClass(nested);
            }
        }

        static void ProcessSiteMethod(TypeDefinition td, MethodDefinition md)
        {
            // process all references to replaced members with properties
            //Weaver.DLog(td, "      ProcessSiteMethod " + md);

            if (md.Name == ".cctor" ||
                md.Name == NetworkBehaviourProcessor.ProcessedFunctionName ||
                md.Name.StartsWith(Weaver.InvokeRpcPrefix))
                return;

            if (md.Body != null && md.Body.Instructions != null)
            {
                // TODO move this to NetworkBehaviourProcessor
                ServerClientAttributeProcessor.ProcessMethodAttributes(td, md);

                for (int iCount = 0; iCount < md.Body.Instructions.Count;)
                {
                    Instruction instr = md.Body.Instructions[iCount];
                    iCount += ProcessInstruction(md, instr, iCount);
                }
            }
        }

        // replaces syncvar write access with the NetworkXYZ.get property calls
        static void ProcessInstructionSetterField(MethodDefinition md, Instruction i, FieldDefinition opField)
        {
            // dont replace property call sites in constructors
            if (md.Name == ".ctor")
                return;

            // does it set a field that we replaced?
            if (Weaver.WeaveLists.replacementSetterProperties.TryGetValue(opField, out MethodDefinition replacement))
            {
                //replace with property
                //DLog(td, "    replacing "  + md.Name + ":" + i);
                i.OpCode = OpCodes.Call;
                i.Operand = replacement;
                //DLog(td, "    replaced  "  + md.Name + ":" + i);
            }
        }

        // replaces syncvar read access with the NetworkXYZ.get property calls
        static void ProcessInstructionGetterField(MethodDefinition md, Instruction i, FieldDefinition opField)
        {
            // dont replace property call sites in constructors
            if (md.Name == ".ctor")
                return;

            // does it set a field that we replaced?
            if (Weaver.WeaveLists.replacementGetterProperties.TryGetValue(opField, out MethodDefinition replacement))
            {
                //replace with property
                //DLog(td, "    replacing "  + md.Name + ":" + i);
                i.OpCode = OpCodes.Call;
                i.Operand = replacement;
                //DLog(td, "    replaced  "  + md.Name + ":" + i);
            }
        }

        static int ProcessInstruction(MethodDefinition md, Instruction instr, int iCount)
        {
            if ((instr.OpCode == OpCodes.Call || instr.OpCode == OpCodes.Callvirt)
                && instr.Operand is MethodReference opMethod)
            {
                ProcessInstructionMethod(md, instr, opMethod, iCount);
            }

            if (instr.OpCode == OpCodes.Stfld && instr.Operand is FieldDefinition opFieldst)
            {
                // this instruction sets the value of a field. cache the field reference.
                ProcessInstructionSetterField(md, instr, opFieldst);
            }

            if (instr.OpCode == OpCodes.Ldfld && instr.Operand is FieldDefinition opFieldld)
            {
                // this instruction gets the value of a field. cache the field reference.
                ProcessInstructionGetterField(md, instr, opFieldld);
            }

            if (instr.OpCode == OpCodes.Ldflda && instr.Operand is FieldDefinition opFieldlda)
            {
                // loading a field by reference,  watch out for initobj instruction
                // see https://github.com/vis2k/Mirror/issues/696
                return ProcessInstructionLoadAddress(md, instr, opFieldlda, iCount);
            }

            return 1;
        }

        static int ProcessInstructionLoadAddress(MethodDefinition md, Instruction instr, FieldDefinition opField, int iCount)
        {
            // dont replace property call sites in constructors
            if (md.Name == ".ctor")
                return 1;

            // does it set a field that we replaced?
            if (Weaver.WeaveLists.replacementSetterProperties.TryGetValue(opField, out MethodDefinition replacement))
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

        static void ProcessInstructionMethod(MethodDefinition md, Instruction instr, MethodReference opMethodRef, int iCount)
        {
            if (opMethodRef.Name != "Invoke")
                return;

            // Events use an "Invoke" method to call the delegate.
            // this code replaces the "Invoke" instruction with the generated "Call***" instruction which send the event to the server.
            // but the "Invoke" instruction is called on the event field - where the "call" instruction is not.
            // so the earlier instruction that loads the event field is replaced with a Noop.

            // go backwards until find a ldfld instruction that matches ANY event
            while (iCount > 0)
            {
                iCount -= 1;
                Instruction inst = md.Body.Instructions[iCount];
                if (inst.OpCode == OpCodes.Ldfld)
                {
                    FieldReference opField = inst.Operand as FieldReference;

                    // find replaceEvent with matching name
                    // NOTE: original weaver compared .Name, not just the MethodDefinition,
                    //       that's why we use dict<string,method>.
                    if (Weaver.WeaveLists.replaceEvents.TryGetValue(opField.FullName, out MethodDefinition replacement))
                    {
                        instr.Operand = replacement;
                        inst.OpCode = OpCodes.Nop;
                        return;
                    }
                }
            }
        }
    }
}
