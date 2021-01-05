using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Mirror.Weaver
{
    public enum RemoteCallType
    {
        ServerRpc,
        ClientRpc
    }

    /// <summary>
    /// processes SyncVars, Cmds, Rpcs, etc. of NetworkBehaviours
    /// </summary>
    class NetworkBehaviourProcessor
    {
        readonly TypeDefinition netBehaviourSubclass;
        readonly ServerRpcProcessor serverRpcProcessor = new ServerRpcProcessor();
        readonly ClientRpcProcessor clientRpcProcessor = new ClientRpcProcessor();
        readonly SyncVarProcessor syncVarProcessor = new SyncVarProcessor();
        readonly SyncObjectProcessor syncObjectProcessor = new SyncObjectProcessor();

        public NetworkBehaviourProcessor(TypeDefinition td)
        {
            Weaver.DLog(td, "NetworkBehaviourProcessor");
            netBehaviourSubclass = td;
        }

        // return true if modified
        public bool Process()
        {
            // only process once
            if (WasProcessed(netBehaviourSubclass))
            {
                return false;
            }
            Weaver.DLog(netBehaviourSubclass, "Found NetworkBehaviour " + netBehaviourSubclass.FullName);

            Weaver.DLog(netBehaviourSubclass, "Process Start");
            MarkAsProcessed(netBehaviourSubclass);

            syncVarProcessor.ProcessSyncVars(netBehaviourSubclass);

            syncObjectProcessor.ProcessSyncObjects(netBehaviourSubclass);

            ProcessRpcs();

            Weaver.DLog(netBehaviourSubclass, "Process Done");
            return true;
        }

        #region mark / check type as processed
        public const string ProcessedFunctionName = "MirrorProcessed";

        // by adding an empty MirrorProcessed() function
        public static bool WasProcessed(TypeDefinition td)
        {
            return td.GetMethod(ProcessedFunctionName) != null;
        }

        public static void MarkAsProcessed(TypeDefinition td)
        {
            if (!WasProcessed(td))
            {
                MethodDefinition versionMethod = td.AddMethod(ProcessedFunctionName, MethodAttributes.Private);
                ILProcessor worker = versionMethod.Body.GetILProcessor();
                worker.Append(worker.Create(OpCodes.Ret));
            }
        }
        #endregion

        void RegisterRpcs()
        {
            Weaver.DLog(netBehaviourSubclass, "  GenerateConstants ");

            // find static constructor
            MethodDefinition cctor = netBehaviourSubclass.GetMethod(".cctor");
            if (cctor != null)
            {
                // remove the return opcode from end of function. will add our own later.
                if (cctor.Body.Instructions.Count != 0)
                {
                    Instruction retInstr = cctor.Body.Instructions[cctor.Body.Instructions.Count - 1];
                    if (retInstr.OpCode == OpCodes.Ret)
                    {
                        cctor.Body.Instructions.RemoveAt(cctor.Body.Instructions.Count - 1);
                    }
                    else
                    {
                        Weaver.Error($"{netBehaviourSubclass.Name} has invalid class constructor", cctor);
                        return;
                    }
                }
            }
            else
            {
                // make one!
                cctor = netBehaviourSubclass.AddMethod(".cctor", MethodAttributes.Private |
                        MethodAttributes.HideBySig |
                        MethodAttributes.SpecialName |
                        MethodAttributes.RTSpecialName |
                        MethodAttributes.Static);
            }

            ILProcessor cctorWorker = cctor.Body.GetILProcessor();

            serverRpcProcessor.RegisterServerRpcs(cctorWorker);

            clientRpcProcessor.RegisterClientRpcs(cctorWorker);

            cctorWorker.Append(cctorWorker.Create(OpCodes.Ret));

            // in case class had no cctor, it might have BeforeFieldInit, so injected cctor would be called too late
            netBehaviourSubclass.Attributes &= ~TypeAttributes.BeforeFieldInit;
        }

        void ProcessRpcs()
        {
            var names = new HashSet<string>();

            // copy the list of methods because we will be adding methods in the loop
            var methods = new List<MethodDefinition>(netBehaviourSubclass.Methods);
            // find ServerRpc and RPC functions
            foreach (MethodDefinition md in methods)
            {
                bool rpc = false;
                foreach (CustomAttribute ca in md.CustomAttributes)
                {
                    if (ca.AttributeType.Is<ServerRpcAttribute>())
                    {
                        serverRpcProcessor.ProcessServerRpc(md, ca);
                        rpc = true;
                        break;
                    }

                    if (ca.AttributeType.Is<ClientRpcAttribute>())
                    {
                        clientRpcProcessor.ProcessClientRpc(md, ca);
                        rpc = true;
                        break;
                    }
                }

                if (rpc)
                {
                    if (names.Contains(md.Name))
                    {
                        Weaver.Error($"Duplicate Rpc name {md.Name}", md);
                    }
                    names.Add(md.Name);
                }
            }

            RegisterRpcs();
        }
    }
}
