
using Mono.Cecil;

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
                if (fd.HasCustomAttribute<Mirror.SyncVarAttribute>())
                    Weaver.Error($"SyncVar {fd.Name} must be inside a NetworkBehaviour.  {td.Name} is not a NetworkBehaviour", fd);

                if (SyncObjectInitializer.ImplementsSyncObject(fd.FieldType))
                {
                    Weaver.Error($"{fd.Name} is a SyncObject and must be inside a NetworkBehaviour.  {td.Name} is not a NetworkBehaviour", fd);
                }
            }
        }

        static void ProcessMethods(TypeDefinition td)
        {
            // find ServerRpc and RPC functions
            foreach (MethodDefinition md in td.Methods)
            {
                foreach (CustomAttribute ca in md.CustomAttributes)
                {
                    if (ca.AttributeType.Is<Mirror.ServerRpcAttribute>())
                    {
                        Weaver.Error($"ServerRpc {md.Name} must be declared inside a NetworkBehaviour", md);
                    }

                    if (ca.AttributeType.Is<Mirror.ClientRpcAttribute>())
                    {
                        Weaver.Error($"ClientRpc {md.Name} must be declared inside a NetworkBehaviour", md);
                    }

                    if (ca.AttributeType.Is<Mirror.ClientAttribute>())
                    {
                        Weaver.Error($"Client method {md.Name} must be declared inside a NetworkBehaviour", md);                        
                    }

                    if (ca.AttributeType.Is<Mirror.ServerAttribute>())
                    {
                        Weaver.Error($"Server method {md.Name} must be declared inside a NetworkBehaviour", md);
                    }
                }
            }
        }
    }
}
