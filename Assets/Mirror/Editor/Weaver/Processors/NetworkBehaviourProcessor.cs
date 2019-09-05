// this class processes SyncVars, Cmds, Rpcs, etc. of NetworkBehaviours
using System;
using System.Linq;
using System.Collections.Generic;
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    class NetworkBehaviourProcessor
    {
        readonly List<FieldDefinition> syncVars = new List<FieldDefinition>();
        readonly List<FieldDefinition> syncObjects = new List<FieldDefinition>();
        readonly Dictionary<FieldDefinition, FieldDefinition> syncVarNetIds = new Dictionary<FieldDefinition, FieldDefinition>(); // <SyncVarField,NetIdField>
        readonly List<MethodDefinition> commands = new List<MethodDefinition>();
        readonly List<MethodDefinition> clientRpcs = new List<MethodDefinition>();
        readonly List<MethodDefinition> targetRpcs = new List<MethodDefinition>();
        readonly List<EventDefinition> eventRpcs = new List<EventDefinition>();
        readonly List<MethodDefinition> commandInvocationFuncs = new List<MethodDefinition>();
        readonly List<MethodDefinition> clientRpcInvocationFuncs = new List<MethodDefinition>();
        readonly List<MethodDefinition> targetRpcInvocationFuncs = new List<MethodDefinition>();
        readonly List<MethodDefinition> eventRpcInvocationFuncs = new List<MethodDefinition>();

        readonly List<MethodDefinition> commandCallFuncs = new List<MethodDefinition>();
        readonly List<MethodDefinition> clientRpcCallFuncs = new List<MethodDefinition>();
        readonly List<MethodDefinition> targetRpcCallFuncs = new List<MethodDefinition>();

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
                Weaver.Error($"{netBehaviourSubclass} cannot have generic parameters");
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
            if (!NetworkClient.active)
              Debug.LogError((object) "Command function CmdRespawn called on server.");

            which is used in InvokeCmd, InvokeRpc, etc.
        */
        public static void WriteClientActiveCheck(ILProcessor worker, string mdName, Instruction label, string errString)
        {
            // client active check
            worker.Append(worker.Create(OpCodes.Call, Weaver.NetworkClientGetActive));
            worker.Append(worker.Create(OpCodes.Brtrue, label));

            worker.Append(worker.Create(OpCodes.Ldstr, errString + " " + mdName + " called on server."));
            worker.Append(worker.Create(OpCodes.Call, Weaver.logErrorReference));
            worker.Append(worker.Create(OpCodes.Ret));
            worker.Append(label);
        }
        /*
        generates code like:
            if (!NetworkServer.active)
              Debug.LogError((object) "Command CmdMsgWhisper called on client.");
        */
        public static void WriteServerActiveCheck(ILProcessor worker, string mdName, Instruction label, string errString)
        {
            // server active check
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
            worker.Body.Variables.Add(new VariableDefinition(Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkWriterType)));
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
                    Weaver.Error($"{md} has invalid parameter {pd}" );
                    return false;
                }
                // use built-in writer func on writer object
                worker.Append(worker.Create(OpCodes.Ldloc_0));         // writer object
                worker.Append(worker.Create(OpCodes.Ldarg, argNum));   // argument
                worker.Append(worker.Create(OpCodes.Call, writeFunc)); // call writer func on writer object
                argNum += 1;
            }
            return true;
        }

        #region mark / check type as processed
        public const string ProcessedFunctionName = "MirrorProcessed";

        // by adding an empty MirrorProcessed() function
        public static bool WasProcessed(TypeDefinition td)
        {
            return td.Methods.Any(method => method.Name == ProcessedFunctionName);
        }

        public static void MarkAsProcessed(TypeDefinition td)
        {
            if (!WasProcessed(td))
            {
                MethodDefinition versionMethod = new MethodDefinition(ProcessedFunctionName, MethodAttributes.Private, Weaver.voidType);
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
            MethodDefinition cctor = null;
            bool cctorFound = false;
            foreach (MethodDefinition md in netBehaviourSubclass.Methods)
            {
                if (md.Name == ".cctor")
                {
                    cctor = md;
                    cctorFound = true;
                }
            }
            if (cctor != null)
            {
                // remove the return opcode from end of function. will add our own later.
                if (cctor.Body.Instructions.Count != 0)
                {
                    Instruction ret = cctor.Body.Instructions[cctor.Body.Instructions.Count - 1];
                    if (ret.OpCode == OpCodes.Ret)
                    {
                        cctor.Body.Instructions.RemoveAt(cctor.Body.Instructions.Count - 1);
                    }
                    else
                    {
                        Weaver.Error($"{netBehaviourSubclass} has invalid class constructor");
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
            MethodDefinition ctor = null;

            foreach (MethodDefinition md in netBehaviourSubclass.Methods)
            {
                if (md.Name == ".ctor")
                {
                    ctor = md;

                    Instruction ret = ctor.Body.Instructions[ctor.Body.Instructions.Count - 1];
                    if (ret.OpCode == OpCodes.Ret)
                    {
                        ctor.Body.Instructions.RemoveAt(ctor.Body.Instructions.Count - 1);
                    }
                    else
                    {
                        Weaver.Error($"{netBehaviourSubclass} has invalid constructor");
                        return;
                    }

                    break;
                }
            }

            if (ctor == null)
            {
                Weaver.Error($"{netBehaviourSubclass} has invalid constructor");
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

            foreach (MethodDefinition m in netBehaviourSubclass.Methods)
            {
                if (m.Name == "OnSerialize")
                    return;
            }

            if (syncVars.Count == 0)
            {
                // no synvars,  no need for custom OnSerialize
                return;
            }

            MethodDefinition serialize = new MethodDefinition("OnSerialize",
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    Weaver.boolType);

            serialize.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkWriterType)));
            serialize.Parameters.Add(new ParameterDefinition("forceAll", ParameterAttributes.None, Weaver.boolType));
            ILProcessor serWorker = serialize.Body.GetILProcessor();

            serialize.Body.InitLocals = true;

            // loc_0,  this local variable is to determine if any variable was dirty
            VariableDefinition dirtyLocal = new VariableDefinition(Weaver.boolType);
            serialize.Body.Variables.Add(dirtyLocal);

            MethodReference baseSerialize = Resolvers.ResolveMethodInParents(netBehaviourSubclass.BaseType, Weaver.CurrentAssembly, "OnSerialize");
            if (baseSerialize != null)
            {
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0)); // base
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1)); // writer
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_2)); // forceAll
                serWorker.Append(serWorker.Create(OpCodes.Call, baseSerialize));
                serWorker.Append(serWorker.Create(OpCodes.Stloc_0)); // set dirtyLocal to result of base.OnSerialize()
            }

            // Generates: if (forceAll);
            Instruction initialStateLabel = serWorker.Create(OpCodes.Nop);
            serWorker.Append(serWorker.Create(OpCodes.Ldarg_2)); // forceAll
            serWorker.Append(serWorker.Create(OpCodes.Brfalse, initialStateLabel));

            foreach (FieldDefinition syncVar in syncVars)
            {
                // Generates a writer call for each sync variable
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1)); // writer
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0)); // this
                serWorker.Append(serWorker.Create(OpCodes.Ldfld, syncVar));
                MethodReference writeFunc = Writers.GetWriteFunc(syncVar.FieldType);
                if (writeFunc != null)
                {
                    serWorker.Append(serWorker.Create(OpCodes.Call, writeFunc));
                }
                else
                {
                    Weaver.Error($"{syncVar} has unsupported type. Use a supported Mirror type instead");
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
            serWorker.Append(serWorker.Create(OpCodes.Ldarg_1)); // writer
            serWorker.Append(serWorker.Create(OpCodes.Ldarg_0)); // base
            serWorker.Append(serWorker.Create(OpCodes.Call, Weaver.NetworkBehaviourDirtyBitsReference));
            serWorker.Append(serWorker.Create(OpCodes.Call, Writers.GetWriteFunc(Weaver.uint64Type)));

            // generate a writer call for any dirty variable in this class

            // start at number of syncvars in parent
            int dirtyBit = Weaver.GetSyncVarStart(netBehaviourSubclass.BaseType.FullName);
            foreach (FieldDefinition syncVar in syncVars)
            {
                Instruction varLabel = serWorker.Create(OpCodes.Nop);

                // Generates: if ((base.get_syncVarDirtyBits() & 1uL) != 0uL)
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0)); // base
                serWorker.Append(serWorker.Create(OpCodes.Call, Weaver.NetworkBehaviourDirtyBitsReference));
                serWorker.Append(serWorker.Create(OpCodes.Ldc_I8, 1L << dirtyBit)); // 8 bytes = long
                serWorker.Append(serWorker.Create(OpCodes.And));
                serWorker.Append(serWorker.Create(OpCodes.Brfalse, varLabel));

                // Generates a call to the writer for that field
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1)); // writer
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0)); // base
                serWorker.Append(serWorker.Create(OpCodes.Ldfld, syncVar));

                MethodReference writeFunc = Writers.GetWriteFunc(syncVar.FieldType);
                if (writeFunc != null)
                {
                    serWorker.Append(serWorker.Create(OpCodes.Call, writeFunc));
                }
                else
                {
                    Weaver.Error($"{syncVar} has unsupported type. Use a supported Mirror type instead");
                    return;
                }

                // something was dirty
                serWorker.Append(serWorker.Create(OpCodes.Ldc_I4_1));
                serWorker.Append(serWorker.Create(OpCodes.Stloc_0)); // set dirtyLocal to true

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

        public static int GetChannelId(CustomAttribute ca)
        {
            foreach (CustomAttributeNamedArgument customField in ca.Fields)
            {
                if (customField.Name == "channel")
                {
                    return (int)customField.Argument.Value;
                }
            }

            return 0;
        }

        void DeserializeField(FieldDefinition syncVar, ILProcessor serWorker, MethodDefinition deserialize)
        {
            // check for Hook function
            if (!SyncVarProcessor.CheckForHookFunction(netBehaviourSubclass, syncVar, out MethodDefinition foundMethod))
            {
                return;
            }

            if (syncVar.FieldType.FullName == Weaver.gameObjectType.FullName ||
                syncVar.FieldType.FullName == Weaver.NetworkIdentityType.FullName)
            {
                // GameObject/NetworkIdentity SyncVar:
                //   OnSerialize sends writer.Write(go);
                //   OnDeserialize reads to __netId manually so we can use
                //     lookups in the getter (so it still works if objects
                //     move in and out of range repeatedly)
                FieldDefinition netIdField = syncVarNetIds[syncVar];

                VariableDefinition tmpValue = new VariableDefinition(Weaver.uint32Type);
                deserialize.Body.Variables.Add(tmpValue);

                // read id and store in a local variable
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                serWorker.Append(serWorker.Create(OpCodes.Call, Readers.GetReadFunc(Weaver.uint32Type)));
                serWorker.Append(serWorker.Create(OpCodes.Stloc, tmpValue));

                if (foundMethod != null)
                {
                    // call Hook(this.GetSyncVarGameObject/NetworkIdentity(reader.ReadPackedUInt32()))
                    // because we send/receive the netID, not the GameObject/NetworkIdentity
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0)); // this.
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    serWorker.Append(serWorker.Create(OpCodes.Ldloc, tmpValue));
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    serWorker.Append(serWorker.Create(OpCodes.Ldflda, syncVar));
                    if (syncVar.FieldType.FullName == Weaver.gameObjectType.FullName)
                        serWorker.Append(serWorker.Create(OpCodes.Callvirt, Weaver.getSyncVarGameObjectReference));
                    else if (syncVar.FieldType.FullName == Weaver.NetworkIdentityType.FullName)
                        serWorker.Append(serWorker.Create(OpCodes.Callvirt, Weaver.getSyncVarNetworkIdentityReference));
                    serWorker.Append(serWorker.Create(OpCodes.Call, foundMethod));
                }
                // set the netid field
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                serWorker.Append(serWorker.Create(OpCodes.Ldloc, tmpValue));
                serWorker.Append(serWorker.Create(OpCodes.Stfld, netIdField));
            }
            else
            {
                MethodReference readFunc = Readers.GetReadFunc(syncVar.FieldType);
                if (readFunc == null)
                {
                    Weaver.Error($"{syncVar} has unsupported type. Use a supported Mirror type instead");
                    return;
                }
                VariableDefinition tmpValue = new VariableDefinition(syncVar.FieldType);
                deserialize.Body.Variables.Add(tmpValue);

                // read value and put it in a local variable
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                serWorker.Append(serWorker.Create(OpCodes.Call, readFunc));
                serWorker.Append(serWorker.Create(OpCodes.Stloc, tmpValue));

                if (foundMethod != null)
                {
                    // call hook
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    serWorker.Append(serWorker.Create(OpCodes.Ldloc, tmpValue));
                    serWorker.Append(serWorker.Create(OpCodes.Call, foundMethod));
                }
                // set the property
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                serWorker.Append(serWorker.Create(OpCodes.Ldloc, tmpValue));
                serWorker.Append(serWorker.Create(OpCodes.Stfld, syncVar));
            }

        }

        void GenerateDeSerialization()
        {
            Weaver.DLog(netBehaviourSubclass, "  GenerateDeSerialization");

            foreach (MethodDefinition m in netBehaviourSubclass.Methods)
            {
                if (m.Name == "OnDeserialize")
                    return;
            }

            if (syncVars.Count == 0)
            {
                // no synvars,  no need for custom OnDeserialize
                return;
            }

            MethodDefinition serialize = new MethodDefinition("OnDeserialize",
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    Weaver.voidType);

            serialize.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkReaderType)));
            serialize.Parameters.Add(new ParameterDefinition("initialState", ParameterAttributes.None, Weaver.boolType));
            ILProcessor serWorker = serialize.Body.GetILProcessor();
            // setup local for dirty bits
            serialize.Body.InitLocals = true;
            VariableDefinition dirtyBitsLocal = new VariableDefinition(Weaver.int64Type);
            serialize.Body.Variables.Add(dirtyBitsLocal);

            MethodReference baseDeserialize = Resolvers.ResolveMethodInParents(netBehaviourSubclass.BaseType, Weaver.CurrentAssembly, "OnDeserialize");
            if (baseDeserialize != null)
            {
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0)); // base
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1)); // reader
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_2)); // initialState
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
            int dirtyBit = Weaver.GetSyncVarStart(netBehaviourSubclass.BaseType.FullName); // start at number of syncvars in parent
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
                MethodReference readFunc = Readers.GetReadFunc(arg.ParameterType); //?

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
                    Weaver.Error($"{md} has invalid parameter {arg}.  Unsupported type {arg.ParameterType},  use a supported Mirror type instead");
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
                Weaver.Error($"{md} cannot be a coroutine");
                return false;
            }
            if (md.ReturnType.FullName != Weaver.voidType.FullName)
            {
                Weaver.Error($"{md} cannot return a value.  Make it void instead");
                return false;
            }
            if (md.HasGenericParameters)
            {
                Weaver.Error($"{md} cannot have generic parameters");
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
                    Weaver.Error($"{md} cannot have out parameters");
                    return false;
                }
                if (p.IsOptional)
                {
                    Weaver.Error($"{md} cannot have optional parameters");
                    return false;
                }
                if (p.ParameterType.Resolve().IsAbstract)
                {
                    Weaver.Error($"{md} has invalid parameter {p}.  Use concrete type instead of abstract type {p.ParameterType}");
                    return false;
                }
                if (p.ParameterType.IsByReference)
                {
                    Weaver.Error($"{md} has invalid parameter {p}. Use supported type instead of reference type {p.ParameterType}");
                    return false;
                }
                // TargetRPC is an exception to this rule and can have a NetworkConnection as first parameter
                if (p.ParameterType.FullName == Weaver.NetworkConnectionType.FullName &&
                    !(ca.AttributeType.FullName == Weaver.TargetRpcType.FullName && i == 0))
                {
                    Weaver.Error($"{md} has invalid parameer {p}. Cannot pass NeworkConnections");
                    return false;
                }
                if (p.ParameterType.Resolve().IsDerivedFrom(Weaver.ComponentType))
                {
                    if (p.ParameterType.FullName != Weaver.NetworkIdentityType.FullName)
                    {
                        Weaver.Error($"{md} has invalid parameter {p}. Cannot pass components in remote method calls");
                        return false;
                    }
                }
            }
            return true;
        }

        void ProcessMethods()
        {
            HashSet<string> names = new HashSet<string>();

            // find command and RPC functions
            foreach (MethodDefinition md in netBehaviourSubclass.Methods)
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

            // cmds
            foreach (MethodDefinition md in commandInvocationFuncs)
            {
                netBehaviourSubclass.Methods.Add(md);
            }
            foreach (MethodDefinition md in commandCallFuncs)
            {
                netBehaviourSubclass.Methods.Add(md);
            }

            // rpcs
            foreach (MethodDefinition md in clientRpcInvocationFuncs)
            {
                netBehaviourSubclass.Methods.Add(md);
            }
            foreach (MethodDefinition md in targetRpcInvocationFuncs)
            {
                netBehaviourSubclass.Methods.Add(md);
            }
            foreach (MethodDefinition md in clientRpcCallFuncs)
            {
                netBehaviourSubclass.Methods.Add(md);
            }
            foreach (MethodDefinition md in targetRpcCallFuncs)
            {
                netBehaviourSubclass.Methods.Add(md);
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
                Weaver.Error("Duplicate ClientRpc name [" + netBehaviourSubclass.FullName + ":" + md.Name + "]");
                return;
            }
            names.Add(md.Name);
            clientRpcs.Add(md);

            MethodDefinition rpcFunc = RpcProcessor.ProcessRpcInvoke(netBehaviourSubclass, md);
            if (rpcFunc != null)
            {
                clientRpcInvocationFuncs.Add(rpcFunc);
            }

            MethodDefinition rpcCallFunc = RpcProcessor.ProcessRpcCall(netBehaviourSubclass, md, ca);
            if (rpcCallFunc != null)
            {
                clientRpcCallFuncs.Add(rpcCallFunc);
                Weaver.WeaveLists.replaceMethods[md.FullName] = rpcCallFunc;
            }
        }

        void ProcessTargetRpc(HashSet<string> names, MethodDefinition md, CustomAttribute ca)
        {
            if (!TargetRpcProcessor.ProcessMethodsValidateTargetRpc(md, ca))
                return;

            if (names.Contains(md.Name))
            {
                Weaver.Error("Duplicate Target Rpc name [" + netBehaviourSubclass.FullName + ":" + md.Name + "]");
                return;
            }
            names.Add(md.Name);
            targetRpcs.Add(md);

            MethodDefinition rpcFunc = TargetRpcProcessor.ProcessTargetRpcInvoke(netBehaviourSubclass, md);
            if (rpcFunc != null)
            {
                targetRpcInvocationFuncs.Add(rpcFunc);
            }

            MethodDefinition rpcCallFunc = TargetRpcProcessor.ProcessTargetRpcCall(netBehaviourSubclass, md, ca);
            if (rpcCallFunc != null)
            {
                targetRpcCallFuncs.Add(rpcCallFunc);
                Weaver.WeaveLists.replaceMethods[md.FullName] = rpcCallFunc;
            }
        }

        void ProcessCommand(HashSet<string> names, MethodDefinition md, CustomAttribute ca)
        {
            if (!CommandProcessor.ProcessMethodsValidateCommand(md, ca))
                return;

            if (names.Contains(md.Name))
            {
                Weaver.Error("Duplicate Command name [" + netBehaviourSubclass.FullName + ":" + md.Name + "]");
                return;
            }

            names.Add(md.Name);
            commands.Add(md);

            MethodDefinition cmdFunc = CommandProcessor.ProcessCommandInvoke(netBehaviourSubclass, md);
            if (cmdFunc != null)
            {
                commandInvocationFuncs.Add(cmdFunc);
            }

            MethodDefinition cmdCallFunc = CommandProcessor.ProcessCommandCall(netBehaviourSubclass, md, ca);
            if (cmdCallFunc != null)
            {
                commandCallFuncs.Add(cmdCallFunc);
                Weaver.WeaveLists.replaceMethods[md.FullName] = cmdCallFunc;
            }
        }
    }
}
