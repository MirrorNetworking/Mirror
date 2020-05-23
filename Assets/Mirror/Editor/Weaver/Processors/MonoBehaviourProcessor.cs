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
                if (fd.HasCustomAttribute(Weaver.SyncVarType))
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
                    if (ca.AttributeType.FullName == Weaver.CommandType.FullName)
                    {
                        Weaver.Error($"Command {md.Name} must be declared inside a NetworkBehaviour", md);
                    }

                    if (ca.AttributeType.FullName == Weaver.ClientRpcType.FullName)
                    {
                        Weaver.Error($"ClientRpc {md.Name} must be declared inside a NetworkBehaviour", md);
                    }

                    if (ca.AttributeType.FullName == Weaver.TargetRpcType.FullName)
                    {
                        Weaver.Error($"TargetRpc {md.Name} must be declared inside a NetworkBehaviour", md);
                    }

                    string attributeName = ca.Constructor.DeclaringType.ToString();

                    switch (attributeName)
                    {
                        case "Mirror.ServerAttribute":
                            Weaver.Error($"Server method {md.Name} must be declared inside a NetworkBehaviour", md);
                            break;
                        case "Mirror.ServerCallbackAttribute":
                            Weaver.Error($"ServerCallback method {md.Name} must be declared inside a NetworkBehaviour", md);
                            break;
                        case "Mirror.ClientAttribute":
                            Weaver.Error($"Client method {md.Name} must be declared inside a NetworkBehaviour", md);
                            break;
                        case "Mirror.ClientCallbackAttribute":
                            Weaver.Error($"ClientCallback method {md.Name} must be declared inside a NetworkBehaviour", md);
                            break;
                    }
                }
            }
        }
    }
}
