// SyncObject code
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    public static class SyncObjectInitializer
    {
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
        public static void GenerateSyncObjectInitializer(ILProcessor methodWorker, FieldReference fd)
        {
            methodWorker.Append(methodWorker.Create(OpCodes.Ldarg_0));
            methodWorker.Append(methodWorker.Create(OpCodes.Ldarg_0));
            methodWorker.Append(methodWorker.Create(OpCodes.Ldfld, fd));

            methodWorker.Append(methodWorker.Create(OpCodes.Call, Weaver.InitSyncObjectReference));
        }
    }
}
