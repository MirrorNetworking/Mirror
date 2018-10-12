using System;
using System.Linq;
using Mono.Cecil;

namespace Mirror.Weaver
{
    class MonoBehaviourProcessor
    {
        TypeDefinition m_td;

        public MonoBehaviourProcessor(TypeDefinition td)
        {
            m_td = td;
        }

        public void Process()
        {
            ProcessSyncVars();
            ProcessMethods();
        }

        void ProcessSyncVars()
        {
            // find syncvars
            foreach (FieldDefinition fd in m_td.Fields)
            {
                foreach (var ca in fd.CustomAttributes)
                {
                    if (ca.AttributeType.FullName == Weaver.SyncVarType.FullName)
                    {
                        Log.Error("Script " + m_td.FullName + " uses [SyncVar] " + fd.Name + " but is not a NetworkBehaviour.");
                        Weaver.fail = true;
                    }
                }

                if (Helpers.ImplementsSyncObject(fd.FieldType))
                {
                    Log.Error(string.Format("Script {0} defines field {1} with type {2}, but it's not a NetworkBehaviour", m_td.FullName, fd.Name, Helpers.PrettyPrintType(fd.FieldType)));
                    Weaver.fail = true;
                }
            }
        }

        void ProcessMethods()
        {
            // find command and RPC functions
            foreach (MethodDefinition md in m_td.Methods)
            {
                foreach (var ca in md.CustomAttributes)
                {
                    if (ca.AttributeType.FullName == Weaver.CommandType.FullName)
                    {
                        Log.Error("Script " + m_td.FullName + " uses [Command] " + md.Name + " but is not a NetworkBehaviour.");
                        Weaver.fail = true;
                    }

                    if (ca.AttributeType.FullName == Weaver.ClientRpcType.FullName)
                    {
                        Log.Error("Script " + m_td.FullName + " uses [ClientRpc] " + md.Name + " but is not a NetworkBehaviour.");
                        Weaver.fail = true;
                    }

                    if (ca.AttributeType.FullName == Weaver.TargetRpcType.FullName)
                    {
                        Log.Error("Script " + m_td.FullName + " uses [TargetRpc] " + md.Name + " but is not a NetworkBehaviour.");
                        Weaver.fail = true;
                    }

                    var attrName = ca.Constructor.DeclaringType.ToString();

                    if (attrName == "Mirror.ServerAttribute")
                    {
                        Log.Error("Script " + m_td.FullName + " uses the attribute [Server] on the method " + md.Name + " but is not a NetworkBehaviour.");
                        Weaver.fail = true;
                    }
                    else if (attrName == "Mirror.ServerCallbackAttribute")
                    {
                        Log.Error("Script " + m_td.FullName + " uses the attribute [ServerCallback] on the method " + md.Name + " but is not a NetworkBehaviour.");
                        Weaver.fail = true;
                    }
                    else if (attrName == "Mirror.ClientAttribute")
                    {
                        Log.Error("Script " + m_td.FullName + " uses the attribute [Client] on the method " + md.Name + " but is not a NetworkBehaviour.");
                        Weaver.fail = true;
                    }
                    else if (attrName == "Mirror.ClientCallbackAttribute")
                    {
                        Log.Error("Script " + m_td.FullName + " uses the attribute [ClientCallback] on the method " + md.Name + " but is not a NetworkBehaviour.");
                        Weaver.fail = true;
                    }
                }
            }
        }
    }
}
