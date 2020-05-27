using System.Linq;
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    public static class SyncObjectInitializer
    {
        public static void GenerateSyncObjectInitializer(ILProcessor worker, FieldDefinition fd)
        {
            // call syncobject constructor
            GenerateSyncObjectInstanceInitializer(worker, fd);

            // register syncobject in network behaviour
            GenerateSyncObjectRegistration(worker, fd);
        }

        // generates 'syncListInt = new SyncListInt()' if user didn't do that yet
        static void GenerateSyncObjectInstanceInitializer(ILProcessor worker, FieldDefinition fd)
        {
            // check the ctor's instructions for an Stfld op-code for this specific sync list field.
            foreach (Instruction ins in worker.Body.Instructions)
            {
                if (ins.OpCode.Code == Code.Stfld)
                {
                    FieldDefinition field = (FieldDefinition)ins.Operand;
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

            TypeDefinition fieldType = fd.FieldType.Resolve();
            // find ctor with no parameters
            MethodDefinition ctor = fieldType.Methods.FirstOrDefault(x => x.Name == ".ctor" && !x.HasParameters);
            if (ctor == null)
            {
                Weaver.Error($"Can not initialize field {fd.Name} because no default constructor was found. Manually initialize the field (call the constructor) or add constructor without Parameter", fd);
                return;
            }
            MethodReference objectConstructor = Weaver.CurrentAssembly.MainModule.ImportReference(ctor);

            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Newobj, objectConstructor));
            worker.Append(worker.Create(OpCodes.Stfld, fd));
        }

        public static bool ImplementsSyncObject(TypeReference typeRef)
        {
            try
            {
                // value types cant inherit from SyncObject
                if (typeRef.IsValueType)
                {
                    return false;
                }

                return typeRef.Resolve().ImplementsInterface(Weaver.SyncObjectType);
            }
            catch
            {
                // sometimes this will fail if we reference a weird library that can't be resolved, so we just swallow that exception and return false
            }

            return false;
        }

        /*
            // generates code like:
            this.InitSyncObject(m_sizes);
        */
        static void GenerateSyncObjectRegistration(ILProcessor worker, FieldDefinition fd)
        {
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldfld, fd));

            worker.Append(worker.Create(OpCodes.Call, Weaver.InitSyncObjectReference));
        }
    }
}
