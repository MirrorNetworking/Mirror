using Mono.CecilX;

namespace Mirror.Weaver
{
    // only shows warnings in case we use SyncVars etc. for MonoBehaviour.
    static class MonoBehaviourProcessor
    {
        public static void Process(Logger Log, TypeDefinition td, ref bool WeavingFailed)
        {
            ProcessSyncVars(Log, td, ref WeavingFailed);
            ProcessMethods(Log, td, ref WeavingFailed);
        }

        static void ProcessSyncVars(Logger Log, TypeDefinition td, ref bool WeavingFailed)
        {
            // find syncvars
            foreach (FieldDefinition fd in td.Fields)
            {
                if (fd.HasCustomAttribute<SyncVarAttribute>())
                {
                    Log.Error($"SyncVar {fd.Name} must be inside a NetworkBehaviour.  {td.Name} is not a NetworkBehaviour", fd);
                    WeavingFailed = true;
                }

                if (SyncObjectInitializer.ImplementsSyncObject(fd.FieldType))
                {
                    Log.Error($"{fd.Name} is a SyncObject and must be inside a NetworkBehaviour.  {td.Name} is not a NetworkBehaviour", fd);
                    WeavingFailed = true;
                }
            }
        }

        static void ProcessMethods(Logger Log, TypeDefinition td, ref bool WeavingFailed)
        {
            // find command and RPC functions
            foreach (MethodDefinition md in td.Methods)
            {
                if (md.HasCustomAttribute<CommandAttribute>())
                {
                    Log.Error($"Command {md.Name} must be declared inside a NetworkBehaviour", md);
                    WeavingFailed = true;
                }
                if (md.HasCustomAttribute<ClientRpcAttribute>())
                {
                    Log.Error($"ClientRpc {md.Name} must be declared inside a NetworkBehaviour", md);
                    WeavingFailed = true;
                }
                if (md.HasCustomAttribute<TargetRpcAttribute>())
                {
                    Log.Error($"TargetRpc {md.Name} must be declared inside a NetworkBehaviour", md);
                    WeavingFailed = true;
                }
            }
        }
    }
}
