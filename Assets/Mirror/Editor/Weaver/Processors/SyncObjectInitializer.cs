using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    public static class SyncObjectInitializer
    {
        // generates code like:
        // this.InitSyncObject(m_sizes);
        public static void GenerateSyncObjectInitializer(ILProcessor worker, WeaverTypes weaverTypes, FieldDefinition fd)
        {
            // register syncobject in network behaviour
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Ldfld, fd);
            worker.Emit(OpCodes.Call, weaverTypes.InitSyncObjectReference);
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

                return typeRef.Resolve().IsDerivedFrom<SyncObject>();
            }
            catch
            {
                // sometimes this will fail if we reference a weird library that can't be resolved, so we just swallow that exception and return false
            }

            return false;
        }
    }
}
