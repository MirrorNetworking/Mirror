using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    public class PropertySiteProcessor
    {
        // setter functions that replace [SyncVar] member variable references. dict<field, replacement>
        public Dictionary<FieldDefinition, MethodDefinition> Setters = new Dictionary<FieldDefinition, MethodDefinition>();
        // getter functions that replace [SyncVar] member variable references. dict<field, replacement>
        public Dictionary<FieldDefinition, MethodDefinition> Getters = new Dictionary<FieldDefinition, MethodDefinition>();

        public void Process(ModuleDefinition moduleDef)
        {
            DateTime startTime = DateTime.Now;

            // replace all field access with property access for syncvars
            CodePass.ForEachInstruction(moduleDef, WeavedMethods, ProcessInstruction);

            Console.WriteLine("  ProcessSitesModule " + moduleDef.Name + " elapsed time:" + (DateTime.Now - startTime));
        }

        private static bool WeavedMethods(MethodDefinition md) =>
                        md.Name != ".cctor" &&
                        md.Name != NetworkBehaviourProcessor.ProcessedFunctionName &&
                        !md.Name.StartsWith(RpcProcessor.InvokeRpcPrefix) &&
                        !md.IsConstructor;

        // replaces syncvar write access with the NetworkXYZ.get property calls
        void ProcessInstructionSetterField(Instruction i, FieldDefinition opField)
        {
            // does it set a field that we replaced?
            if (Setters.TryGetValue(opField, out MethodDefinition replacement))
            {
                //replace with property
                i.OpCode = OpCodes.Call;
                i.Operand = replacement;
            }
        }

        // replaces syncvar read access with the NetworkXYZ.get property calls
        void ProcessInstructionGetterField(Instruction i, FieldDefinition opField)
        {
            // does it set a field that we replaced?
            if (Getters.TryGetValue(opField, out MethodDefinition replacement))
            {
                //replace with property
                i.OpCode = OpCodes.Call;
                i.Operand = replacement;
            }
        }

        Instruction ProcessInstruction(MethodDefinition md, Instruction instr, SequencePoint sequencePoint)
        {
            if (instr.OpCode == OpCodes.Stfld && instr.Operand is FieldDefinition opFieldst)
            {
                // this instruction sets the value of a field. cache the field reference.
                ProcessInstructionSetterField(instr, opFieldst);
            }

            if (instr.OpCode == OpCodes.Ldfld && instr.Operand is FieldDefinition opFieldld)
            {
                // this instruction gets the value of a field. cache the field reference.
                ProcessInstructionGetterField(instr, opFieldld);
            }

            if (instr.OpCode == OpCodes.Ldflda && instr.Operand is FieldDefinition opFieldlda)
            {
                // loading a field by reference,  watch out for initobj instruction
                // see https://github.com/vis2k/Mirror/issues/696
                return ProcessInstructionLoadAddress(md, instr, opFieldlda);
            }

            return instr;
        }

        Instruction ProcessInstructionLoadAddress(MethodDefinition md, Instruction instr, FieldDefinition opField)
        {
            // does it set a field that we replaced?
            if (Setters.TryGetValue(opField, out MethodDefinition replacement))
            {
                // we have a replacement for this property
                // is the next instruction a initobj?
                Instruction nextInstr = instr.Next;

                if (nextInstr.OpCode == OpCodes.Initobj)
                {
                    // we need to replace this code with:
                    //     var tmp = new MyStruct();
                    //     this.set_Networkxxxx(tmp);
                    ILProcessor worker = md.Body.GetILProcessor();
                    VariableDefinition tmpVariable = md.AddLocal(opField.FieldType);

                    worker.InsertBefore(instr, worker.Create(OpCodes.Ldloca, tmpVariable));
                    worker.InsertBefore(instr, worker.Create(OpCodes.Initobj, opField.FieldType));
                    worker.InsertBefore(instr, worker.Create(OpCodes.Ldloc, tmpVariable));
                    Instruction newInstr = worker.Create(OpCodes.Call, replacement);
                    worker.InsertBefore(instr, newInstr);

                    worker.Remove(instr);
                    worker.Remove(nextInstr);

                    return newInstr;
                }
            }

            return instr;
        }
    }
}
