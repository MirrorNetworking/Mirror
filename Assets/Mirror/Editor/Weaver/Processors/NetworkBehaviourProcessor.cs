// this class processes SyncVars, Cmds, Rpcs, etc. of NetworkBehaviours
using System;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    class NetworkBehaviourProcessor
    {
        readonly List<FieldDefinition> m_SyncVars = new List<FieldDefinition>();
        readonly List<FieldDefinition> m_SyncObjects = new List<FieldDefinition>();
        readonly Dictionary<FieldDefinition, FieldDefinition> m_SyncVarNetIds = new Dictionary<FieldDefinition, FieldDefinition>(); // <SyncVarField,NetIdField>
        readonly List<MethodDefinition> m_Cmds = new List<MethodDefinition>();
        readonly List<MethodDefinition> m_Rpcs = new List<MethodDefinition>();
        readonly List<MethodDefinition> m_TargetRpcs = new List<MethodDefinition>();
        readonly List<EventDefinition> m_Events = new List<EventDefinition>();
        readonly List<MethodDefinition> m_CmdInvocationFuncs = new List<MethodDefinition>();
        readonly List<MethodDefinition> m_RpcInvocationFuncs = new List<MethodDefinition>();
        readonly List<MethodDefinition> m_TargetRpcInvocationFuncs = new List<MethodDefinition>();
        readonly List<MethodDefinition> m_EventInvocationFuncs = new List<MethodDefinition>();

        readonly List<MethodDefinition> m_CmdCallFuncs = new List<MethodDefinition>();
        readonly List<MethodDefinition> m_RpcCallFuncs = new List<MethodDefinition>();
        readonly List<MethodDefinition> m_TargetRpcCallFuncs = new List<MethodDefinition>();

        readonly TypeDefinition m_td;

        public NetworkBehaviourProcessor(TypeDefinition td)
        {
            Weaver.DLog(td, "NetworkBehaviourProcessor");
            m_td = td;
        }

        public void Process()
        {
            if (m_td.HasGenericParameters)
            {
                Weaver.Error("NetworkBehaviour " + m_td.Name + " cannot have generic parameters");
                return;
            }
            Weaver.DLog(m_td, "Process Start");
            MarkAsProcessed(m_td);
            SyncVarProcessor.ProcessSyncVars(m_td, m_SyncVars, m_SyncObjects, m_SyncVarNetIds);
            Weaver.ResetRecursionCount();

            ProcessMethods();

            SyncEventProcessor.ProcessEvents(m_td, m_Events, m_EventInvocationFuncs);
            if (Weaver.WeavingFailed)
            {
                return;
            }
            GenerateConstants();

            Weaver.ResetRecursionCount();
            GenerateSerialization();
            if (Weaver.WeavingFailed)
            {
                return;
            }

            GenerateDeSerialization();
            Weaver.DLog(m_td, "Process Done");
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
            worker.Append(worker.Create(OpCodes.Newobj, Weaver.NetworkWriterCtor));
            worker.Append(worker.Create(OpCodes.Stloc_0));
        }

        public static bool WriteArguments(ILProcessor worker, MethodDefinition md, string errString, bool skipFirst)
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

                MethodReference writeFunc = Weaver.GetWriteFunc(pd.ParameterType);
                if (writeFunc == null)
                {
                    Weaver.Error("WriteArguments for " + md.Name + " type " + pd.ParameterType + " not supported");
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
            if (m_Cmds.Count == 0 && m_Rpcs.Count == 0 && m_TargetRpcs.Count == 0 && m_Events.Count == 0 && m_SyncObjects.Count == 0)
                return;

            Weaver.DLog(m_td, "  GenerateConstants ");

            // find static constructor
            MethodDefinition cctor = null;
            bool cctorFound = false;
            foreach (MethodDefinition md in m_td.Methods)
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
                        Weaver.Error("No cctor for " + m_td.Name);
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

            foreach (MethodDefinition md in m_td.Methods)
            {
                if (md.Name == ".ctor")
                {
                    ctor = md;

                    var ret = ctor.Body.Instructions[ctor.Body.Instructions.Count - 1];
                    if (ret.OpCode == OpCodes.Ret)
                    {
                        ctor.Body.Instructions.RemoveAt(ctor.Body.Instructions.Count - 1);
                    }
                    else
                    {
                        Weaver.Error("No ctor for " + m_td.Name);
                        return;
                    }

                    break;
                }
            }

            if (ctor == null)
            {
                Weaver.Error("No ctor for " + m_td.Name);
                return;
            }

            ILProcessor ctorWorker = ctor.Body.GetILProcessor();
            ILProcessor cctorWorker = cctor.Body.GetILProcessor();

            for (int i = 0; i < m_Cmds.Count; ++i)
            {
                GenerateRegisterCommandDelegate(cctorWorker, Weaver.registerCommandDelegateReference, m_CmdInvocationFuncs[i], m_Cmds[i].Name);
            }

            for (int i = 0; i < m_Rpcs.Count; ++i)
            {
                GenerateRegisterCommandDelegate(cctorWorker, Weaver.registerRpcDelegateReference, m_RpcInvocationFuncs[i], m_Rpcs[i].Name);
            }

            for (int i = 0; i < m_TargetRpcs.Count; ++i)
            {
                GenerateRegisterCommandDelegate(cctorWorker, Weaver.registerRpcDelegateReference, m_TargetRpcInvocationFuncs[i], m_TargetRpcs[i].Name);
            }

            for (int i = 0; i < m_Events.Count; ++i)
            {
                GenerateRegisterCommandDelegate(cctorWorker, Weaver.registerEventDelegateReference, m_EventInvocationFuncs[i], m_Events[i].Name);
            }

            foreach (FieldDefinition fd in m_SyncObjects)
            {
                SyncListInitializer.GenerateSyncListInstanceInitializer(ctorWorker, fd);
                SyncObjectInitializer.GenerateSyncObjectInitializer(ctorWorker, fd);
            }

            cctorWorker.Append(cctorWorker.Create(OpCodes.Ret));
            if (!cctorFound)
            {
                m_td.Methods.Add(cctor);
            }

            // finish ctor
            ctorWorker.Append(ctorWorker.Create(OpCodes.Ret));

            // in case class had no cctor, it might have BeforeFieldInit, so injected cctor would be called too late
            m_td.Attributes = m_td.Attributes & ~TypeAttributes.BeforeFieldInit;
        }

        /*
            // This generates code like:
            NetworkBehaviour.RegisterCommandDelegate(base.GetType(), "CmdThrust", new NetworkBehaviour.CmdDelegate(ShipControl.InvokeCmdCmdThrust));
        */
        void GenerateRegisterCommandDelegate(ILProcessor awakeWorker, MethodReference registerMethod, MethodDefinition func, string cmdName)
        {
            awakeWorker.Append(awakeWorker.Create(OpCodes.Ldtoken, m_td));
            awakeWorker.Append(awakeWorker.Create(OpCodes.Call, Weaver.getTypeFromHandleReference));
            awakeWorker.Append(awakeWorker.Create(OpCodes.Ldstr, cmdName));
            awakeWorker.Append(awakeWorker.Create(OpCodes.Ldnull));
            awakeWorker.Append(awakeWorker.Create(OpCodes.Ldftn, func));

            awakeWorker.Append(awakeWorker.Create(OpCodes.Newobj, Weaver.CmdDelegateConstructor));
            awakeWorker.Append(awakeWorker.Create(OpCodes.Call, registerMethod));
        }

        void GenerateSerialization()
        {
            Weaver.DLog(m_td, "  GenerateSerialization");

            foreach (MethodDefinition m in m_td.Methods)
            {
                if (m.Name == "OnSerialize")
                    return;
            }

            if (m_SyncVars.Count == 0)
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

            MethodReference baseSerialize = Resolvers.ResolveMethodInParents(m_td.BaseType, Weaver.CurrentAssembly, "OnSerialize");
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

            foreach (FieldDefinition syncVar in m_SyncVars)
            {
                // Generates a writer call for each sync variable
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1)); // writer
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0)); // this
                serWorker.Append(serWorker.Create(OpCodes.Ldfld, syncVar));
                MethodReference writeFunc = Weaver.GetWriteFunc(syncVar.FieldType);
                if (writeFunc != null)
                {
                    serWorker.Append(serWorker.Create(OpCodes.Call, writeFunc));
                }
                else
                {
                    Weaver.Error("GenerateSerialization for " + m_td.Name + " unknown type [" + syncVar.FieldType + "]. Mirror [SyncVar] member variables must be basic types.");
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
            serWorker.Append(serWorker.Create(OpCodes.Callvirt, Weaver.NetworkWriterWritePackedUInt64));

            // generate a writer call for any dirty variable in this class

            // start at number of syncvars in parent
            int dirtyBit = Weaver.GetSyncVarStart(m_td.BaseType.FullName);
            foreach (FieldDefinition syncVar in m_SyncVars)
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

                MethodReference writeFunc = Weaver.GetWriteFunc(syncVar.FieldType);
                if (writeFunc != null)
                {
                    serWorker.Append(serWorker.Create(OpCodes.Call, writeFunc));
                }
                else
                {
                    Weaver.Error("GenerateSerialization for " + m_td.Name + " unknown type [" + syncVar.FieldType + "]. Mirror [SyncVar] member variables must be basic types.");
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
                serWorker.Append(serWorker.Create(OpCodes.Ldstr, "Injected Serialize " + m_td.Name));
                serWorker.Append(serWorker.Create(OpCodes.Call, Weaver.logErrorReference));
            }

            // generate: return dirtyLocal
            serWorker.Append(serWorker.Create(OpCodes.Ldloc_0));
            serWorker.Append(serWorker.Create(OpCodes.Ret));
            m_td.Methods.Add(serialize);
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
            MethodDefinition foundMethod;
            if (!SyncVarProcessor.CheckForHookFunction(m_td, syncVar, out foundMethod))
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
                FieldDefinition netIdField = m_SyncVarNetIds[syncVar];

                VariableDefinition tmpValue = new VariableDefinition(Weaver.uint32Type);
                deserialize.Body.Variables.Add(tmpValue);

                // read id and store in a local variable
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                serWorker.Append(serWorker.Create(OpCodes.Call, Weaver.NetworkReaderReadPackedUInt32));
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
                MethodReference readFunc = Weaver.GetReadFunc(syncVar.FieldType);
                if (readFunc == null)
                {
                    Weaver.Error("GenerateDeSerialization for " + m_td.Name + " unknown type [" + syncVar.FieldType + "]. Mirror [SyncVar] member variables must be basic types.");
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
            Weaver.DLog(m_td, "  GenerateDeSerialization");

            foreach (MethodDefinition m in m_td.Methods)
            {
                if (m.Name == "OnDeserialize")
                    return;
            }

            if (m_SyncVars.Count == 0)
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

            MethodReference baseDeserialize = Resolvers.ResolveMethodInParents(m_td.BaseType, Weaver.CurrentAssembly, "OnDeserialize");
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

            foreach (FieldDefinition syncVar in m_SyncVars)
            {
                DeserializeField(syncVar, serWorker, serialize);
            }

            serWorker.Append(serWorker.Create(OpCodes.Ret));

            // Generates: end if (initialState);
            serWorker.Append(initialStateLabel);


            // get dirty bits
            serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
            serWorker.Append(serWorker.Create(OpCodes.Callvirt, Weaver.NetworkReaderReadPackedUInt64));
            serWorker.Append(serWorker.Create(OpCodes.Stloc_0));

            // conditionally read each syncvar
            int dirtyBit = Weaver.GetSyncVarStart(m_td.BaseType.FullName); // start at number of syncvars in parent
            foreach (FieldDefinition syncVar in m_SyncVars)
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
                serWorker.Append(serWorker.Create(OpCodes.Ldstr, "Injected Deserialize " + m_td.Name));
                serWorker.Append(serWorker.Create(OpCodes.Call, Weaver.logErrorReference));
            }

            serWorker.Append(serWorker.Create(OpCodes.Ret));
            m_td.Methods.Add(serialize);
        }

        public static bool ProcessNetworkReaderParameters(TypeDefinition td, MethodDefinition md, ILProcessor worker, bool skipFirst)
        {
            int count = 0;

            // read cmd args from NetworkReader
            foreach (ParameterDefinition arg in md.Parameters)
            {
                if (count++ == 0 && skipFirst)
                {
                    continue;
                }
                MethodReference readFunc = Weaver.GetReadFunc(arg.ParameterType); //?

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
                    Weaver.Error("ProcessNetworkReaderParameters for " + td.Name + ":" + md.Name + " type " + arg.ParameterType + " not supported");
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

        public static bool ProcessMethodsValidateFunction(TypeDefinition td, MethodReference md, string actionType)
        {
            if (md.ReturnType.FullName == Weaver.IEnumeratorType.FullName)
            {
                Weaver.Error(actionType + " function [" + td.FullName + ":" + md.Name + "] cannot be a coroutine");
                return false;
            }
            if (md.ReturnType.FullName != Weaver.voidType.FullName)
            {
                Weaver.Error(actionType + " function [" + td.FullName + ":" + md.Name + "] must have a void return type.");
                return false;
            }
            if (md.HasGenericParameters)
            {
                Weaver.Error(actionType + " [" + td.FullName + ":" + md.Name + "] cannot have generic parameters");
                return false;
            }
            return true;
        }

        public static bool ProcessMethodsValidateParameters(TypeDefinition td, MethodReference md, CustomAttribute ca, string actionType)
        {
            for (int i = 0; i < md.Parameters.Count; ++i)
            {
                var p = md.Parameters[i];
                if (p.IsOut)
                {
                    Weaver.Error(actionType + " function [" + td.FullName + ":" + md.Name + "] cannot have out parameters");
                    return false;
                }
                if (p.IsOptional)
                {
                    Weaver.Error(actionType + "function [" + td.FullName + ":" + md.Name + "] cannot have optional parameters");
                    return false;
                }
                if (p.ParameterType.Resolve().IsAbstract)
                {
                    Weaver.Error(actionType + " function [" + td.FullName + ":" + md.Name + "] cannot have abstract parameters");
                    return false;
                }
                if (p.ParameterType.IsByReference)
                {
                    Weaver.Error(actionType + " function [" + td.FullName + ":" + md.Name + "] cannot have ref parameters");
                    return false;
                }
                // TargetRPC is an exception to this rule and can have a NetworkConnection as first parameter
                if (p.ParameterType.FullName == Weaver.NetworkConnectionType.FullName &&
                    !(ca.AttributeType.FullName == Weaver.TargetRpcType.FullName && i == 0))
                {
                    Weaver.Error(actionType + " [" + td.FullName + ":" + md.Name + "] cannot use a NetworkConnection as a parameter. To access a player object's connection on the server use connectionToClient");
                    Weaver.Error("Name: " + ca.AttributeType.FullName + " parameter: " + md.Parameters[0].ParameterType.FullName);
                    return false;
                }
                if (p.ParameterType.Resolve().IsDerivedFrom(Weaver.ComponentType))
                {
                    if (p.ParameterType.FullName != Weaver.NetworkIdentityType.FullName)
                    {
                        Weaver.Error(actionType + " function [" + td.FullName + ":" + md.Name + "] parameter [" + p.Name +
                            "] is of the type [" +
                            p.ParameterType.Name +
                            "] which is a Component. You cannot pass a Component to a remote call. Try passing data from within the component.");
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
            foreach (MethodDefinition md in m_td.Methods)
            {
                Weaver.ResetRecursionCount();
                foreach (CustomAttribute ca in md.CustomAttributes)
                {
                    if (ca.AttributeType.FullName == Weaver.CommandType.FullName)
                    {
                        if (!CommandProcessor.ProcessMethodsValidateCommand(m_td, md, ca))
                            return;

                        if (names.Contains(md.Name))
                        {
                            Weaver.Error("Duplicate Command name [" + m_td.FullName + ":" + md.Name + "]");
                            return;
                        }
                        names.Add(md.Name);
                        m_Cmds.Add(md);

                        MethodDefinition cmdFunc = CommandProcessor.ProcessCommandInvoke(m_td, md);
                        if (cmdFunc != null)
                        {
                            m_CmdInvocationFuncs.Add(cmdFunc);
                        }

                        MethodDefinition cmdCallFunc = CommandProcessor.ProcessCommandCall(m_td, md, ca);
                        if (cmdCallFunc != null)
                        {
                            m_CmdCallFuncs.Add(cmdCallFunc);
                            Weaver.WeaveLists.replaceMethods[md.FullName] = cmdCallFunc;
                        }
                        break;
                    }

                    if (ca.AttributeType.FullName == Weaver.TargetRpcType.FullName)
                    {
                        if (!TargetRpcProcessor.ProcessMethodsValidateTargetRpc(m_td, md, ca))
                            return;

                        if (names.Contains(md.Name))
                        {
                            Weaver.Error("Duplicate Target Rpc name [" + m_td.FullName + ":" + md.Name + "]");
                            return;
                        }
                        names.Add(md.Name);
                        m_TargetRpcs.Add(md);

                        MethodDefinition rpcFunc = TargetRpcProcessor.ProcessTargetRpcInvoke(m_td, md);
                        if (rpcFunc != null)
                        {
                            m_TargetRpcInvocationFuncs.Add(rpcFunc);
                        }

                        MethodDefinition rpcCallFunc = TargetRpcProcessor.ProcessTargetRpcCall(m_td, md, ca);
                        if (rpcCallFunc != null)
                        {
                            m_TargetRpcCallFuncs.Add(rpcCallFunc);
                            Weaver.WeaveLists.replaceMethods[md.FullName] = rpcCallFunc;
                        }
                        break;
                    }

                    if (ca.AttributeType.FullName == Weaver.ClientRpcType.FullName)
                    {
                        if (!RpcProcessor.ProcessMethodsValidateRpc(m_td, md, ca))
                            return;

                        if (names.Contains(md.Name))
                        {
                            Weaver.Error("Duplicate ClientRpc name [" + m_td.FullName + ":" + md.Name + "]");
                            return;
                        }
                        names.Add(md.Name);
                        m_Rpcs.Add(md);

                        MethodDefinition rpcFunc = RpcProcessor.ProcessRpcInvoke(m_td, md);
                        if (rpcFunc != null)
                        {
                            m_RpcInvocationFuncs.Add(rpcFunc);
                        }

                        MethodDefinition rpcCallFunc = RpcProcessor.ProcessRpcCall(m_td, md, ca);
                        if (rpcCallFunc != null)
                        {
                            m_RpcCallFuncs.Add(rpcCallFunc);
                            Weaver.WeaveLists.replaceMethods[md.FullName] = rpcCallFunc;
                        }
                        break;
                    }
                }
            }

            // cmds
            foreach (MethodDefinition md in m_CmdInvocationFuncs)
            {
                m_td.Methods.Add(md);
            }
            foreach (MethodDefinition md in m_CmdCallFuncs)
            {
                m_td.Methods.Add(md);
            }

            // rpcs
            foreach (MethodDefinition md in m_RpcInvocationFuncs)
            {
                m_td.Methods.Add(md);
            }
            foreach (MethodDefinition md in m_TargetRpcInvocationFuncs)
            {
                m_td.Methods.Add(md);
            }
            foreach (MethodDefinition md in m_RpcCallFuncs)
            {
                m_td.Methods.Add(md);
            }
            foreach (MethodDefinition md in m_TargetRpcCallFuncs)
            {
                m_td.Methods.Add(md);
            }
        }
    }
}
