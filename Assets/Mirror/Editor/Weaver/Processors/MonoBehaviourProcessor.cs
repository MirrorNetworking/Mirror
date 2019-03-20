// this class only shows warnings in case we use SyncVars etc. for MonoBehaviour.
using Mono.Cecil;

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
                        Weaver.Error("Script " + td.FullName + " uses [SyncVar] " + fd.Name + " but is not a NetworkBehaviour.");
                    }
                }

                if (SyncObjectInitializer.ImplementsSyncObject(fd.FieldType))
                {
                    Weaver.Error(string.Format("Script {0} defines field {1} with type {2}, but it's not a NetworkBehaviour", td.FullName, fd.Name, Helpers.PrettyPrintType(fd.FieldType)));
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
                        Weaver.Error("Script " + td.FullName + " uses [Command] " + md.Name + " but is not a NetworkBehaviour.");
                    }

                    if (ca.AttributeType.FullName == Weaver.ClientRpcType.FullName)
                    {
                        Weaver.Error("Script " + td.FullName + " uses [ClientRpc] " + md.Name + " but is not a NetworkBehaviour.");
                    }

                    if (ca.AttributeType.FullName == Weaver.TargetRpcType.FullName)
                    {
                        Weaver.Error("Script " + td.FullName + " uses [TargetRpc] " + md.Name + " but is not a NetworkBehaviour.");
                    }

                    string attributeName = ca.Constructor.DeclaringType.ToString();

                    switch (attributeName)
                    {
                        case "Mirror.ServerAttribute":
                            Weaver.Error("Script " + td.FullName + " uses the attribute [Server] on the method " + md.Name + " but is not a NetworkBehaviour.");
                            break;
                        case "Mirror.ServerCallbackAttribute":
                            Weaver.Error("Script " + td.FullName + " uses the attribute [ServerCallback] on the method " + md.Name + " but is not a NetworkBehaviour.");
                            break;
                        case "Mirror.ClientAttribute":
                            Weaver.Error("Script " + td.FullName + " uses the attribute [Client] on the method " + md.Name + " but is not a NetworkBehaviour.");
                            break;
                        case "Mirror.ClientCallbackAttribute":
                            Weaver.Error("Script " + td.FullName + " uses the attribute [ClientCallback] on the method " + md.Name + " but is not a NetworkBehaviour.");
                            break;
                    }
                }
            }
        }
    }
}
