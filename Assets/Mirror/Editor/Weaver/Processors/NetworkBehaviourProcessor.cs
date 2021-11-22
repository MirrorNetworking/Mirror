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
        SyncVarAccessLists syncVarAccessLists;
        SyncVarAttributeProcessor syncVarAttributeProcessor;
        Writers writers;
        Readers readers;
        Logger Log;

        List<FieldDefinition> syncVars = new List<FieldDefinition>();
        List<FieldDefinition> syncObjects = new List<FieldDefinition>();
        // <SyncVarField,NetIdField>
        Dictionary<FieldDefinition, FieldDefinition> syncVarNetIds = new Dictionary<FieldDefinition, FieldDefinition>();
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

        public NetworkBehaviourProcessor(AssemblyDefinition assembly, WeaverTypes weaverTypes, SyncVarAccessLists syncVarAccessLists, Writers writers, Readers readers, Logger Log, TypeDefinition td)
        {
            this.assembly = assembly;
            this.weaverTypes = weaverTypes;
            this.syncVarAccessLists = syncVarAccessLists;
            this.writers = writers;
            this.readers = readers;
            this.Log = Log;
            syncVarAttributeProcessor = new SyncVarAttributeProcessor(assembly, weaverTypes, syncVarAccessLists, Log);
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

            // deconstruct tuple and set fields
            (syncVars, syncVarNetIds) = syncVarAttributeProcessor.ProcessSyncVars(netBehaviourSubclass, ref WeavingFailed);

            syncObjects = SyncObjectProcessor.FindSyncObjectsFields(writers, readers, Log, netBehaviourSubclass, ref WeavingFailed);

            ProcessMethods(ref WeavingFailed);
            if (WeavingFailed)
            {
                // originally Process returned true in every case, except if already processed.
                // maybe return false here in the future.
                return true;
            }

            // inject initializations into static & instance constructor
            InjectIntoStaticConstructor(ref WeavingFailed);
            InjectIntoInstanceConstructor(ref WeavingFailed);

            GenerateSerialization(ref WeavingFailed);
            if (WeavingFailed)
            {
                // originally Process returned true in every case, except if already processed.
                // maybe return false here in the future.
                return true;
            }

            GenerateDeSerialization(ref WeavingFailed);
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

        // helper function to remove 'Ret' from the end of the method if 'Ret'
        // is the last instruction.
        // returns false if there was an issue
        static bool RemoveFinalRetInstruction(MethodDefinition method)
        {
            // remove the return opcode from end of function. will add our own later.
            if (method.Body.Instructions.Count != 0)
            {
                Instruction retInstr = method.Body.Instructions[method.Body.Instructions.Count - 1];
                if (retInstr.OpCode == OpCodes.Ret)
                {
                    method.Body.Instructions.RemoveAt(method.Body.Instructions.Count - 1);
                    return true;
                }
                return false;
            }

            // we did nothing, but there was no error.
            return true;
        }

        // we need to inject several initializations into NetworkBehaviour cctor
        void InjectIntoStaticConstructor(ref bool WeavingFailed)
        {
            if (commands.Count == 0 && clientRpcs.Count == 0 && targetRpcs.Count == 0)
                return;

            // find static constructor
            MethodDefinition cctor = netBehaviourSubclass.GetMethod(".cctor");
            bool cctorFound = cctor != null;
            if (cctor != null)
            {
                // remove the return opcode from end of function. will add our own later.
                if (!RemoveFinalRetInstruction(cctor))
                {
                    Log.Error($"{netBehaviourSubclass.Name} has invalid class constructor", cctor);
                    WeavingFailed = true;
                    return;
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

            ILProcessor cctorWorker = cctor.Body.GetILProcessor();

            // register all commands in cctor
            for (int i = 0; i < commands.Count; ++i)
            {
                CmdResult cmdResult = commands[i];
                GenerateRegisterCommandDelegate(cctorWorker, weaverTypes.registerCommandDelegateReference, commandInvocationFuncs[i], cmdResult);
            }

            // register all client rpcs in cctor
            for (int i = 0; i < clientRpcs.Count; ++i)
            {
                ClientRpcResult clientRpcResult = clientRpcs[i];
                GenerateRegisterRemoteDelegate(cctorWorker, weaverTypes.registerRpcDelegateReference, clientRpcInvocationFuncs[i], clientRpcResult.method.Name);
            }

            // register all target rpcs in cctor
            for (int i = 0; i < targetRpcs.Count; ++i)
            {
                GenerateRegisterRemoteDelegate(cctorWorker, weaverTypes.registerRpcDelegateReference, targetRpcInvocationFuncs[i], targetRpcs[i].Name);
            }

            // add final 'Ret' instruction to cctor
            cctorWorker.Append(cctorWorker.Create(OpCodes.Ret));
            if (!cctorFound)
            {
                netBehaviourSubclass.Methods.Add(cctor);
            }

            // in case class had no cctor, it might have BeforeFieldInit, so injected cctor would be called too late
            netBehaviourSubclass.Attributes &= ~TypeAttributes.BeforeFieldInit;
        }

        // we need to inject several initializations into NetworkBehaviour ctor
        void InjectIntoInstanceConstructor(ref bool WeavingFailed)
        {
            if (syncObjects.Count == 0)
                return;

            // find instance constructor
            MethodDefinition ctor = netBehaviourSubclass.GetMethod(".ctor");
            if (ctor == null)
            {
                Log.Error($"{netBehaviourSubclass.Name} has invalid constructor", netBehaviourSubclass);
                WeavingFailed = true;
                return;
            }

            // remove the return opcode from end of function. will add our own later.
            if (!RemoveFinalRetInstruction(ctor))
            {
                Log.Error($"{netBehaviourSubclass.Name} has invalid constructor", ctor);
                WeavingFailed = true;
                return;
            }

            ILProcessor ctorWorker = ctor.Body.GetILProcessor();

            // initialize all sync objects in ctor
            foreach (FieldDefinition fd in syncObjects)
            {
                SyncObjectInitializer.GenerateSyncObjectInitializer(ctorWorker, weaverTypes, fd);
            }

            // add final 'Ret' instruction to ctor
            ctorWorker.Append(ctorWorker.Create(OpCodes.Ret));
        }

        /*
            // This generates code like:
            NetworkBehaviour.RegisterCommandDelegate(base.GetType(), "CmdThrust", new NetworkBehaviour.CmdDelegate(ShipControl.InvokeCmdCmdThrust));
        */
        void GenerateRegisterRemoteDelegate(ILProcessor worker, MethodReference registerMethod, MethodDefinition func, string cmdName)
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

        void GenerateRegisterCommandDelegate(ILProcessor worker, MethodReference registerMethod, MethodDefinition func, CmdResult cmdResult)
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

        void GenerateSerialization(ref bool WeavingFailed)
        {
            const string SerializeMethodName = "SerializeSyncVars";
            if (netBehaviourSubclass.GetMethod(SerializeMethodName) != null)
                return;

            if (syncVars.Count == 0)
            {
                // no synvars,  no need for custom OnSerialize
                return;
            }

            MethodDefinition serialize = new MethodDefinition(SerializeMethodName,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    weaverTypes.Import<bool>());

            serialize.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, weaverTypes.Import<NetworkWriter>()));
            serialize.Parameters.Add(new ParameterDefinition("forceAll", ParameterAttributes.None, weaverTypes.Import<bool>()));
            ILProcessor worker = serialize.Body.GetILProcessor();

            serialize.Body.InitLocals = true;

            // loc_0,  this local variable is to determine if any variable was dirty
            VariableDefinition dirtyLocal = new VariableDefinition(weaverTypes.Import<bool>());
            serialize.Body.Variables.Add(dirtyLocal);

            MethodReference baseSerialize = Resolvers.TryResolveMethodInParents(netBehaviourSubclass.BaseType, assembly, SerializeMethodName);
            if (baseSerialize != null)
            {
                // base
                worker.Emit(OpCodes.Ldarg_0);
                // writer
                worker.Emit(OpCodes.Ldarg_1);
                // forceAll
                worker.Emit(OpCodes.Ldarg_2);
                worker.Emit(OpCodes.Call, baseSerialize);
                // set dirtyLocal to result of base.OnSerialize()
                worker.Emit(OpCodes.Stloc_0);
            }

            // Generates: if (forceAll);
            Instruction initialStateLabel = worker.Create(OpCodes.Nop);
            // forceAll
            worker.Emit(OpCodes.Ldarg_2);
            worker.Emit(OpCodes.Brfalse, initialStateLabel);

            foreach (FieldDefinition syncVar in syncVars)
            {
                // Generates a writer call for each sync variable
                // writer
                worker.Emit(OpCodes.Ldarg_1);
                // this
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldfld, syncVar);
                MethodReference writeFunc = writers.GetWriteFunc(syncVar.FieldType, ref WeavingFailed);
                if (writeFunc != null)
                {
                    worker.Emit(OpCodes.Call, writeFunc);
                }
                else
                {
                    Log.Error($"{syncVar.Name} has unsupported type. Use a supported Mirror type instead", syncVar);
                    WeavingFailed = true;
                    return;
                }
            }

            // always return true if forceAll

            // Generates: return true
            worker.Emit(OpCodes.Ldc_I4_1);
            worker.Emit(OpCodes.Ret);

            // Generates: end if (forceAll);
            worker.Append(initialStateLabel);

            // write dirty bits before the data fields
            // Generates: writer.WritePackedUInt64 (base.get_syncVarDirtyBits ());
            // writer
            worker.Emit(OpCodes.Ldarg_1);
            // base
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Call, weaverTypes.NetworkBehaviourDirtyBitsReference);
            MethodReference writeUint64Func = writers.GetWriteFunc(weaverTypes.Import<ulong>(), ref WeavingFailed);
            worker.Emit(OpCodes.Call, writeUint64Func);

            // generate a writer call for any dirty variable in this class

            // start at number of syncvars in parent
            int dirtyBit = syncVarAccessLists.GetSyncVarStart(netBehaviourSubclass.BaseType.FullName);
            foreach (FieldDefinition syncVar in syncVars)
            {
                Instruction varLabel = worker.Create(OpCodes.Nop);

                // Generates: if ((base.get_syncVarDirtyBits() & 1uL) != 0uL)
                // base
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Call, weaverTypes.NetworkBehaviourDirtyBitsReference);
                // 8 bytes = long
                worker.Emit(OpCodes.Ldc_I8, 1L << dirtyBit);
                worker.Emit(OpCodes.And);
                worker.Emit(OpCodes.Brfalse, varLabel);

                // Generates a call to the writer for that field
                // writer
                worker.Emit(OpCodes.Ldarg_1);
                // base
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldfld, syncVar);

                MethodReference writeFunc = writers.GetWriteFunc(syncVar.FieldType, ref WeavingFailed);
                if (writeFunc != null)
                {
                    worker.Emit(OpCodes.Call, writeFunc);
                }
                else
                {
                    Log.Error($"{syncVar.Name} has unsupported type. Use a supported Mirror type instead", syncVar);
                    WeavingFailed = true;
                    return;
                }

                // something was dirty
                worker.Emit(OpCodes.Ldc_I4_1);
                // set dirtyLocal to true
                worker.Emit(OpCodes.Stloc_0);

                worker.Append(varLabel);
                dirtyBit += 1;
            }

            // add a log message if needed for debugging
            //worker.Emit(OpCodes.Ldstr, $"Injected Serialize {netBehaviourSubclass.Name}");
            //worker.Emit(OpCodes.Call, WeaverTypes.logErrorReference);

            // generate: return dirtyLocal
            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Ret);
            netBehaviourSubclass.Methods.Add(serialize);
        }

        void DeserializeField(FieldDefinition syncVar, ILProcessor worker, MethodDefinition deserialize, ref bool WeavingFailed)
        {
            // check for Hook function
            MethodDefinition hookMethod = syncVarAttributeProcessor.GetHookMethod(netBehaviourSubclass, syncVar, ref WeavingFailed);

            if (syncVar.FieldType.IsDerivedFrom<NetworkBehaviour>())
            {
                DeserializeNetworkBehaviourField(syncVar, worker, deserialize, hookMethod, ref WeavingFailed);
            }
            else if (syncVar.FieldType.IsNetworkIdentityField())
            {
                DeserializeNetworkIdentityField(syncVar, worker, deserialize, hookMethod, ref WeavingFailed);
            }
            else
            {
                DeserializeNormalField(syncVar, worker, deserialize, hookMethod, ref WeavingFailed);
            }
        }

        /// [SyncVar] GameObject/NetworkIdentity?
        void DeserializeNetworkIdentityField(FieldDefinition syncVar, ILProcessor worker, MethodDefinition deserialize, MethodDefinition hookMethod, ref bool WeavingFailed)
        {
            /*
            Generates code like:
               uint oldNetId = ___qNetId;
               // returns GetSyncVarGameObject(___qNetId)
               GameObject oldSyncVar = syncvar.getter;
               ___qNetId = reader.ReadPackedUInt32();
               if (!SyncVarEqual(oldNetId, ref ___goNetId))
               {
                   // getter returns GetSyncVarGameObject(___qNetId)
                   OnSetQ(oldSyncVar, syncvar.getter);
               }
            */

            // GameObject/NetworkIdentity SyncVar:
            //   OnSerialize sends writer.Write(go);
            //   OnDeserialize reads to __netId manually so we can use
            //     lookups in the getter (so it still works if objects
            //     move in and out of range repeatedly)
            FieldDefinition netIdField = syncVarNetIds[syncVar];

            // uint oldNetId = ___qNetId;
            VariableDefinition oldNetId = new VariableDefinition(weaverTypes.Import<uint>());
            deserialize.Body.Variables.Add(oldNetId);
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Ldfld, netIdField);
            worker.Emit(OpCodes.Stloc, oldNetId);

            // GameObject/NetworkIdentity oldSyncVar = syncvar.getter;
            VariableDefinition oldSyncVar = new VariableDefinition(syncVar.FieldType);
            deserialize.Body.Variables.Add(oldSyncVar);
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Ldfld, syncVar);
            worker.Emit(OpCodes.Stloc, oldSyncVar);

            // read id and store in netId field BEFORE calling the hook
            // -> this makes way more sense. by definition, the hook is
            //    supposed to be called after it was changed. not before.
            // -> setting it BEFORE calling the hook fixes the following bug:
            //    https://github.com/vis2k/Mirror/issues/1151 in host mode
            //    where the value during the Hook call would call Cmds on
            //    the host server, and they would all happen and compare
            //    values BEFORE the hook even returned and hence BEFORE the
            //    actual value was even set.
            // put 'this.' onto stack for 'this.netId' below
            worker.Emit(OpCodes.Ldarg_0);
            // reader. for 'reader.Read()' below
            worker.Emit(OpCodes.Ldarg_1);
            // Read()
            worker.Emit(OpCodes.Call, readers.GetReadFunc(weaverTypes.Import<uint>(), ref WeavingFailed));
            // netId
            worker.Emit(OpCodes.Stfld, netIdField);

            if (hookMethod != null)
            {
                // call Hook(this.GetSyncVarGameObject/NetworkIdentity(reader.ReadPackedUInt32()))
                // because we send/receive the netID, not the GameObject/NetworkIdentity
                // but only if SyncVar changed. otherwise a client would
                // get hook calls for all initial values, even if they
                // didn't change from the default values on the client.
                // see also: https://github.com/vis2k/Mirror/issues/1278

                // IMPORTANT: for GameObjects/NetworkIdentities we usually
                //            use SyncVarGameObjectEqual to compare equality.
                //            in this case however, we can just use
                //            SyncVarEqual with the two uint netIds.
                //            => this is easier weaver code because we don't
                //               have to get the GameObject/NetworkIdentity
                //               from the uint netId
                //            => this is faster because we void one
                //               GetComponent call for GameObjects to get
                //               their NetworkIdentity when comparing.

                // Generates: if (!SyncVarEqual);
                Instruction syncVarEqualLabel = worker.Create(OpCodes.Nop);

                // NOTE: static function. don't Emit Ldarg_0 aka 'this'.

                // 'oldNetId'
                worker.Emit(OpCodes.Ldloc, oldNetId);
                // 'ref this.__netId'
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldflda, netIdField);
                // call the function
                GenericInstanceMethod syncVarEqualGm = new GenericInstanceMethod(weaverTypes.syncVarEqualReference);
                syncVarEqualGm.GenericArguments.Add(netIdField.FieldType);
                worker.Emit(OpCodes.Call, syncVarEqualGm);
                worker.Emit(OpCodes.Brtrue, syncVarEqualLabel);

                // call the hook
                // Generates: OnValueChanged(oldValue, this.syncVar);
                syncVarAttributeProcessor.WriteCallHookMethodUsingField(worker, hookMethod, oldSyncVar, syncVar, ref WeavingFailed);

                // Generates: end if (!SyncVarEqual);
                worker.Append(syncVarEqualLabel);
            }
        }

        // [SyncVar] NetworkBehaviour
        void DeserializeNetworkBehaviourField(FieldDefinition syncVar, ILProcessor worker, MethodDefinition deserialize, MethodDefinition hookMethod, ref bool WeavingFailed)
        {
            /*
            Generates code like:
               uint oldNetId = ___qNetId.netId;
               byte oldCompIndex = ___qNetId.componentIndex;
               T oldSyncVar = syncvar.getter;
               ___qNetId.netId = reader.ReadPackedUInt32();
               ___qNetId.componentIndex = reader.ReadByte();
               if (!SyncVarEqual(oldNetId, ref ___goNetId))
               {
                   // getter returns GetSyncVarGameObject(___qNetId)
                   OnSetQ(oldSyncVar, syncvar.getter);
               }
            */

            // GameObject/NetworkIdentity SyncVar:
            //   OnSerialize sends writer.Write(go);
            //   OnDeserialize reads to __netId manually so we can use
            //     lookups in the getter (so it still works if objects
            //     move in and out of range repeatedly)
            FieldDefinition netIdField = syncVarNetIds[syncVar];

            // uint oldNetId = ___qNetId;
            VariableDefinition oldNetId = new VariableDefinition(weaverTypes.Import<NetworkBehaviour.NetworkBehaviourSyncVar>());
            deserialize.Body.Variables.Add(oldNetId);
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Ldfld, netIdField);
            worker.Emit(OpCodes.Stloc, oldNetId);

            // GameObject/NetworkIdentity oldSyncVar = syncvar.getter;
            VariableDefinition oldSyncVar = new VariableDefinition(syncVar.FieldType);
            deserialize.Body.Variables.Add(oldSyncVar);
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Ldfld, syncVar);
            worker.Emit(OpCodes.Stloc, oldSyncVar);

            // read id and store in netId field BEFORE calling the hook
            // -> this makes way more sense. by definition, the hook is
            //    supposed to be called after it was changed. not before.
            // -> setting it BEFORE calling the hook fixes the following bug:
            //    https://github.com/vis2k/Mirror/issues/1151 in host mode
            //    where the value during the Hook call would call Cmds on
            //    the host server, and they would all happen and compare
            //    values BEFORE the hook even returned and hence BEFORE the
            //    actual value was even set.
            // put 'this.' onto stack for 'this.netId' below
            worker.Emit(OpCodes.Ldarg_0);
            // reader. for 'reader.Read()' below
            worker.Emit(OpCodes.Ldarg_1);
            // Read()
            worker.Emit(OpCodes.Call, readers.GetReadFunc(weaverTypes.Import<NetworkBehaviour.NetworkBehaviourSyncVar>(), ref WeavingFailed));
            // netId
            worker.Emit(OpCodes.Stfld, netIdField);

            if (hookMethod != null)
            {
                // call Hook(this.GetSyncVarGameObject/NetworkIdentity(reader.ReadPackedUInt32()))
                // because we send/receive the netID, not the GameObject/NetworkIdentity
                // but only if SyncVar changed. otherwise a client would
                // get hook calls for all initial values, even if they
                // didn't change from the default values on the client.
                // see also: https://github.com/vis2k/Mirror/issues/1278

                // IMPORTANT: for GameObjects/NetworkIdentities we usually
                //            use SyncVarGameObjectEqual to compare equality.
                //            in this case however, we can just use
                //            SyncVarEqual with the two uint netIds.
                //            => this is easier weaver code because we don't
                //               have to get the GameObject/NetworkIdentity
                //               from the uint netId
                //            => this is faster because we void one
                //               GetComponent call for GameObjects to get
                //               their NetworkIdentity when comparing.

                // Generates: if (!SyncVarEqual);
                Instruction syncVarEqualLabel = worker.Create(OpCodes.Nop);

                // NOTE: static function. don't Emit Ldarg_0 aka 'this'.

                // 'oldNetId'
                worker.Emit(OpCodes.Ldloc, oldNetId);
                // 'ref this.__netId'
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldflda, netIdField);
                // call the function
                GenericInstanceMethod syncVarEqualGm = new GenericInstanceMethod(weaverTypes.syncVarEqualReference);
                syncVarEqualGm.GenericArguments.Add(netIdField.FieldType);
                worker.Emit(OpCodes.Call, syncVarEqualGm);
                worker.Emit(OpCodes.Brtrue, syncVarEqualLabel);

                // call the hook
                // Generates: OnValueChanged(oldValue, this.syncVar);
                syncVarAttributeProcessor.WriteCallHookMethodUsingField(worker, hookMethod, oldSyncVar, syncVar, ref WeavingFailed);

                // Generates: end if (!SyncVarEqual);
                worker.Append(syncVarEqualLabel);
            }
        }

        // [SyncVar] int/float/struct/etc.?
        void DeserializeNormalField(FieldDefinition syncVar, ILProcessor serWorker, MethodDefinition deserialize, MethodDefinition hookMethod, ref bool WeavingFailed)
        {
            /*
             Generates code like:
                // for hook
                int oldValue = a;
                Networka = reader.ReadPackedInt32();
                if (!SyncVarEqual(oldValue, ref a))
                {
                    OnSetA(oldValue, Networka);
                }
             */

            MethodReference readFunc = readers.GetReadFunc(syncVar.FieldType, ref WeavingFailed);
            if (readFunc == null)
            {
                Log.Error($"{syncVar.Name} has unsupported type. Use a supported Mirror type instead", syncVar);
                WeavingFailed = true;
                return;
            }

            // T oldValue = value;
            VariableDefinition oldValue = new VariableDefinition(syncVar.FieldType);
            deserialize.Body.Variables.Add(oldValue);
            serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
            serWorker.Append(serWorker.Create(OpCodes.Ldfld, syncVar));
            serWorker.Append(serWorker.Create(OpCodes.Stloc, oldValue));

            // read value and store in syncvar BEFORE calling the hook
            // -> this makes way more sense. by definition, the hook is
            //    supposed to be called after it was changed. not before.
            // -> setting it BEFORE calling the hook fixes the following bug:
            //    https://github.com/vis2k/Mirror/issues/1151 in host mode
            //    where the value during the Hook call would call Cmds on
            //    the host server, and they would all happen and compare
            //    values BEFORE the hook even returned and hence BEFORE the
            //    actual value was even set.
            // put 'this.' onto stack for 'this.syncvar' below
            serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
            // reader. for 'reader.Read()' below
            serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
            // reader.Read()
            serWorker.Append(serWorker.Create(OpCodes.Call, readFunc));
            // syncvar
            serWorker.Append(serWorker.Create(OpCodes.Stfld, syncVar));

            if (hookMethod != null)
            {
                // call hook
                // but only if SyncVar changed. otherwise a client would
                // get hook calls for all initial values, even if they
                // didn't change from the default values on the client.
                // see also: https://github.com/vis2k/Mirror/issues/1278

                // Generates: if (!SyncVarEqual);
                Instruction syncVarEqualLabel = serWorker.Create(OpCodes.Nop);

                // NOTE: static function. don't Emit Ldarg_0 aka 'this'.

                // 'oldValue'
                serWorker.Append(serWorker.Create(OpCodes.Ldloc, oldValue));
                // 'ref this.syncVar'
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                serWorker.Append(serWorker.Create(OpCodes.Ldflda, syncVar));
                // call the function
                GenericInstanceMethod syncVarEqualGm = new GenericInstanceMethod(weaverTypes.syncVarEqualReference);
                syncVarEqualGm.GenericArguments.Add(syncVar.FieldType);
                serWorker.Append(serWorker.Create(OpCodes.Call, syncVarEqualGm));
                serWorker.Append(serWorker.Create(OpCodes.Brtrue, syncVarEqualLabel));

                // call the hook
                // Generates: OnValueChanged(oldValue, this.syncVar);
                syncVarAttributeProcessor.WriteCallHookMethodUsingField(serWorker, hookMethod, oldValue, syncVar, ref WeavingFailed);

                // Generates: end if (!SyncVarEqual);
                serWorker.Append(syncVarEqualLabel);
            }
        }

        void GenerateDeSerialization(ref bool WeavingFailed)
        {
            const string DeserializeMethodName = "DeserializeSyncVars";
            if (netBehaviourSubclass.GetMethod(DeserializeMethodName) != null)
                return;

            if (syncVars.Count == 0)
            {
                // no synvars,  no need for custom OnDeserialize
                return;
            }

            MethodDefinition serialize = new MethodDefinition(DeserializeMethodName,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    weaverTypes.Import(typeof(void)));

            serialize.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, weaverTypes.Import<NetworkReader>()));
            serialize.Parameters.Add(new ParameterDefinition("initialState", ParameterAttributes.None, weaverTypes.Import<bool>()));
            ILProcessor serWorker = serialize.Body.GetILProcessor();
            // setup local for dirty bits
            serialize.Body.InitLocals = true;
            VariableDefinition dirtyBitsLocal = new VariableDefinition(weaverTypes.Import<long>());
            serialize.Body.Variables.Add(dirtyBitsLocal);

            MethodReference baseDeserialize = Resolvers.TryResolveMethodInParents(netBehaviourSubclass.BaseType, assembly, DeserializeMethodName);
            if (baseDeserialize != null)
            {
                // base
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                // reader
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                // initialState
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_2));
                serWorker.Append(serWorker.Create(OpCodes.Call, baseDeserialize));
            }

            // Generates: if (initialState);
            Instruction initialStateLabel = serWorker.Create(OpCodes.Nop);

            serWorker.Append(serWorker.Create(OpCodes.Ldarg_2));
            serWorker.Append(serWorker.Create(OpCodes.Brfalse, initialStateLabel));

            foreach (FieldDefinition syncVar in syncVars)
            {
                DeserializeField(syncVar, serWorker, serialize, ref WeavingFailed);
            }

            serWorker.Append(serWorker.Create(OpCodes.Ret));

            // Generates: end if (initialState);
            serWorker.Append(initialStateLabel);

            // get dirty bits
            serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
            serWorker.Append(serWorker.Create(OpCodes.Call, readers.GetReadFunc(weaverTypes.Import<ulong>(), ref WeavingFailed)));
            serWorker.Append(serWorker.Create(OpCodes.Stloc_0));

            // conditionally read each syncvar
            // start at number of syncvars in parent
            int dirtyBit = syncVarAccessLists.GetSyncVarStart(netBehaviourSubclass.BaseType.FullName);
            foreach (FieldDefinition syncVar in syncVars)
            {
                Instruction varLabel = serWorker.Create(OpCodes.Nop);

                // check if dirty bit is set
                serWorker.Append(serWorker.Create(OpCodes.Ldloc_0));
                serWorker.Append(serWorker.Create(OpCodes.Ldc_I8, 1L << dirtyBit));
                serWorker.Append(serWorker.Create(OpCodes.And));
                serWorker.Append(serWorker.Create(OpCodes.Brfalse, varLabel));

                DeserializeField(syncVar, serWorker, serialize, ref WeavingFailed);

                serWorker.Append(varLabel);
                dirtyBit += 1;
            }

            // add a log message if needed for debugging
            //serWorker.Append(serWorker.Create(OpCodes.Ldstr, $"Injected Deserialize {netBehaviourSubclass.Name}"));
            //serWorker.Append(serWorker.Create(OpCodes.Call, WeaverTypes.logErrorReference));

            serWorker.Append(serWorker.Create(OpCodes.Ret));
            netBehaviourSubclass.Methods.Add(serialize);
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
