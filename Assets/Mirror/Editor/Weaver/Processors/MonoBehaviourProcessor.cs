using Mono.CecilX;

namespace Mirror.Weaver
{
    /// <summary>
    /// only shows warnings in case we use SyncVars etc. for MonoBehaviour.
    /// </summary>
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
                    Weaver.Error($"SyncVar {fd.Name} must be inside a NetworkBehaviour.  {td.Name} is not a NetworkBehaviour", fd);

                if (SyncObjectInitializer.ImplementsSyncObject(fd.FieldType))
                {
                    Weaver.Error($"{fd.Name} is a SyncObject and must be inside a NetworkBehaviour.  {td.Name} is not a NetworkBehaviour", fd);
                }
            }
        }

        static void ProcessMethods(TypeDefinition td)
        {
            // find command and RPC functions
            foreach (MethodDefinition md in td.Methods)
            {
                if (md.HasCustomAttribute<CommandAttribute>())
                    Weaver.Error($"Command {md.Name} must be declared inside a NetworkBehaviour", md);
                if (md.HasCustomAttribute<ClientRpcAttribute>())
                    Weaver.Error($"ClientRpc {md.Name} must be declared inside a NetworkBehaviour", md);
                if (md.HasCustomAttribute<TargetRpcAttribute>())
                    Weaver.Error($"TargetRpc {md.Name} must be declared inside a NetworkBehaviour", md);
            }
        }
    }
}
