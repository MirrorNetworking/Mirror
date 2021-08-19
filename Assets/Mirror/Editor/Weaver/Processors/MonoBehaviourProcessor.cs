using Mono.CecilX;

namespace Mirror.Weaver
{
    // only shows warnings in case we use SyncVars etc. for MonoBehaviour.
    static class MonoBehaviourProcessor
    {
        public static void Process(TypeDefinition td)
        {
            ProcessSyncVars(td);
            ProcessMethods(td);
        }

        static void ProcessSyncVars(TypeDefinition td)
        {
            // find syncvars
            foreach (FieldDefinition fd in td.Fields)
            {
                if (fd.HasCustomAttribute<SyncVarAttribute>())
                {
                    Weaver.Log.Error($"SyncVar {fd.Name} must be inside a NetworkBehaviour.  {td.Name} is not a NetworkBehaviour", fd);
                    Weaver.WeavingFailed = true;
                }

                if (SyncObjectInitializer.ImplementsSyncObject(fd.FieldType))
                {
                    Weaver.Log.Error($"{fd.Name} is a SyncObject and must be inside a NetworkBehaviour.  {td.Name} is not a NetworkBehaviour", fd);
                    Weaver.WeavingFailed = true;
                }
            }
        }

        static void ProcessMethods(TypeDefinition td)
        {
            // find command and RPC functions
            foreach (MethodDefinition md in td.Methods)
            {
                if (md.HasCustomAttribute<CommandAttribute>())
                {
                    Weaver.Log.Error($"Command {md.Name} must be declared inside a NetworkBehaviour", md);
                    Weaver.WeavingFailed = true;
                }
                if (md.HasCustomAttribute<ClientRpcAttribute>())
                {
                    Weaver.Log.Error($"ClientRpc {md.Name} must be declared inside a NetworkBehaviour", md);
                    Weaver.WeavingFailed = true;
                }
                if (md.HasCustomAttribute<TargetRpcAttribute>())
                {
                    Weaver.Log.Error($"TargetRpc {md.Name} must be declared inside a NetworkBehaviour", md);
                    Weaver.WeavingFailed = true;
                }
            }
        }
    }
}
