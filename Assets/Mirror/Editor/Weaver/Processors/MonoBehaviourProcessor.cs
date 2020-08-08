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
                if (fd.HasCustomAttribute(WeaverTypes.SyncVarType))
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
                foreach (CustomAttribute ca in md.CustomAttributes)
                {
                    if (ca.AttributeType.FullName == WeaverTypes.CommandType.FullName)
                    {
                        Weaver.Error($"Command {md.Name} must be declared inside a NetworkBehaviour", md);
                    }

                    if (ca.AttributeType.FullName == WeaverTypes.ClientRpcType.FullName)
                    {
                        Weaver.Error($"ClientRpc {md.Name} must be declared inside a NetworkBehaviour", md);
                    }

                    if (ca.AttributeType.FullName == WeaverTypes.TargetRpcType.FullName)
                    {
                        Weaver.Error($"TargetRpc {md.Name} must be declared inside a NetworkBehaviour", md);
                    }
                }
            }
        }
    }
}
