using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    public static class SyncObjectInitializer
    {
        public static void GenerateSyncObjectInitializer(ILProcessor methodWorker, FieldDefinition fd)
        {
            // call syncobject constructor
            GenerateSyncObjectInstanceInitializer(methodWorker, fd);

            // register syncobject in network behaviour
            GenerateSyncObjectRegistration(methodWorker, fd);
        }

        // generates 'syncListInt = new SyncListInt()' if user didn't do that yet
        static void GenerateSyncObjectInstanceInitializer(ILProcessor ctorWorker, FieldDefinition fd)
        {
            // check the ctor's instructions for an Stfld op-code for this specific sync list field.
            foreach (Instruction ins in ctorWorker.Body.Instructions)
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

            TypeDefinition fieldType = fd.FieldType.Resolve();
            // find ctor with no parameters
            MethodDefinition ctor = fieldType.Methods.FirstOrDefault(x => x.Name == ".ctor" && !x.HasParameters);
            if (ctor == null)
            {
                Weaver.Error($"Can not intialize field {fd.Name} because no default constructor was found. Manually intialize the field (call the constructor) or add constructor without Parameter", fd);
                return;
            }
            MethodReference objectConstructor = Weaver.CurrentAssembly.MainModule.ImportReference(ctor);

            ctorWorker.Append(ctorWorker.Create(OpCodes.Ldarg_0));
            ctorWorker.Append(ctorWorker.Create(OpCodes.Newobj, objectConstructor));
            ctorWorker.Append(ctorWorker.Create(OpCodes.Stfld, fd));
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
        static void GenerateSyncObjectRegistration(ILProcessor methodWorker, FieldDefinition fd)
        {
            methodWorker.Append(methodWorker.Create(OpCodes.Ldarg_0));
            methodWorker.Append(methodWorker.Create(OpCodes.Ldarg_0));
            methodWorker.Append(methodWorker.Create(OpCodes.Ldfld, fd));

            methodWorker.Append(methodWorker.Create(OpCodes.Call, Weaver.InitSyncObjectReference));
        }
    }
}
