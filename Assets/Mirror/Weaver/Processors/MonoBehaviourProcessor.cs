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
                        Log.Error("Script " + td.FullName + " uses [SyncVar] " + fd.Name + " but is not a NetworkBehaviour.");
                        Weaver.fail = true;
                    }
                }

                if (SyncObjectProcessor.ImplementsSyncObject(fd.FieldType))
                {
                    Log.Error(string.Format("Script {0} defines field {1} with type {2}, but it's not a NetworkBehaviour", td.FullName, fd.Name, Helpers.PrettyPrintType(fd.FieldType)));
                    Weaver.fail = true;
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
                        Log.Error("Script " + td.FullName + " uses [Command] " + md.Name + " but is not a NetworkBehaviour.");
                        Weaver.fail = true;
                    }

                    if (ca.AttributeType.FullName == Weaver.ClientRpcType.FullName)
                    {
                        Log.Error("Script " + td.FullName + " uses [ClientRpc] " + md.Name + " but is not a NetworkBehaviour.");
                        Weaver.fail = true;
                    }

                    if (ca.AttributeType.FullName == Weaver.TargetRpcType.FullName)
                    {
                        Log.Error("Script " + td.FullName + " uses [TargetRpc] " + md.Name + " but is not a NetworkBehaviour.");
                        Weaver.fail = true;
                    }

                    string attributeName = ca.Constructor.DeclaringType.ToString();

                    switch (attributeName)
                    {
                        case "Mirror.ServerAttribute":
                            Log.Error("Script " + td.FullName + " uses the attribute [Server] on the method " + md.Name + " but is not a NetworkBehaviour.");
                            Weaver.fail = true;
                            break;
                        case "Mirror.ServerCallbackAttribute":
                            Log.Error("Script " + td.FullName + " uses the attribute [ServerCallback] on the method " + md.Name + " but is not a NetworkBehaviour.");
                            Weaver.fail = true;
                            break;
                        case "Mirror.ClientAttribute":
                            Log.Error("Script " + td.FullName + " uses the attribute [Client] on the method " + md.Name + " but is not a NetworkBehaviour.");
                            Weaver.fail = true;
                            break;
                        case "Mirror.ClientCallbackAttribute":
                            Log.Error("Script " + td.FullName + " uses the attribute [ClientCallback] on the method " + md.Name + " but is not a NetworkBehaviour.");
                            Weaver.fail = true;
                            break;
                    }
                }
            }
        }
    }
}