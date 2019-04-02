using System;
using Mono.Cecil;
using Mono.Cecil.Cil;

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
                    ProcessSiteClass(moduleDef, td);
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

        static void ProcessSiteClass(ModuleDefinition moduleDef, TypeDefinition td)
        {
            //Console.WriteLine("    ProcessSiteClass " + td);
            foreach (MethodDefinition md in td.Methods)
            {
                ProcessSiteMethod(moduleDef, td, md);
            }

            foreach (TypeDefinition nested in td.NestedTypes)
            {
                ProcessSiteClass(moduleDef, nested);
            }
        }

        static void ProcessSiteMethod(ModuleDefinition moduleDef, TypeDefinition td, MethodDefinition md)
        {
            // process all references to replaced members with properties
            //Weaver.DLog(td, "      ProcessSiteMethod " + md);

            if (md.Name == ".cctor" ||
                md.Name == NetworkBehaviourProcessor.ProcessedFunctionName ||
                md.Name.StartsWith("CallCmd") ||
                md.Name.StartsWith("InvokeCmd") ||
                md.Name.StartsWith("InvokeRpc") ||
                md.Name.StartsWith("InvokeSyn"))
                return;

            if (md.Body != null && md.Body.Instructions != null)
            {
                // TODO move this to NetworkBehaviourProcessor
                foreach (CustomAttribute attr in md.CustomAttributes)
                {
                    switch (attr.Constructor.DeclaringType.ToString())
                    {
                        case "Mirror.ServerAttribute":
                            InjectServerGuard(moduleDef, td, md, true);
                            break;
                        case "Mirror.ServerCallbackAttribute":
                            InjectServerGuard(moduleDef, td, md, false);
                            break;
                        case "Mirror.ClientAttribute":
                            InjectClientGuard(moduleDef, td, md, true);
                            break;
                        case "Mirror.ClientCallbackAttribute":
                            InjectClientGuard(moduleDef, td, md, false);
                            break;
                    }
                }

                int iCount = 0;
                foreach (Instruction i in md.Body.Instructions)
                {
                    ProcessInstruction(moduleDef, td, md, i, iCount);
                    iCount += 1;
                }
            }
        }

        static void InjectServerGuard(ModuleDefinition moduleDef, TypeDefinition td, MethodDefinition md, bool logWarning)
        {
            if (!Weaver.IsNetworkBehaviour(td))
            {
                Log.Error("[Server] guard on non-NetworkBehaviour script at [" + md.FullName + "]");
                return;
            }
            ILProcessor worker = md.Body.GetILProcessor();
            Instruction top = md.Body.Instructions[0];

            worker.InsertBefore(top, worker.Create(OpCodes.Call, Weaver.NetworkServerGetActive));
            worker.InsertBefore(top, worker.Create(OpCodes.Brtrue, top));
            if (logWarning)
            {
                worker.InsertBefore(top, worker.Create(OpCodes.Ldstr, "[Server] function '" + md.FullName + "' called on client"));
                worker.InsertBefore(top, worker.Create(OpCodes.Call, Weaver.logWarningReference));
            }
            InjectGuardParameters(md, worker, top);
            InjectGuardReturnValue(md, worker, top);
            worker.InsertBefore(top, worker.Create(OpCodes.Ret));
        }

        static void InjectClientGuard(ModuleDefinition moduleDef, TypeDefinition td, MethodDefinition md, bool logWarning)
        {
            if (!Weaver.IsNetworkBehaviour(td))
            {
                Log.Error("[Client] guard on non-NetworkBehaviour script at [" + md.FullName + "]");
                return;
            }
            ILProcessor worker = md.Body.GetILProcessor();
            Instruction top = md.Body.Instructions[0];

            worker.InsertBefore(top, worker.Create(OpCodes.Call, Weaver.NetworkClientGetActive));
            worker.InsertBefore(top, worker.Create(OpCodes.Brtrue, top));
            if (logWarning)
            {
                worker.InsertBefore(top, worker.Create(OpCodes.Ldstr, "[Client] function '" + md.FullName + "' called on server"));
                worker.InsertBefore(top, worker.Create(OpCodes.Call, Weaver.logWarningReference));
            }

            InjectGuardParameters(md, worker, top);
            InjectGuardReturnValue(md, worker, top);
            worker.InsertBefore(top, worker.Create(OpCodes.Ret));
        }

        // replaces syncvar write access with the NetworkXYZ.get property calls
        static void ProcessInstructionSetterField(TypeDefinition td, MethodDefinition md, Instruction i, FieldDefinition opField)
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
        static void ProcessInstructionGetterField(TypeDefinition td, MethodDefinition md, Instruction i, FieldDefinition opField)
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

        static void ProcessInstruction(ModuleDefinition moduleDef, TypeDefinition td, MethodDefinition md, Instruction i, int iCount)
        {
            if (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt)
            {
                if (i.Operand is MethodReference opMethod)
                {
                    ProcessInstructionMethod(moduleDef, td, md, i, opMethod, iCount);
                }
            }

            if (i.OpCode == OpCodes.Stfld)
            {
                // this instruction sets the value of a field. cache the field reference.
                if (i.Operand is FieldDefinition opField)
                {
                    ProcessInstructionSetterField(td, md, i, opField);
                }
            }

            if (i.OpCode == OpCodes.Ldfld)
            {
                // this instruction gets the value of a field. cache the field reference.
                if (i.Operand is FieldDefinition opField)
                {
                    ProcessInstructionGetterField(td, md, i, opField);
                }
            }
        }

        static void ProcessInstructionMethod(ModuleDefinition moduleDef, TypeDefinition td, MethodDefinition md, Instruction instr, MethodReference opMethodRef, int iCount)
        {
            //DLog(td, "ProcessInstructionMethod " + opMethod.Name);
            if (opMethodRef.Name == "Invoke")
            {
                // Events use an "Invoke" method to call the delegate.
                // this code replaces the "Invoke" instruction with the generated "Call***" instruction which send the event to the server.
                // but the "Invoke" instruction is called on the event field - where the "call" instruction is not.
                // so the earlier instruction that loads the event field is replaced with a Noop.

                // go backwards until find a ldfld instruction that matches ANY event
                bool found = false;
                while (iCount > 0 && !found)
                {
                    iCount -= 1;
                    Instruction inst = md.Body.Instructions[iCount];
                    if (inst.OpCode == OpCodes.Ldfld)
                    {
                        FieldReference opField = inst.Operand as FieldReference;

                        // find replaceEvent with matching name
                        // NOTE: original weaver compared .Name, not just the MethodDefinition,
                        //       that's why we use dict<string,method>.
                        // TODO maybe replaceEvents[md] would work too?
                        if (Weaver.WeaveLists.replaceEvents.TryGetValue(opField.Name, out MethodDefinition replacement))
                        {
                            instr.Operand = replacement;
                            inst.OpCode = OpCodes.Nop;
                            found = true;
                        }
                    }
                }
            }
            else
            {
                // should it be replaced?
                // NOTE: original weaver compared .FullName, not just the MethodDefinition,
                //       that's why we use dict<string,method>.
                // TODO maybe replaceMethods[md] would work too?
                if (Weaver.WeaveLists.replaceMethods.TryGetValue(opMethodRef.FullName, out MethodDefinition replacement))
                {
                    //DLog(td, "    replacing "  + md.Name + ":" + i);
                    instr.Operand = replacement;
                    //DLog(td, "    replaced  "  + md.Name + ":" + i);
                }
            }
        }


        // this is required to early-out from a function with "ref" or "out" parameters
        static void InjectGuardParameters(MethodDefinition md, ILProcessor worker, Instruction top)
        {
            int offset = md.Resolve().IsStatic ? 0 : 1;
            for (int index = 0; index < md.Parameters.Count; index++)
            {
                ParameterDefinition param = md.Parameters[index];
                if (param.IsOut)
                {
                    TypeReference elementType = param.ParameterType.GetElementType();
                    if (elementType.IsPrimitive)
                    {
                        worker.InsertBefore(top, worker.Create(OpCodes.Ldarg, index + offset));
                        worker.InsertBefore(top, worker.Create(OpCodes.Ldc_I4_0));
                        worker.InsertBefore(top, worker.Create(OpCodes.Stind_I4));
                    }
                    else
                    {
                        md.Body.Variables.Add(new VariableDefinition(elementType));
                        md.Body.InitLocals = true;

                        worker.InsertBefore(top, worker.Create(OpCodes.Ldarg, index + offset));
                        worker.InsertBefore(top, worker.Create(OpCodes.Ldloca_S, (byte)(md.Body.Variables.Count - 1)));
                        worker.InsertBefore(top, worker.Create(OpCodes.Initobj, elementType));
                        worker.InsertBefore(top, worker.Create(OpCodes.Ldloc, md.Body.Variables.Count - 1));
                        worker.InsertBefore(top, worker.Create(OpCodes.Stobj, elementType));
                    }
                }
            }
        }

        // this is required to early-out from a function with a return value.
        static void InjectGuardReturnValue(MethodDefinition md, ILProcessor worker, Instruction top)
        {
            if (md.ReturnType.FullName != Weaver.voidType.FullName)
            {
                if (md.ReturnType.IsPrimitive)
                {
                    worker.InsertBefore(top, worker.Create(OpCodes.Ldc_I4_0));
                }
                else
                {
                    md.Body.Variables.Add(new VariableDefinition(md.ReturnType));
                    md.Body.InitLocals = true;

                    worker.InsertBefore(top, worker.Create(OpCodes.Ldloca_S, (byte)(md.Body.Variables.Count - 1)));
                    worker.InsertBefore(top, worker.Create(OpCodes.Initobj, md.ReturnType));
                    worker.InsertBefore(top, worker.Create(OpCodes.Ldloc, md.Body.Variables.Count - 1));
                }
            }
        }
    }
}
