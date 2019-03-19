// SyncList code
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    public static class SyncListInitializer
    {
        // generates 'syncListInt = new SyncListInt()' if user didn't do that yet
        public static void GenerateSyncListInstanceInitializer(ILProcessor ctorWorker, FieldDefinition fd)
        {
            // check the ctor's instructions for an Stfld op-code for this specific sync list field.
            foreach (var ins in ctorWorker.Body.Instructions)
            {
                if (ins.OpCode.Code == Code.Stfld)
                {
                    var field = (FieldDefinition)ins.Operand;
                    if (field.DeclaringType == fd.DeclaringType && field.Name == fd.Name)
                    {
                        // Already initialized by the user in the field definition, e.g:
                        // public SyncListInt Foo = new SyncListInt();
                        return;
                    }
                }
            }

            // Not initialized by the user in the field definition, e.g:
            // public SyncListInt Foo;
            MethodReference listCtor;
            try
            {
                listCtor = Weaver.CurrentAssembly.MainModule.ImportReference(fd.FieldType.Resolve().Methods.First<MethodDefinition>(x => x.Name == ".ctor" && !x.HasParameters));
            }
            catch (Exception)
            {
                Weaver.Error("Missing parameter-less constructor for:" + fd.FieldType.Name);
                return;
            }

            ctorWorker.Append(ctorWorker.Create(OpCodes.Ldarg_0));
            ctorWorker.Append(ctorWorker.Create(OpCodes.Newobj, listCtor));
            ctorWorker.Append(ctorWorker.Create(OpCodes.Stfld, fd));
        }
    }
}
