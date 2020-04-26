// this class processes SyncVars, Cmds, Rpcs, etc. of NetworkBehaviours
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    class NetworkBehaviourProcessor
    {
        readonly List<FieldDefinition> syncVars = new List<FieldDefinition>();
        readonly List<FieldDefinition> syncObjects = new List<FieldDefinition>();
        // <SyncVarField,NetIdField>
        readonly Dictionary<FieldDefinition, FieldDefinition> syncVarNetIds = new Dictionary<FieldDefinition, FieldDefinition>();
        readonly List<MethodDefinition> commands = new List<MethodDefinition>();
        readonly List<MethodDefinition> clientRpcs = new List<MethodDefinition>();
        readonly List<MethodDefinition> targetRpcs = new List<MethodDefinition>();
        readonly List<EventDefinition> eventRpcs = new List<EventDefinition>();
        readonly List<MethodDefinition> commandInvocationFuncs = new List<MethodDefinition>();
        readonly List<MethodDefinition> clientRpcInvocationFuncs = new List<MethodDefinition>();
        readonly List<MethodDefinition> targetRpcInvocationFuncs = new List<MethodDefinition>();
        readonly List<MethodDefinition> eventRpcInvocationFuncs = new List<MethodDefinition>();

        readonly TypeDefinition netBehaviourSubclass;

        public NetworkBehaviourProcessor(TypeDefinition td)
        {
            Weaver.DLog(td, "NetworkBehaviourProcessor");
            netBehaviourSubclass = td;
        }

        public void Process()
        {
            if (netBehaviourSubclass.HasGenericParameters)
            {
                Weaver.Error($"{netBehaviourSubclass.Name} cannot have generic parameters", netBehaviourSubclass);
                return;
            }
            Weaver.DLog(netBehaviourSubclass, "Process Start");
            MarkAsProcessed(netBehaviourSubclass);
            SyncVarProcessor.ProcessSyncVars(netBehaviourSubclass, syncVars, syncObjects, syncVarNetIds);

            ProcessMethods();

            SyncEventProcessor.ProcessEvents(netBehaviourSubclass, eventRpcs, eventRpcInvocationFuncs);
            if (Weaver.WeavingFailed)
            {
                return;
            }
            GenerateConstants();

            GenerateSerialization();
            if (Weaver.WeavingFailed)
            {
                return;
            }

            GenerateDeSerialization();
            Weaver.DLog(netBehaviourSubclass, "Process Done");
        }

        /*
        generates code like:
            if (!obj.netIdentity.client.active)
              Debug.LogError((object) "Command function CmdRespawn called on server.");

            which is used in InvokeCmd, InvokeRpc, etc.
        */
        public static void WriteClientActiveCheck(ILProcessor worker, string mdName, Instruction label, string errString)
        {
            // client active check
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Call, Weaver.NetworkBehaviourGetIdentity));
            worker.Append(worker.Create(OpCodes.Call, Weaver.NetworkIdentityGetClient));
            worker.Append(worker.Create(OpCodes.Call, Weaver.NetworkClientGetActive));
            worker.Append(worker.Create(OpCodes.Brtrue, label));

            worker.Append(worker.Create(OpCodes.Ldstr, errString + " " + mdName + " called on server."));
            worker.Append(worker.Create(OpCodes.Call, Weaver.logErrorReference));
            worker.Append(worker.Create(OpCodes.Ret));
            worker.Append(label);
        }
        /*
        generates code like:
            if (!obj.netIdentity.server.active)
              Debug.LogError((object) "Command CmdMsgWhisper called on client.");
        */
        public static void WriteServerActiveCheck(ILProcessor worker, string mdName, Instruction label, string errString)
        {
            // server active check
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Call, Weaver.NetworkBehaviourGetIdentity));
            worker.Append(worker.Create(OpCodes.Call, Weaver.NetworkIdentityGetServer));
            worker.Append(worker.Create(OpCodes.Call, Weaver.NetworkServerGetActive));
            worker.Append(worker.Create(OpCodes.Brtrue, label));

            worker.Append(worker.Create(OpCodes.Ldstr, errString + " " + mdName + " called on client."));
            worker.Append(worker.Create(OpCodes.Call, Weaver.logErrorReference));
            worker.Append(worker.Create(OpCodes.Ret));
            worker.Append(label);
        }

        public static void WriteSetupLocals(ILProcessor worker)
        {
            worker.Body.InitLocals = true;
            worker.Body.Variables.Add(new VariableDefinition(Weaver.PooledNetworkWriterType));
        }

        public static void WriteCreateWriter(ILProcessor worker)
        {
            // create writer
            worker.Append(worker.Create(OpCodes.Call, Weaver.GetPooledWriterReference));
            worker.Append(worker.Create(OpCodes.Stloc_0));
        }

        public static void WriteRecycleWriter(ILProcessor worker)
        {
            // NetworkWriterPool.Recycle(writer);
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Call, Weaver.RecycleWriterReference));
        }

        public static bool WriteArguments(ILProcessor worker, MethodDefinition md, bool skipFirst)
        {
            // write each argument
            short argNum = 1;
            foreach (ParameterDefinition pd in md.Parameters)
            {
                if (argNum == 1 && skipFirst)
                {
                    argNum += 1;
                    continue;
                }

                MethodReference writeFunc = Writers.GetWriteFunc(pd.ParameterType);
                if (writeFunc == null)
                {
                    Weaver.Error($"{md.Name} has invalid parameter {pd}", md);
                    return false;
                }
                // use built-in writer func on writer object
                worker.Append(worker.Create(OpCodes.Ldloc_0));         // writer object
                worker.Append(worker.Create(OpCodes.Ldarg, argNum));   // argument
                // call writer func on writer object
                worker.Append(worker.Create(OpCodes.Call, writeFunc));
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

        public static void MarkAsProcessed(TypeDefinition td)
        {
            if (!WasProcessed(td))
            {
                var versionMethod = new MethodDefinition(ProcessedFunctionName, MethodAttributes.Private, Weaver.voidType);
                ILProcessor worker = versionMethod.Body.GetILProcessor();
                worker.Append(worker.Create(OpCodes.Ret));
                td.Methods.Add(versionMethod);
            }
        }
        #endregion

        void GenerateConstants()
        {
            if (commands.Count == 0 && clientRpcs.Count == 0 && targetRpcs.Count == 0 && eventRpcs.Count == 0 && syncObjects.Count == 0)
                return;

            Weaver.DLog(netBehaviourSubclass, "  GenerateConstants ");

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
                        Weaver.Error($"{netBehaviourSubclass.Name} has invalid class constructor", cctor);
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
                        Weaver.voidType);
            }

            // find instance constructor
            MethodDefinition ctor = netBehaviourSubclass.GetMethod(".ctor");

            if (ctor == null)
            {
                Weaver.Error($"{netBehaviourSubclass.Name} has invalid constructor", netBehaviourSubclass);
                return;
            }

            Instruction ret = ctor.Body.Instructions[ctor.Body.Instructions.Count - 1];
            if (ret.OpCode == OpCodes.Ret)
            {
                ctor.Body.Instructions.RemoveAt(ctor.Body.Instructions.Count - 1);
            }
            else
            {
                Weaver.Error($"{netBehaviourSubclass.Name} has invalid constructor", ctor);
                return;
            }

            ILProcessor ctorWorker = ctor.Body.GetILProcessor();
            ILProcessor cctorWorker = cctor.Body.GetILProcessor();

            for (int i = 0; i < commands.Count; ++i)
            {
                GenerateRegisterCommandDelegate(cctorWorker, Weaver.registerCommandDelegateReference, commandInvocationFuncs[i], commands[i].Name);
            }

            for (int i = 0; i < clientRpcs.Count; ++i)
            {
                GenerateRegisterCommandDelegate(cctorWorker, Weaver.registerRpcDelegateReference, clientRpcInvocationFuncs[i], clientRpcs[i].Name);
            }

            for (int i = 0; i < targetRpcs.Count; ++i)
            {
                GenerateRegisterCommandDelegate(cctorWorker, Weaver.registerRpcDelegateReference, targetRpcInvocationFuncs[i], targetRpcs[i].Name);
            }

            for (int i = 0; i < eventRpcs.Count; ++i)
            {
                GenerateRegisterCommandDelegate(cctorWorker, Weaver.registerEventDelegateReference, eventRpcInvocationFuncs[i], eventRpcs[i].Name);
            }

            foreach (FieldDefinition fd in syncObjects)
            {
                SyncObjectInitializer.GenerateSyncObjectInitializer(ctorWorker, fd);
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
        void GenerateRegisterCommandDelegate(ILProcessor awakeWorker, MethodReference registerMethod, MethodDefinition func, string cmdName)
        {
            awakeWorker.Append(awakeWorker.Create(OpCodes.Ldtoken, netBehaviourSubclass));
            awakeWorker.Append(awakeWorker.Create(OpCodes.Call, Weaver.getTypeFromHandleReference));
            awakeWorker.Append(awakeWorker.Create(OpCodes.Ldstr, cmdName));
            awakeWorker.Append(awakeWorker.Create(OpCodes.Ldnull));
            awakeWorker.Append(awakeWorker.Create(OpCodes.Ldftn, func));

            awakeWorker.Append(awakeWorker.Create(OpCodes.Newobj, Weaver.CmdDelegateConstructor));
            awakeWorker.Append(awakeWorker.Create(OpCodes.Call, registerMethod));
        }

        void GenerateSerialization()
        {
            Weaver.DLog(netBehaviourSubclass, "  GenerateSerialization");

            const string SerializeMethodName = "SerializeSyncVars";
            if (netBehaviourSubclass.GetMethod(SerializeMethodName) != null)
                return;

            if (syncVars.Count == 0)
            {
                // no synvars,  no need for custom OnSerialize
                return;
            }

            var serialize = new MethodDefinition(SerializeMethodName,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    Weaver.boolType);

            serialize.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkWriterType)));
            serialize.Parameters.Add(new ParameterDefinition("forceAll", ParameterAttributes.None, Weaver.boolType));
            ILProcessor serWorker = serialize.Body.GetILProcessor();

            serialize.Body.InitLocals = true;

            // loc_0,  this local variable is to determine if any variable was dirty
            var dirtyLocal = new VariableDefinition(Weaver.boolType);
            serialize.Body.Variables.Add(dirtyLocal);

            MethodReference baseSerialize = Resolvers.ResolveMethodInParents(netBehaviourSubclass.BaseType, Weaver.CurrentAssembly, SerializeMethodName);
            if (baseSerialize != null)
            {
                // base
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                // writer
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                // forceAll
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_2));
                serWorker.Append(serWorker.Create(OpCodes.Call, baseSerialize));
                // set dirtyLocal to result of base.OnSerialize()
                serWorker.Append(serWorker.Create(OpCodes.Stloc_0));
            }

            // Generates: if (forceAll);
            Instruction initialStateLabel = serWorker.Create(OpCodes.Nop);
            // forceAll
            serWorker.Append(serWorker.Create(OpCodes.Ldarg_2));
            serWorker.Append(serWorker.Create(OpCodes.Brfalse, initialStateLabel));

            foreach (FieldDefinition syncVar in syncVars)
            {
                // Generates a writer call for each sync variable
                // writer
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                // this
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                serWorker.Append(serWorker.Create(OpCodes.Ldfld, syncVar));
                MethodReference writeFunc = Writers.GetWriteFunc(syncVar.FieldType);
                if (writeFunc != null)
                {
                    serWorker.Append(serWorker.Create(OpCodes.Call, writeFunc));
                }
                else
                {
                    Weaver.Error($"{syncVar.Name} has unsupported type. Use a supported Mirror type instead", syncVar);
                    return;
                }
            }

            // always return true if forceAll

            // Generates: return true
            serWorker.Append(serWorker.Create(OpCodes.Ldc_I4_1));
            serWorker.Append(serWorker.Create(OpCodes.Ret));

            // Generates: end if (forceAll);
            serWorker.Append(initialStateLabel);

            // write dirty bits before the data fields
            // Generates: writer.WritePackedUInt64 (base.get_syncVarDirtyBits ());
            // writer
            serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
            // base
            serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
            serWorker.Append(serWorker.Create(OpCodes.Call, Weaver.NetworkBehaviourDirtyBitsReference));
            serWorker.Append(serWorker.Create(OpCodes.Call, Writers.GetWriteFunc(Weaver.uint64Type)));

            // generate a writer call for any dirty variable in this class

            // start at number of syncvars in parent
            int dirtyBit = Weaver.GetSyncVarStart(netBehaviourSubclass.BaseType.FullName);
            foreach (FieldDefinition syncVar in syncVars)
            {
                Instruction varLabel = serWorker.Create(OpCodes.Nop);

                // Generates: if ((base.get_syncVarDirtyBits() & 1uL) != 0uL)
                // base
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                serWorker.Append(serWorker.Create(OpCodes.Call, Weaver.NetworkBehaviourDirtyBitsReference));
                // 8 bytes = long
                serWorker.Append(serWorker.Create(OpCodes.Ldc_I8, 1L << dirtyBit));
                serWorker.Append(serWorker.Create(OpCodes.And));
                serWorker.Append(serWorker.Create(OpCodes.Brfalse, varLabel));

                // Generates a call to the writer for that field
                // writer
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                // base
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                serWorker.Append(serWorker.Create(OpCodes.Ldfld, syncVar));

                MethodReference writeFunc = Writers.GetWriteFunc(syncVar.FieldType);
                if (writeFunc != null)
                {
                    serWorker.Append(serWorker.Create(OpCodes.Call, writeFunc));
                }
                else
                {
                    Weaver.Error($"{syncVar.Name} has unsupported type. Use a supported Mirror type instead", syncVar);
                    return;
                }

                // something was dirty
                serWorker.Append(serWorker.Create(OpCodes.Ldc_I4_1));
                // set dirtyLocal to true
                serWorker.Append(serWorker.Create(OpCodes.Stloc_0));

                serWorker.Append(varLabel);
                dirtyBit += 1;
            }

            if (Weaver.GenerateLogErrors)
            {
                serWorker.Append(serWorker.Create(OpCodes.Ldstr, "Injected Serialize " + netBehaviourSubclass.Name));
                serWorker.Append(serWorker.Create(OpCodes.Call, Weaver.logErrorReference));
            }

            // generate: return dirtyLocal
            serWorker.Append(serWorker.Create(OpCodes.Ldloc_0));
            serWorker.Append(serWorker.Create(OpCodes.Ret));
            netBehaviourSubclass.Methods.Add(serialize);
        }

        void DeserializeField(FieldDefinition syncVar, ILProcessor serWorker, MethodDefinition deserialize)
        {
            // check for Hook function
            MethodDefinition hookMethod = SyncVarProcessor.GetHookMethod(netBehaviourSubclass, syncVar);

            // [SyncVar] GameObject/NetworkIdentity?
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
            if (syncVar.FieldType.FullName == Weaver.gameObjectType.FullName ||
                syncVar.FieldType.FullName == Weaver.NetworkIdentityType.FullName)
            {
                // GameObject/NetworkIdentity SyncVar:
                //   OnSerialize sends writer.Write(go);
                //   OnDeserialize reads to __netId manually so we can use
                //     lookups in the getter (so it still works if objects
                //     move in and out of range repeatedly)
                FieldDefinition netIdField = syncVarNetIds[syncVar];

                // uint oldNetId = ___qNetId;
                var oldNetId = new VariableDefinition(Weaver.uint32Type);
                deserialize.Body.Variables.Add(oldNetId);
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                serWorker.Append(serWorker.Create(OpCodes.Ldfld, netIdField));
                serWorker.Append(serWorker.Create(OpCodes.Stloc, oldNetId));

                // GameObject/NetworkIdentity oldSyncVar = syncvar.getter;
                var oldSyncVar = new VariableDefinition(syncVar.FieldType);
                deserialize.Body.Variables.Add(oldSyncVar);
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                serWorker.Append(serWorker.Create(OpCodes.Ldfld, syncVar));
                serWorker.Append(serWorker.Create(OpCodes.Stloc, oldSyncVar));

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
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                // reader. for 'reader.Read()' below
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                // Read()
                serWorker.Append(serWorker.Create(OpCodes.Call, Readers.GetReadFunc(Weaver.uint32Type)));
                // netId
                serWorker.Append(serWorker.Create(OpCodes.Stfld, netIdField));

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
                    Instruction syncVarEqualLabel = serWorker.Create(OpCodes.Nop);

                    // 'this.' for 'this.SyncVarEqual'
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    // 'oldNetId'
                    serWorker.Append(serWorker.Create(OpCodes.Ldloc, oldNetId));
                    // 'ref this.__netId'
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    serWorker.Append(serWorker.Create(OpCodes.Ldflda, netIdField));
                    // call the function
                    var syncVarEqualGm = new GenericInstanceMethod(Weaver.syncVarEqualReference);
                    syncVarEqualGm.GenericArguments.Add(netIdField.FieldType);
                    serWorker.Append(serWorker.Create(OpCodes.Call, syncVarEqualGm));
                    serWorker.Append(serWorker.Create(OpCodes.Brtrue, syncVarEqualLabel));

                    // call the hook
                    // this.
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    // oldSyncVar GO/NI
                    serWorker.Append(serWorker.Create(OpCodes.Ldloc, oldSyncVar));
                    // this.
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    // syncvar.get (finds current GO/NI from netId)
                    serWorker.Append(serWorker.Create(OpCodes.Ldfld, syncVar));
                    serWorker.Append(serWorker.Create(OpCodes.Callvirt, hookMethod));

                    // Generates: end if (!SyncVarEqual);
                    serWorker.Append(syncVarEqualLabel);
                }
            }
            // [SyncVar] int/float/struct/etc.?
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
            else
            {
                MethodReference readFunc = Readers.GetReadFunc(syncVar.FieldType);
                if (readFunc == null)
                {
                    Weaver.Error($"{syncVar.Name} has unsupported type. Use a supported Mirror type instead", syncVar);
                    return;
                }

                // T oldValue = value;
                var oldValue = new VariableDefinition(syncVar.FieldType);
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

                    // 'this.' for 'this.SyncVarEqual'
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    // 'oldValue'
                    serWorker.Append(serWorker.Create(OpCodes.Ldloc, oldValue));
                    // 'ref this.syncVar'
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    serWorker.Append(serWorker.Create(OpCodes.Ldflda, syncVar));
                    // call the function
                    var syncVarEqualGm = new GenericInstanceMethod(Weaver.syncVarEqualReference);
                    syncVarEqualGm.GenericArguments.Add(syncVar.FieldType);
                    serWorker.Append(serWorker.Create(OpCodes.Call, syncVarEqualGm));
                    serWorker.Append(serWorker.Create(OpCodes.Brtrue, syncVarEqualLabel));

                    // call the hook
                    // this.
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    // oldvalue
                    serWorker.Append(serWorker.Create(OpCodes.Ldloc, oldValue));
                    // this.
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    // syncvar.get
                    serWorker.Append(serWorker.Create(OpCodes.Ldfld, syncVar));
                    serWorker.Append(serWorker.Create(OpCodes.Callvirt, hookMethod));

                    // Generates: end if (!SyncVarEqual);
                    serWorker.Append(syncVarEqualLabel);
                }
            }
        }

        void GenerateDeSerialization()
        {
            Weaver.DLog(netBehaviourSubclass, "  GenerateDeSerialization");

            const string DeserializeMethodName = "DeserializeSyncVars";
            if (netBehaviourSubclass.GetMethod(DeserializeMethodName) != null)
                return;

            if (syncVars.Count == 0)
            {
                // no synvars,  no need for custom OnDeserialize
                return;
            }

            var serialize = new MethodDefinition(DeserializeMethodName,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    Weaver.voidType);

            serialize.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkReaderType)));
            serialize.Parameters.Add(new ParameterDefinition("initialState", ParameterAttributes.None, Weaver.boolType));
            ILProcessor serWorker = serialize.Body.GetILProcessor();
            // setup local for dirty bits
            serialize.Body.InitLocals = true;
            var dirtyBitsLocal = new VariableDefinition(Weaver.int64Type);
            serialize.Body.Variables.Add(dirtyBitsLocal);

            MethodReference baseDeserialize = Resolvers.ResolveMethodInParents(netBehaviourSubclass.BaseType, Weaver.CurrentAssembly, DeserializeMethodName);
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
                DeserializeField(syncVar, serWorker, serialize);
            }

            serWorker.Append(serWorker.Create(OpCodes.Ret));

            // Generates: end if (initialState);
            serWorker.Append(initialStateLabel);

            // get dirty bits
            serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
            serWorker.Append(serWorker.Create(OpCodes.Call, Readers.GetReadFunc(Weaver.uint64Type)));
            serWorker.Append(serWorker.Create(OpCodes.Stloc_0));

            // conditionally read each syncvar
            // start at number of syncvars in parent
            int dirtyBit = Weaver.GetSyncVarStart(netBehaviourSubclass.BaseType.FullName);
            foreach (FieldDefinition syncVar in syncVars)
            {
                Instruction varLabel = serWorker.Create(OpCodes.Nop);

                // check if dirty bit is set
                serWorker.Append(serWorker.Create(OpCodes.Ldloc_0));
                serWorker.Append(serWorker.Create(OpCodes.Ldc_I8, 1L << dirtyBit));
                serWorker.Append(serWorker.Create(OpCodes.And));
                serWorker.Append(serWorker.Create(OpCodes.Brfalse, varLabel));

                DeserializeField(syncVar, serWorker, serialize);

                serWorker.Append(varLabel);
                dirtyBit += 1;
            }

            if (Weaver.GenerateLogErrors)
            {
                serWorker.Append(serWorker.Create(OpCodes.Ldstr, "Injected Deserialize " + netBehaviourSubclass.Name));
                serWorker.Append(serWorker.Create(OpCodes.Call, Weaver.logErrorReference));
            }

            serWorker.Append(serWorker.Create(OpCodes.Ret));
            netBehaviourSubclass.Methods.Add(serialize);
        }

        public static bool ProcessNetworkReaderParameters(MethodDefinition md, ILProcessor worker, bool skipFirst)
        {
            int count = 0;

            // read cmd args from NetworkReader
            foreach (ParameterDefinition arg in md.Parameters)
            {
                if (count++ == 0 && skipFirst)
                {
                    continue;
                }
                //?
                MethodReference readFunc = Readers.GetReadFunc(arg.ParameterType);

                if (readFunc != null)
                {
                    worker.Append(worker.Create(OpCodes.Ldarg_1));
                    worker.Append(worker.Create(OpCodes.Call, readFunc));

                    // conversion.. is this needed?
                    if (arg.ParameterType.FullName == Weaver.singleType.FullName)
                    {
                        worker.Append(worker.Create(OpCodes.Conv_R4));
                    }
                    else if (arg.ParameterType.FullName == Weaver.doubleType.FullName)
                    {
                        worker.Append(worker.Create(OpCodes.Conv_R8));
                    }
                }
                else
                {
                    Weaver.Error($"{md.Name} has invalid parameter {arg}.  Unsupported type {arg.ParameterType},  use a supported Mirror type instead", md);
                    return false;
                }
            }
            return true;
        }

        public static void AddInvokeParameters(ICollection<ParameterDefinition> collection)
        {
            collection.Add(new ParameterDefinition("obj", ParameterAttributes.None, Weaver.NetworkBehaviourType2));
            collection.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkReaderType)));
        }

        public static bool ProcessMethodsValidateFunction(MethodReference md)
        {
            if (md.ReturnType.FullName == Weaver.IEnumeratorType.FullName)
            {
                Weaver.Error($"{md.Name} cannot be a coroutine", md);
                return false;
            }
            if (md.ReturnType.FullName != Weaver.voidType.FullName)
            {
                Weaver.Error($"{md.Name} cannot return a value.  Make it void instead", md);
                return false;
            }
            if (md.HasGenericParameters)
            {
                Weaver.Error($"{md.Name} cannot have generic parameters", md);
                return false;
            }
            return true;
        }

        public static bool ProcessMethodsValidateParameters(MethodReference md, CustomAttribute ca)
        {
            for (int i = 0; i < md.Parameters.Count; ++i)
            {
                ParameterDefinition p = md.Parameters[i];
                if (p.IsOut)
                {
                    Weaver.Error($"{md.Name} cannot have out parameters", md);
                    return false;
                }
                if (p.IsOptional)
                {
                    Weaver.Error($"{md.Name} cannot have optional parameters", md);
                    return false;
                }
                // TargetRPC is an exception to this rule and can have a NetworkConnection as first parameter
                if (p.ParameterType.FullName == Weaver.INetworkConnectionType.FullName &&
                    !(ca.AttributeType.FullName == Weaver.TargetRpcType.FullName && i == 0))
                {
                    Weaver.Error($"{md.Name} has invalid parameer {p}. Cannot pass NeworkConnections", md);
                    return false;
                }
            }
            return true;
        }

        void ProcessMethods()
        {
            var names = new HashSet<string>();

            // copy the list of methods because we will be adding methods in the loop
            var methods = new List<MethodDefinition>(netBehaviourSubclass.Methods);
            // find command and RPC functions
            foreach (MethodDefinition md in methods)
            {
                foreach (CustomAttribute ca in md.CustomAttributes)
                {
                    if (ca.AttributeType.FullName == Weaver.CommandType.FullName)
                    {
                        ProcessCommand(names, md, ca);
                        break;
                    }

                    if (ca.AttributeType.FullName == Weaver.TargetRpcType.FullName)
                    {
                        ProcessTargetRpc(names, md, ca);
                        break;
                    }

                    if (ca.AttributeType.FullName == Weaver.ClientRpcType.FullName)
                    {
                        ProcessClientRpc(names, md, ca);
                        break;
                    }
                }
            }
        }

        void ProcessClientRpc(HashSet<string> names, MethodDefinition md, CustomAttribute ca)
        {
            if (!RpcProcessor.ProcessMethodsValidateRpc(md, ca))
            {
                return;
            }

            if (names.Contains(md.Name))
            {
                Weaver.Error($"Duplicate ClientRpc name {md.Name}", md);
                return;
            }
            names.Add(md.Name);
            clientRpcs.Add(md);

            MethodDefinition rpcCallFunc = RpcProcessor.ProcessRpcCall(netBehaviourSubclass, md, ca);

            MethodDefinition rpcFunc = RpcProcessor.ProcessRpcInvoke(netBehaviourSubclass, md, rpcCallFunc);
            if (rpcFunc != null)
            {
                clientRpcInvocationFuncs.Add(rpcFunc);
            }
        }

        void ProcessTargetRpc(HashSet<string> names, MethodDefinition md, CustomAttribute ca)
        {
            if (!TargetRpcProcessor.ProcessMethodsValidateTargetRpc(md, ca))
                return;

            if (names.Contains(md.Name))
            {
                Weaver.Error($"Duplicate Target Rpc name {md.Name}", md);
                return;
            }
            names.Add(md.Name);
            targetRpcs.Add(md);

            MethodDefinition rpcCallFunc = TargetRpcProcessor.ProcessTargetRpcCall(netBehaviourSubclass, md, ca);

            MethodDefinition rpcFunc = TargetRpcProcessor.ProcessTargetRpcInvoke(netBehaviourSubclass, md, rpcCallFunc);
            if (rpcFunc != null)
            {
                targetRpcInvocationFuncs.Add(rpcFunc);
            }
        }

        void ProcessCommand(HashSet<string> names, MethodDefinition md, CustomAttribute ca)
        {
            if (!CommandProcessor.ProcessMethodsValidateCommand(md, ca))
                return;

            if (names.Contains(md.Name))
            {
                Weaver.Error($"Duplicate Command name {md.Name}", md);
                return;
            }

            names.Add(md.Name);
            commands.Add(md);

            MethodDefinition cmdCallFunc = CommandProcessor.ProcessCommandCall(netBehaviourSubclass, md, ca);

            MethodDefinition cmdFunc = CommandProcessor.ProcessCommandInvoke(netBehaviourSubclass, md, cmdCallFunc);
            if (cmdFunc != null)
            {
                commandInvocationFuncs.Add(cmdFunc);
            }
        }
    }
}
