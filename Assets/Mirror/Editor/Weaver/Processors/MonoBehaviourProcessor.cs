// this class only shows warnings in case we use SyncVars etc. for MonoBehaviour.
using Mono.CecilX;

namespace Mirror.Weaver
{
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
                foreach (CustomAttribute ca in fd.CustomAttributes)
                {
                    if (ca.AttributeType.FullName == Weaver.SyncVarType.FullName)
                    {
                        Weaver.Error($"[SyncVar] {fd} must be inside a NetworkBehaviour.  {td} is not a NetworkBehaviour");
                    }
                }

                if (SyncObjectInitializer.ImplementsSyncObject(fd.FieldType))
                {
                    Weaver.Error($"{fd} is a SyncObject and must be inside a NetworkBehaviour.  {td} is not a NetworkBehaviour");
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
                        Weaver.Error($"[Command] {md} must be declared inside a NetworkBehaviour");
                    }

                    if (ca.AttributeType.FullName == Weaver.ClientRpcType.FullName)
                    {
                        Weaver.Error($"[ClienRpc] {md} must be declared inside a NetworkBehaviour");
                    }

                    if (ca.AttributeType.FullName == Weaver.TargetRpcType.FullName)
                    {
                        Weaver.Error($"[TargetRpc] {md} must be declared inside a NetworkBehaviour");
                    }

                    string attributeName = ca.Constructor.DeclaringType.ToString();

                    switch (attributeName)
                    {
                        case "Mirror.ServerAttribute":
                            Weaver.Error($"[Server] {md} must be declared inside a NetworkBehaviour");
                            break;
                        case "Mirror.ServerCallbackAttribute":
                            Weaver.Error($"[ServerCallback] {md} must be declared inside a NetworkBehaviour");
                            break;
                        case "Mirror.ClientAttribute":
                            Weaver.Error($"[Client] {md} must be declared inside a NetworkBehaviour");
                            break;
                        case "Mirror.ClientCallbackAttribute":
                            Weaver.Error($"[ClientCallback] {md} must be declared inside a NetworkBehaviour");
                            break;
                    }
                }
            }
        }
    }
}
