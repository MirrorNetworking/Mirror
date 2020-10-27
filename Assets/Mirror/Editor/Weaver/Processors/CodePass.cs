
using System;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    /// <summary>
    /// Algorithm for doing a pass over the code
    /// </summary>
    public static class CodePass
    {
        /// <summary>
        /// Process an instruction,  it can replace the instruction and return new instruction
        /// else it should return the same instruction
        /// </summary>
        /// <param name="md">The method containing the instruction</param>
        /// <param name="instruction">The instruction being processed</param>
        /// <returns>return the same instruction, or replace the instruction and return the replacement</returns>
        public delegate Instruction InstructionProcessor(MethodDefinition md, Instruction instruction);

        /// <summary>
        /// Executes a method for every instruction in a module
        /// </summary>
        /// <param name="module">The module to be passed over</param>
        /// <param name="selector">A predicate that indicates if we should pass over a method or not</param>
        /// <param name="processor">The function that processes each instruction</param>
        public static void ForEachInstruction(ModuleDefinition module, Predicate<MethodDefinition> selector, InstructionProcessor processor)
        {
            foreach (TypeDefinition td in module.Types)
            {
                if (td.IsClass)
                {
                    InstructionPass(td, selector, processor);
                }
            }
        }

        public static void ForEachInstruction(ModuleDefinition module, InstructionProcessor processor) =>
            ForEachInstruction(module, md => true, processor);

        private static void InstructionPass(TypeDefinition td, Predicate<MethodDefinition> selector, InstructionProcessor processor)
        {
            foreach (MethodDefinition md in td.Methods)
            {
                InstructionPass(md, selector, processor);
            }

            foreach (TypeDefinition nested in td.NestedTypes)
            {
                InstructionPass(nested, selector, processor);
            }
        }

        private static void InstructionPass(MethodDefinition md, Predicate<MethodDefinition> selector, InstructionProcessor processor)
        {
            // process all references to replaced members with properties
            if (md.IsAbstract || md.Body == null || md.Body.Instructions == null)
            {
                return;
            }

            if (md.Body.CodeSize> 0 && selector(md))
            {
                Instruction instr = md.Body.Instructions[0];

                while (instr != null)
                {
                    instr = processor(md, instr);
                    instr = instr.Next;
                }
            }
        }
    }
}
