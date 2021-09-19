using System.Collections.Generic;
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    public enum RemoteCallType
    {
        Command,
        ClientRpc,
        TargetRpc
    }

    // processes SyncVars, Cmds, Rpcs, etc. of NetworkBehaviours
    class NetworkBehaviourProcessor
    {
        AssemblyDefinition assembly;
        WeaverTypes weaverTypes;
        Writers writers;
        Readers readers;
        Logger Log;

        List<FieldDefinition> syncObjects = new List<FieldDefinition>();
        // <SyncVarField,NetIdField>
        readonly List<CmdResult> commands = new List<CmdResult>();
        readonly List<ClientRpcResult> clientRpcs = new List<ClientRpcResult>();
        readonly List<MethodDefinition> targetRpcs = new List<MethodDefinition>();
        readonly List<MethodDefinition> commandInvocationFuncs = new List<MethodDefinition>();
        readonly List<MethodDefinition> clientRpcInvocationFuncs = new List<MethodDefinition>();
        readonly List<MethodDefinition> targetRpcInvocationFuncs = new List<MethodDefinition>();

        readonly TypeDefinition netBehaviourSubclass;

        public struct CmdResult
        {
            public MethodDefinition method;
            public bool requiresAuthority;
        }

        public struct ClientRpcResult
        {
            public MethodDefinition method;
            public bool includeOwner;
        }

        public NetworkBehaviourProcessor(AssemblyDefinition assembly, WeaverTypes weaverTypes, Writers writers, Readers readers, Logger Log, TypeDefinition td)
        {
            this.assembly = assembly;
            this.weaverTypes = weaverTypes;
            this.writers = writers;
            this.readers = readers;
            this.Log = Log;
            netBehaviourSubclass = td;
        }

        // return true if modified
        public bool Process(ref bool WeavingFailed)
        {
            // only process once
            if (WasProcessed(netBehaviourSubclass))
            {
                return false;
            }

            if (netBehaviourSubclass.HasGenericParameters)
            {
                Log.Error($"{netBehaviourSubclass.Name} cannot have generic parameters", netBehaviourSubclass);
                WeavingFailed = true;
                // originally Process returned true in every case, except if already processed.
                // maybe return false here in the future.
                return true;
            }
            MarkAsProcessed(netBehaviourSubclass);

            syncObjects = SyncObjectProcessor.FindSyncObjectsFields(writers, readers, Log, netBehaviourSubclass, ref WeavingFailed);

            ProcessMethods(ref WeavingFailed);
            if (WeavingFailed)
            {
                // originally Process returned true in every case, except if already processed.
                // maybe return false here in the future.
                return true;
            }
            GenerateConstants(ref WeavingFailed);

            return true;
        }

        /*
        generates code like:
            if (!NetworkClient.active)
              Debug.LogError((object) "Command function CmdRespawn called on server.");

            which is used in InvokeCmd, InvokeRpc, etc.
        */
        public static void WriteClientActiveCheck(ILProcessor worker, WeaverTypes weaverTypes, string mdName, Instruction label, string errString)
        {
            // client active check
            worker.Emit(OpCodes.Call, weaverTypes.NetworkClientGetActive);
            worker.Emit(OpCodes.Brtrue, label);

            worker.Emit(OpCodes.Ldstr, $"{errString} {mdName} called on server.");
            worker.Emit(OpCodes.Call, weaverTypes.logErrorReference);
            worker.Emit(OpCodes.Ret);
            worker.Append(label);
        }
        /*
        generates code like:
            if (!NetworkServer.active)
              Debug.LogError((object) "Command CmdMsgWhisper called on client.");
        */
        public static void WriteServerActiveCheck(ILProcessor worker, WeaverTypes weaverTypes, string mdName, Instruction label, string errString)
        {
            // server active check
            worker.Emit(OpCodes.Call, weaverTypes.NetworkServerGetActive);
            worker.Emit(OpCodes.Brtrue, label);

            worker.Emit(OpCodes.Ldstr, $"{errString} {mdName} called on client.");
            worker.Emit(OpCodes.Call, weaverTypes.logErrorReference);
            worker.Emit(OpCodes.Ret);
            worker.Append(label);
        }

        public static void WriteSetupLocals(ILProcessor worker, WeaverTypes weaverTypes)
        {
            worker.Body.InitLocals = true;
            worker.Body.Variables.Add(new VariableDefinition(weaverTypes.Import<PooledNetworkWriter>()));
        }

        public static void WriteCreateWriter(ILProcessor worker, WeaverTypes weaverTypes)
        {
            // create writer
            worker.Emit(OpCodes.Call, weaverTypes.GetPooledWriterReference);
            worker.Emit(OpCodes.Stloc_0);
        }

        public static void WriteRecycleWriter(ILProcessor worker, WeaverTypes weaverTypes)
        {
            // NetworkWriterPool.Recycle(writer);
            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Call, weaverTypes.RecycleWriterReference);
        }

        public static bool WriteArguments(ILProcessor worker, Writers writers, Logger Log, MethodDefinition method, RemoteCallType callType, ref bool WeavingFailed)
        {
            // write each argument
            // example result
            /*
            writer.WritePackedInt32(someNumber);
            writer.WriteNetworkIdentity(someTarget);
             */

            bool skipFirst = callType == RemoteCallType.TargetRpc
                && TargetRpcProcessor.HasNetworkConnectionParameter(method);

            // arg of calling  function, arg 0 is "this" so start counting at 1
            int argNum = 1;
            foreach (ParameterDefinition param in method.Parameters)
            {
                // NetworkConnection is not sent via the NetworkWriter so skip it here
                // skip first for NetworkConnection in TargetRpc
                if (argNum == 1 && skipFirst)
                {
                    argNum += 1;
                    continue;
                }
                // skip SenderConnection in Command
                if (IsSenderConnection(param, callType))
                {
                    argNum += 1;
                    continue;
                }

                MethodReference writeFunc = writers.GetWriteFunc(param.ParameterType, ref WeavingFailed);
                if (writeFunc == null)
                {
                    Log.Error($"{method.Name} has invalid parameter {param}", method);
                    WeavingFailed = true;
                    return false;
                }

                // use built-in writer func on writer object
                // NetworkWriter object
                worker.Emit(OpCodes.Ldloc_0);
                // add argument to call
                worker.Emit(OpCodes.Ldarg, argNum);
                // call writer extension method
                worker.Emit(OpCodes.Call, writeFunc);
                argNum += 1;
            }
            return true;
        }

        #region mark / check type as processed
        public const string ProcessedFunctionName = "MirrorProcessed";

        // by adding an empty MirrorProcessed() function
        public static bool WasProcessed(TypeDefinition td)
        {
            return td.GetMethod(ProcessedFunctionName) != null;
        }

        public void MarkAsProcessed(TypeDefinition td)
        {
            if (!WasProcessed(td))
            {
                MethodDefinition versionMethod = new MethodDefinition(ProcessedFunctionName, MethodAttributes.Private, weaverTypes.Import(typeof(void)));
                ILProcessor worker = versionMethod.Body.GetILProcessor();
                worker.Emit(OpCodes.Ret);
                td.Methods.Add(versionMethod);
            }
        }
        #endregion

        void GenerateConstants(ref bool WeavingFailed)
        {
            if (commands.Count == 0 && clientRpcs.Count == 0 && targetRpcs.Count == 0 && syncObjects.Count == 0)
                return;

            // find static constructor
            MethodDefinition cctor = netBehaviourSubclass.GetMethod(".cctor");
            bool cctorFound = cctor != null;
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
                        Log.Error($"{netBehaviourSubclass.Name} has invalid class constructor", cctor);
                        WeavingFailed = true;
                        return;
                    }
                }
            }
            else
            {
                // make one!
                cctor = new MethodDefinition(".cctor", MethodAttributes.Private |
                        MethodAttributes.HideBySig |
                        MethodAttributes.SpecialName |
                        MethodAttributes.RTSpecialName |
                        MethodAttributes.Static,
                        weaverTypes.Import(typeof(void)));
            }

            // find instance constructor
            MethodDefinition ctor = netBehaviourSubclass.GetMethod(".ctor");

            if (ctor == null)
            {
                Log.Error($"{netBehaviourSubclass.Name} has invalid constructor", netBehaviourSubclass);
                WeavingFailed = true;
                return;
            }

            Instruction ret = ctor.Body.Instructions[ctor.Body.Instructions.Count - 1];
            if (ret.OpCode == OpCodes.Ret)
            {
                ctor.Body.Instructions.RemoveAt(ctor.Body.Instructions.Count - 1);
            }
            else
            {
                Log.Error($"{netBehaviourSubclass.Name} has invalid constructor", ctor);
                WeavingFailed = true;
                return;
            }

            // TODO: find out if the order below matters. If it doesn't split code below into 2 functions
            ILProcessor ctorWorker = ctor.Body.GetILProcessor();
            ILProcessor cctorWorker = cctor.Body.GetILProcessor();

            for (int i = 0; i < commands.Count; ++i)
            {
                CmdResult cmdResult = commands[i];
                GenerateRegisterCommandDelegate(cctorWorker, weaverTypes, weaverTypes.registerCommandDelegateReference, commandInvocationFuncs[i], cmdResult);
            }

            for (int i = 0; i < clientRpcs.Count; ++i)
            {
                ClientRpcResult clientRpcResult = clientRpcs[i];
                GenerateRegisterRemoteDelegate(cctorWorker, weaverTypes, weaverTypes.registerRpcDelegateReference, clientRpcInvocationFuncs[i], clientRpcResult.method.Name);
            }

            for (int i = 0; i < targetRpcs.Count; ++i)
            {
                GenerateRegisterRemoteDelegate(cctorWorker, weaverTypes, weaverTypes.registerRpcDelegateReference, targetRpcInvocationFuncs[i], targetRpcs[i].Name);
            }

            foreach (FieldDefinition fd in syncObjects)
            {
                SyncObjectInitializer.GenerateSyncObjectInitializer(ctorWorker, weaverTypes, fd);
            }

            cctorWorker.Append(cctorWorker.Create(OpCodes.Ret));
            if (!cctorFound)
            {
                netBehaviourSubclass.Methods.Add(cctor);
            }

            // finish ctor
            ctorWorker.Append(ctorWorker.Create(OpCodes.Ret));

            // in case class had no cctor, it might have BeforeFieldInit, so injected cctor would be called too late
            netBehaviourSubclass.Attributes &= ~TypeAttributes.BeforeFieldInit;
        }

        /*
            // This generates code like:
            NetworkBehaviour.RegisterCommandDelegate(base.GetType(), "CmdThrust", new NetworkBehaviour.CmdDelegate(ShipControl.InvokeCmdCmdThrust));
        */
        void GenerateRegisterRemoteDelegate(ILProcessor worker, WeaverTypes weaverTypes, MethodReference registerMethod, MethodDefinition func, string cmdName)
        {
            worker.Emit(OpCodes.Ldtoken, netBehaviourSubclass);
            worker.Emit(OpCodes.Call, weaverTypes.getTypeFromHandleReference);
            worker.Emit(OpCodes.Ldstr, cmdName);
            worker.Emit(OpCodes.Ldnull);
            worker.Emit(OpCodes.Ldftn, func);

            worker.Emit(OpCodes.Newobj, weaverTypes.CmdDelegateConstructor);
            //
            worker.Emit(OpCodes.Call, registerMethod);
        }

        void GenerateRegisterCommandDelegate(ILProcessor worker, WeaverTypes weaverTypes, MethodReference registerMethod, MethodDefinition func, CmdResult cmdResult)
        {
            string cmdName = cmdResult.method.Name;
            bool requiresAuthority = cmdResult.requiresAuthority;

            worker.Emit(OpCodes.Ldtoken, netBehaviourSubclass);
            worker.Emit(OpCodes.Call, weaverTypes.getTypeFromHandleReference);
            worker.Emit(OpCodes.Ldstr, cmdName);
            worker.Emit(OpCodes.Ldnull);
            worker.Emit(OpCodes.Ldftn, func);

            worker.Emit(OpCodes.Newobj, weaverTypes.CmdDelegateConstructor);

            // requiresAuthority ? 1 : 0
            worker.Emit(requiresAuthority ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);

            worker.Emit(OpCodes.Call, registerMethod);
        }

        public static bool ReadArguments(MethodDefinition method, Readers readers, Logger Log, ILProcessor worker, RemoteCallType callType, ref bool WeavingFailed)
        {
            // read each argument
            // example result
            /*
            CallCmdDoSomething(reader.ReadPackedInt32(), reader.ReadNetworkIdentity());
             */

            bool skipFirst = callType == RemoteCallType.TargetRpc
                && TargetRpcProcessor.HasNetworkConnectionParameter(method);

            // arg of calling  function, arg 0 is "this" so start counting at 1
            int argNum = 1;
            foreach (ParameterDefinition param in method.Parameters)
            {
                // NetworkConnection is not sent via the NetworkWriter so skip it here
                // skip first for NetworkConnection in TargetRpc
                if (argNum == 1 && skipFirst)
                {
                    argNum += 1;
                    continue;
                }
                // skip SenderConnection in Command
                if (IsSenderConnection(param, callType))
                {
                    argNum += 1;
                    continue;
                }


                MethodReference readFunc = readers.GetReadFunc(param.ParameterType, ref WeavingFailed);

                if (readFunc == null)
                {
                    Log.Error($"{method.Name} has invalid parameter {param}.  Unsupported type {param.ParameterType},  use a supported Mirror type instead", method);
                    WeavingFailed = true;
                    return false;
                }

                worker.Emit(OpCodes.Ldarg_1);
                worker.Emit(OpCodes.Call, readFunc);

                // conversion.. is this needed?
                if (param.ParameterType.Is<float>())
                {
                    worker.Emit(OpCodes.Conv_R4);
                }
                else if (param.ParameterType.Is<double>())
                {
                    worker.Emit(OpCodes.Conv_R8);
                }
            }
            return true;
        }

        public static void AddInvokeParameters(WeaverTypes weaverTypes, ICollection<ParameterDefinition> collection)
        {
            collection.Add(new ParameterDefinition("obj", ParameterAttributes.None, weaverTypes.Import<NetworkBehaviour>()));
            collection.Add(new ParameterDefinition("reader", ParameterAttributes.None, weaverTypes.Import<NetworkReader>()));
            // senderConnection is only used for commands but NetworkBehaviour.CmdDelegate is used for all remote calls
            collection.Add(new ParameterDefinition("senderConnection", ParameterAttributes.None, weaverTypes.Import<NetworkConnectionToClient>()));
        }

        // check if a Command/TargetRpc/Rpc function & parameters are valid for weaving
        public bool ValidateRemoteCallAndParameters(MethodDefinition method, RemoteCallType callType, ref bool WeavingFailed)
        {
            if (method.IsStatic)
            {
                Log.Error($"{method.Name} must not be static", method);
                WeavingFailed = true;
                return false;
            }

            return ValidateFunction(method, ref WeavingFailed) &&
                   ValidateParameters(method, callType, ref WeavingFailed);
        }

        // check if a Command/TargetRpc/Rpc function is valid for weaving
        bool ValidateFunction(MethodReference md, ref bool WeavingFailed)
        {
            if (md.ReturnType.Is<System.Collections.IEnumerator>())
            {
                Log.Error($"{md.Name} cannot be a coroutine", md);
                WeavingFailed = true;
                return false;
            }
            if (!md.ReturnType.Is(typeof(void)))
            {
                Log.Error($"{md.Name} cannot return a value.  Make it void instead", md);
                WeavingFailed = true;
                return false;
            }
            if (md.HasGenericParameters)
            {
                Log.Error($"{md.Name} cannot have generic parameters", md);
                WeavingFailed = true;
                return false;
            }
            return true;
        }

        // check if all Command/TargetRpc/Rpc function's parameters are valid for weaving
        bool ValidateParameters(MethodReference method, RemoteCallType callType, ref bool WeavingFailed)
        {
            for (int i = 0; i < method.Parameters.Count; ++i)
            {
                ParameterDefinition param = method.Parameters[i];
                if (!ValidateParameter(method, param, callType, i == 0, ref WeavingFailed))
                {
                    return false;
                }
            }
            return true;
        }

        // validate parameters for a remote function call like Rpc/Cmd
        bool ValidateParameter(MethodReference method, ParameterDefinition param, RemoteCallType callType, bool firstParam, ref bool WeavingFailed)
        {
            bool isNetworkConnection = param.ParameterType.Is<NetworkConnection>();
            bool isSenderConnection = IsSenderConnection(param, callType);

            if (param.IsOut)
            {
                Log.Error($"{method.Name} cannot have out parameters", method);
                WeavingFailed = true;
                return false;
            }


            // if not SenderConnection And not TargetRpc NetworkConnection first param
            if (!isSenderConnection && isNetworkConnection && !(callType == RemoteCallType.TargetRpc && firstParam))
            {
                if (callType == RemoteCallType.Command)
                {
                    Log.Error($"{method.Name} has invalid parameter {param}, Cannot pass NetworkConnections. Instead use 'NetworkConnectionToClient conn = null' to get the sender's connection on the server", method);
                }
                else
                {
                    Log.Error($"{method.Name} has invalid parameter {param}. Cannot pass NetworkConnections", method);
                }
                WeavingFailed = true;
                return false;
            }

            // sender connection can be optional
            if (param.IsOptional && !isSenderConnection)
            {
                Log.Error($"{method.Name} cannot have optional parameters", method);
                WeavingFailed = true;
                return false;
            }

            return true;
        }

        public static bool IsSenderConnection(ParameterDefinition param, RemoteCallType callType)
        {
            if (callType != RemoteCallType.Command)
            {
                return false;
            }

            TypeReference type = param.ParameterType;

            return type.Is<NetworkConnectionToClient>()
                || type.Resolve().IsDerivedFrom<NetworkConnectionToClient>();
        }

        void ProcessMethods(ref bool WeavingFailed)
        {
            HashSet<string> names = new HashSet<string>();

            // copy the list of methods because we will be adding methods in the loop
            List<MethodDefinition> methods = new List<MethodDefinition>(netBehaviourSubclass.Methods);
            // find command and RPC functions
            foreach (MethodDefinition md in methods)
            {
                foreach (CustomAttribute ca in md.CustomAttributes)
                {
                    if (ca.AttributeType.Is<CommandAttribute>())
                    {
                        ProcessCommand(names, md, ca, ref WeavingFailed);
                        break;
                    }

                    if (ca.AttributeType.Is<TargetRpcAttribute>())
                    {
                        ProcessTargetRpc(names, md, ca, ref WeavingFailed);
                        break;
                    }

                    if (ca.AttributeType.Is<ClientRpcAttribute>())
                    {
                        ProcessClientRpc(names, md, ca, ref WeavingFailed);
                        break;
                    }
                }
            }
        }

        void ProcessClientRpc(HashSet<string> names, MethodDefinition md, CustomAttribute clientRpcAttr, ref bool WeavingFailed)
        {
            if (md.IsAbstract)
            {
                Log.Error("Abstract ClientRpc are currently not supported, use virtual method instead", md);
                WeavingFailed = true;
                return;
            }

            if (!ValidateRemoteCallAndParameters(md, RemoteCallType.ClientRpc, ref WeavingFailed))
            {
                return;
            }

            if (names.Contains(md.Name))
            {
                Log.Error($"Duplicate ClientRpc name {md.Name}", md);
                WeavingFailed = true;
                return;
            }

            bool includeOwner = clientRpcAttr.GetField("includeOwner", true);

            names.Add(md.Name);
            clientRpcs.Add(new ClientRpcResult
            {
                method = md,
                includeOwner = includeOwner
            });

            MethodDefinition rpcCallFunc = RpcProcessor.ProcessRpcCall(weaverTypes, writers, Log, netBehaviourSubclass, md, clientRpcAttr, ref WeavingFailed);
            // need null check here because ProcessRpcCall returns null if it can't write all the args
            if (rpcCallFunc == null) { return; }

            MethodDefinition rpcFunc = RpcProcessor.ProcessRpcInvoke(weaverTypes, writers, readers, Log, netBehaviourSubclass, md, rpcCallFunc, ref WeavingFailed);
            if (rpcFunc != null)
            {
                clientRpcInvocationFuncs.Add(rpcFunc);
            }
        }

        void ProcessTargetRpc(HashSet<string> names, MethodDefinition md, CustomAttribute targetRpcAttr, ref bool WeavingFailed)
        {
            if (md.IsAbstract)
            {
                Log.Error("Abstract TargetRpc are currently not supported, use virtual method instead", md);
                WeavingFailed = true;
                return;
            }

            if (!ValidateRemoteCallAndParameters(md, RemoteCallType.TargetRpc, ref WeavingFailed))
                return;

            if (names.Contains(md.Name))
            {
                Log.Error($"Duplicate Target Rpc name {md.Name}", md);
                WeavingFailed = true;
                return;
            }
            names.Add(md.Name);
            targetRpcs.Add(md);

            MethodDefinition rpcCallFunc = TargetRpcProcessor.ProcessTargetRpcCall(weaverTypes, writers, Log, netBehaviourSubclass, md, targetRpcAttr, ref WeavingFailed);

            MethodDefinition rpcFunc = TargetRpcProcessor.ProcessTargetRpcInvoke(weaverTypes, readers, Log, netBehaviourSubclass, md, rpcCallFunc, ref WeavingFailed);
            if (rpcFunc != null)
            {
                targetRpcInvocationFuncs.Add(rpcFunc);
            }
        }

        void ProcessCommand(HashSet<string> names, MethodDefinition md, CustomAttribute commandAttr, ref bool WeavingFailed)
        {
            if (md.IsAbstract)
            {
                Log.Error("Abstract Commands are currently not supported, use virtual method instead", md);
                WeavingFailed = true;
                return;
            }

            if (!ValidateRemoteCallAndParameters(md, RemoteCallType.Command, ref WeavingFailed))
                return;

            if (names.Contains(md.Name))
            {
                Log.Error($"Duplicate Command name {md.Name}", md);
                WeavingFailed = true;
                return;
            }

            bool requiresAuthority = commandAttr.GetField("requiresAuthority", true);

            names.Add(md.Name);
            commands.Add(new CmdResult
            {
                method = md,
                requiresAuthority = requiresAuthority
            });

            MethodDefinition cmdCallFunc = CommandProcessor.ProcessCommandCall(weaverTypes, writers, Log, netBehaviourSubclass, md, commandAttr, ref WeavingFailed);

            MethodDefinition cmdFunc = CommandProcessor.ProcessCommandInvoke(weaverTypes, readers, Log, netBehaviourSubclass, md, cmdCallFunc, ref WeavingFailed);
            if (cmdFunc != null)
            {
                commandInvocationFuncs.Add(cmdFunc);
            }
        }
    }
}
