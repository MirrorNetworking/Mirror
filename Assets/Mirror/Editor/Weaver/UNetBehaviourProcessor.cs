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
        readonly List<FieldDefinition> m_SyncVarNetIds = new List<FieldDefinition>();
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

        const int k_SyncVarLimit = 64; // ulong = 64 bytes
        readonly TypeDefinition m_td;

        const string k_CmdPrefix = "InvokeCmd";
        const string k_RpcPrefix = "InvokeRpc";
        const string k_TargetRpcPrefix = "InvokeTargetRpc";

        public NetworkBehaviourProcessor(TypeDefinition td)
        {
            Weaver.DLog(td, "NetworkBehaviourProcessor");
            m_td = td;
        }

        public void Process()
        {
            if (m_td.HasGenericParameters)
            {
                Weaver.fail = true;
                Log.Error("NetworkBehaviour " + m_td.Name + " cannot have generic parameters");
                return;
            }
            Weaver.DLog(m_td, "Process Start");
            ProcessVersion();
            ProcessSyncVars();
            Weaver.ResetRecursionCount();

            ProcessMethods();

            ProcessEvents();
            if (Weaver.fail)
            {
                return;
            }
            GenerateNetworkSettings();
            GenerateConstants();

            Weaver.ResetRecursionCount();
            GenerateSerialization();
            if (Weaver.fail)
            {
                return;
            }

            GenerateDeSerialization();
            GeneratePreStartClient();
            Weaver.DLog(m_td, "Process Done");
        }

        static void WriteClientActiveCheck(ILProcessor worker, string mdName, Instruction label, string errString)
        {
            // client active check
            worker.Append(worker.Create(OpCodes.Call, Weaver.NetworkClientGetActive));
            worker.Append(worker.Create(OpCodes.Brtrue, label));

            worker.Append(worker.Create(OpCodes.Ldstr, errString + " " + mdName + " called on server."));
            worker.Append(worker.Create(OpCodes.Call, Weaver.logErrorReference));
            worker.Append(worker.Create(OpCodes.Ret));
            worker.Append(label);
        }

        static void WriteServerActiveCheck(ILProcessor worker, string mdName, Instruction label, string errString)
        {
            // server active check
            worker.Append(worker.Create(OpCodes.Call, Weaver.NetworkServerGetActive));
            worker.Append(worker.Create(OpCodes.Brtrue, label));

            worker.Append(worker.Create(OpCodes.Ldstr, errString + " " + mdName + " called on client."));
            worker.Append(worker.Create(OpCodes.Call, Weaver.logErrorReference));
            worker.Append(worker.Create(OpCodes.Ret));
            worker.Append(label);
        }

        static void WriteSetupLocals(ILProcessor worker)
        {
            worker.Body.InitLocals = true;
            worker.Body.Variables.Add(new VariableDefinition(Weaver.scriptDef.MainModule.ImportReference(Weaver.NetworkWriterType)));
        }

        static void WriteCreateWriter(ILProcessor worker)
        {
            // create writer
            worker.Append(worker.Create(OpCodes.Newobj, Weaver.NetworkWriterCtor));
            worker.Append(worker.Create(OpCodes.Stloc_0));
        }

        static bool WriteArguments(ILProcessor worker, MethodDefinition md, string errString, bool skipFirst)
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
                    Log.Error("WriteArguments for " + md.Name + " type " + pd.ParameterType + " not supported");
                    Weaver.fail = true;
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

        void ProcessVersion()
        {
            foreach (MethodDefinition md in m_td.Methods)
            {
                if (md.Name == "UNetVersion")
                {
                    return;
                }
            }

            MethodDefinition versionMethod = new MethodDefinition("UNetVersion", MethodAttributes.Private, Weaver.voidType);
            ILProcessor worker = versionMethod.Body.GetILProcessor();
            worker.Append(worker.Create(OpCodes.Ret));
            m_td.Methods.Add(versionMethod);
        }

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
                        Log.Error("No cctor for " + m_td.Name);
                        Weaver.fail = true;
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
                        Weaver.fail = true;
                        Log.Error("No ctor for " + m_td.Name);
                        return;
                    }

                    break;
                }
            }

            if (ctor == null)
            {
                Weaver.fail = true;
                Log.Error("No ctor for " + m_td.Name);
                return;
            }

            ILProcessor ctorWorker = ctor.Body.GetILProcessor();
            ILProcessor cctorWorker = cctor.Body.GetILProcessor();

            int cmdIndex = 0;
            foreach (MethodDefinition md in m_Cmds)
            {
                FieldReference cmdConstant = Weaver.ResolveField(m_td, "kCmd" + md.Name);

                int cmdHash = GetHashCode(m_td.Name + ":Cmd:" + md.Name);
                cctorWorker.Append(cctorWorker.Create(OpCodes.Ldc_I4, cmdHash));
                cctorWorker.Append(cctorWorker.Create(OpCodes.Stsfld, cmdConstant));
                //Weaver.DLog(m_td, "    Constant " + m_td.Name + ":Cmd:" + md.Name);

                GenerateCommandDelegate(cctorWorker, Weaver.registerCommandDelegateReference, m_CmdInvocationFuncs[cmdIndex], cmdConstant);
                cmdIndex += 1;
            }

            int rpcIndex = 0;
            foreach (MethodDefinition md in m_Rpcs)
            {
                FieldReference rpcConstant = Weaver.ResolveField(m_td, "kRpc" + md.Name);

                int rpcHash = GetHashCode(m_td.Name + ":Rpc:" + md.Name);
                cctorWorker.Append(cctorWorker.Create(OpCodes.Ldc_I4, rpcHash));
                cctorWorker.Append(cctorWorker.Create(OpCodes.Stsfld, rpcConstant));
                //Weaver.DLog(m_td, "    Constant " + m_td.Name + ":Rpc:" + md.Name);

                GenerateCommandDelegate(cctorWorker, Weaver.registerRpcDelegateReference, m_RpcInvocationFuncs[rpcIndex], rpcConstant);
                rpcIndex += 1;
            }

            int targetRpcIndex = 0;
            foreach (MethodDefinition md in m_TargetRpcs)
            {
                FieldReference targetRpcConstant = Weaver.ResolveField(m_td, "kTargetRpc" + md.Name);

                int targetRpcHash = GetHashCode(m_td.Name + ":TargetRpc:" + md.Name);
                cctorWorker.Append(cctorWorker.Create(OpCodes.Ldc_I4, targetRpcHash));
                cctorWorker.Append(cctorWorker.Create(OpCodes.Stsfld, targetRpcConstant));
                //Weaver.DLog(m_td, "    Constant " + m_td.Name + ":Rpc:" + md.Name);

                GenerateCommandDelegate(cctorWorker, Weaver.registerRpcDelegateReference, m_TargetRpcInvocationFuncs[targetRpcIndex], targetRpcConstant);
                targetRpcIndex += 1;
            }

            int eventIndex = 0;
            foreach (EventDefinition ed in m_Events)
            {
                FieldReference eventConstant = Weaver.ResolveField(m_td, "kEvent" + ed.Name);

                int eventHash = GetHashCode(m_td.Name + ":Event:" + ed.Name);
                cctorWorker.Append(cctorWorker.Create(OpCodes.Ldc_I4, eventHash));
                cctorWorker.Append(cctorWorker.Create(OpCodes.Stsfld, eventConstant));
                //Weaver.DLog(m_td, "    Constant " + m_td.Name + ":Event:" + ed.Name);

                GenerateCommandDelegate(cctorWorker, Weaver.registerEventDelegateReference, m_EventInvocationFuncs[eventIndex], eventConstant);
                eventIndex += 1;
            }

            foreach (FieldDefinition fd in m_SyncObjects)
            {
                GenerateSyncListInstanceInitializer(ctorWorker, fd);
                GenerateSyncObjectInitializer(ctorWorker, fd);
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

        void GenerateSyncListInstanceInitializer(ILProcessor ctorWorker, FieldDefinition fd)
        {
            // check the ctor's instructions for an Stfld op-code for this specific sync list field.
            foreach (var ins in ctorWorker.Body.Instructions)
            {
                if (ins.OpCode.Code == Code.Stfld)
                {
                    var field = (FieldDefinition)ins.Operand;
                    if (field.DeclaringType == fd.DeclaringType && field.Name == fd.Name)
                    {
                        // Already initialized by the user in the field definition, e.g:
                        // public SyncListInt Foo = new SyncListInt();
                        return;
                    }
                }
            }

            // Not initialized by the user in the field definition, e.g:
            // public SyncListInt Foo;
            var listCtor = Weaver.scriptDef.MainModule.ImportReference(fd.FieldType.Resolve().Methods.First<MethodDefinition>(x => x.Name == ".ctor" && !x.HasParameters));

            ctorWorker.Append(ctorWorker.Create(OpCodes.Ldarg_0));
            ctorWorker.Append(ctorWorker.Create(OpCodes.Newobj, listCtor));
            ctorWorker.Append(ctorWorker.Create(OpCodes.Stfld, fd));
        }

        /*
            // This generates code like:
            NetworkBehaviour.RegisterCommandDelegate(base.GetType(), ShipControl.kCmdCmdThrust, new NetworkBehaviour.CmdDelegate(ShipControl.InvokeCmdCmdThrust));
        */
        void GenerateCommandDelegate(ILProcessor awakeWorker, MethodReference registerMethod, MethodDefinition func, FieldReference field)
        {
            awakeWorker.Append(awakeWorker.Create(OpCodes.Ldtoken, m_td));
            awakeWorker.Append(awakeWorker.Create(OpCodes.Call, Weaver.getTypeFromHandleReference));
            awakeWorker.Append(awakeWorker.Create(OpCodes.Ldsfld, field));
            awakeWorker.Append(awakeWorker.Create(OpCodes.Ldnull));
            awakeWorker.Append(awakeWorker.Create(OpCodes.Ldftn, func));

            awakeWorker.Append(awakeWorker.Create(OpCodes.Newobj, Weaver.CmdDelegateConstructor));
            awakeWorker.Append(awakeWorker.Create(OpCodes.Call, registerMethod));
        }

        /*
            // generates code like:
            this.InitSyncObject(m_sizes);
        */
        void GenerateSyncObjectInitializer(ILProcessor methodWorker, FieldReference fd)
        {
            methodWorker.Append(methodWorker.Create(OpCodes.Ldarg_0));
            methodWorker.Append(methodWorker.Create(OpCodes.Ldarg_0));
            methodWorker.Append(methodWorker.Create(OpCodes.Ldfld, fd));

            methodWorker.Append(methodWorker.Create(OpCodes.Call, Weaver.InitSyncObjectReference));
        }

        void GenerateSerialization()
        {
            Weaver.DLog(m_td, "  GenerateSerialization");

            foreach (var m in m_td.Methods)
            {
                if (m.Name == "OnSerialize")
                    return;
            }

            MethodDefinition serialize = new MethodDefinition("OnSerialize", MethodAttributes.Public |
                    MethodAttributes.Virtual |
                    MethodAttributes.HideBySig,
                    Weaver.boolType);

            serialize.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, Weaver.scriptDef.MainModule.ImportReference(Weaver.NetworkWriterType)));
            serialize.Parameters.Add(new ParameterDefinition("forceAll", ParameterAttributes.None, Weaver.boolType));
            ILProcessor serWorker = serialize.Body.GetILProcessor();

            serialize.Body.InitLocals = true;

            // loc_0,  this local variable is to determine if any variable was dirty
            VariableDefinition dirtyLocal = new VariableDefinition(Weaver.boolType);
            serialize.Body.Variables.Add(dirtyLocal);

            MethodReference baseSerialize = Weaver.ResolveMethod(m_td.BaseType, "OnSerialize");
            if (baseSerialize != null)
            {
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0)); // base
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1)); // writer
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_2)); // forceAll
                serWorker.Append(serWorker.Create(OpCodes.Call, baseSerialize));
                serWorker.Append(serWorker.Create(OpCodes.Stloc_0)); // set dirtyLocal to result of base.OnSerialize()
            }

            if (m_SyncVars.Count == 0)
            {
                // generate: return dirtyLocal
                serWorker.Append(serWorker.Create(OpCodes.Ldloc_0));
                serWorker.Append(serWorker.Create(OpCodes.Ret));
                m_td.Methods.Add(serialize);
                return;
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
                    Weaver.fail = true;
                    Log.Error("GenerateSerialization for " + m_td.Name + " unknown type [" + syncVar.FieldType + "]. UNet [SyncVar] member variables must be basic types.");
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
            serWorker.Append(serWorker.Create(OpCodes.Callvirt, Weaver.NetworkWriterWritePacked64));

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
                    Log.Error("GenerateSerialization for " + m_td.Name + " unknown type [" + syncVar.FieldType + "]. UNet [SyncVar] member variables must be basic types.");
                    Weaver.fail = true;
                    return;
                }

                // something was dirty
                serWorker.Append(serWorker.Create(OpCodes.Ldc_I4_1));
                serWorker.Append(serWorker.Create(OpCodes.Stloc_0)); // set dirtyLocal to true

                serWorker.Append(varLabel);
                dirtyBit += 1;
            }

            if (Weaver.generateLogErrors)
            {
                serWorker.Append(serWorker.Create(OpCodes.Ldstr, "Injected Serialize " + m_td.Name));
                serWorker.Append(serWorker.Create(OpCodes.Call, Weaver.logErrorReference));
            }

            // generate: return dirtyLocal
            serWorker.Append(serWorker.Create(OpCodes.Ldloc_0));
            serWorker.Append(serWorker.Create(OpCodes.Ret));
            m_td.Methods.Add(serialize);
        }

        static int GetChannelId(CustomAttribute ca)
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

        // returns false for error, not for no-hook-exists
        bool CheckForHookFunction(FieldDefinition syncVar, out MethodDefinition foundMethod)
        {
            foundMethod = null;
            foreach (var ca in syncVar.CustomAttributes)
            {
                if (ca.AttributeType.FullName == Weaver.SyncVarType.FullName)
                {
                    foreach (CustomAttributeNamedArgument customField in ca.Fields)
                    {
                        if (customField.Name == "hook")
                        {
                            string hookFunctionName = customField.Argument.Value as string;

                            foreach (var m in m_td.Methods)
                            {
                                if (m.Name == hookFunctionName)
                                {
                                    if (m.Parameters.Count == 1)
                                    {
                                        if (m.Parameters[0].ParameterType != syncVar.FieldType)
                                        {
                                            Log.Error("SyncVar Hook function " + hookFunctionName + " has wrong type signature for " + m_td.Name);
                                            Weaver.fail = true;
                                            return false;
                                        }
                                        foundMethod = m;
                                        return true;
                                    }
                                    Log.Error("SyncVar Hook function " + hookFunctionName + " must have one argument " + m_td.Name);
                                    Weaver.fail = true;
                                    return false;
                                }
                            }
                            Log.Error("SyncVar Hook function " + hookFunctionName + " not found for " + m_td.Name);
                            Weaver.fail = true;
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        void GenerateNetworkIntervalSetting(float interval)
        {
            MethodDefinition meth = new MethodDefinition("GetNetworkSendInterval", MethodAttributes.Public |
                    MethodAttributes.Virtual |
                    MethodAttributes.HideBySig,
                    Weaver.singleType);

            ILProcessor worker = meth.Body.GetILProcessor();

            worker.Append(worker.Create(OpCodes.Ldc_R4, interval));
            worker.Append(worker.Create(OpCodes.Ret));
            m_td.Methods.Add(meth);
        }

        void GenerateNetworkSettings()
        {
            // look for custom attribute
            foreach (var ca in m_td.CustomAttributes)
            {
                if (ca.AttributeType.FullName == Weaver.NetworkSettingsType.FullName)
                {
                    // generate virtual functions
                    foreach (var field in ca.Fields)
                    {
                        if (field.Name == "sendInterval")
                        {
                            const float stdValue = 0.1f;
                            const float epsilon = 0.00001f;

                            if ((Math.Abs((float)field.Argument.Value - stdValue) <= epsilon))
                                continue;

                            if (HasMethod("GetNetworkSendInterval"))
                            {
                                Log.Error(
                                    "GetNetworkSendInterval, is already implemented, please make sure you either use NetworkSettings or GetNetworkSendInterval");
                                Weaver.fail = true;
                                return;
                            }
                            GenerateNetworkIntervalSetting((float)field.Argument.Value);
                        }
                    }
                }
            }
        }

        void GeneratePreStartClient()
        {
            int netIdFieldCounter  = 0;
            MethodDefinition preStartMethod = null;
            ILProcessor serWorker = null;

            foreach (var m in m_td.Methods)
            {
                if (m.Name == "PreStartClient")
                    return;
            }

            foreach (FieldDefinition syncVar in m_SyncVars)
            {
                if (syncVar.FieldType.FullName == Weaver.gameObjectType.FullName)
                {
                    if (preStartMethod == null)
                    {
                        preStartMethod = new MethodDefinition("PreStartClient", MethodAttributes.Public |
                                MethodAttributes.Virtual |
                                MethodAttributes.HideBySig,
                                Weaver.voidType);

                        serWorker = preStartMethod.Body.GetILProcessor();
                    }

                    FieldDefinition netIdField = m_SyncVarNetIds[netIdFieldCounter];
                    netIdFieldCounter += 1;

                    // Generates: if (!_crateNetId.IsEmpty()) { crate = ClientScene.FindLocalObject(_crateNetId); }
                    Instruction nullLabel = serWorker.Create(OpCodes.Nop);
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    serWorker.Append(serWorker.Create(OpCodes.Ldfld, netIdField));
                    serWorker.Append(serWorker.Create(OpCodes.Ldc_I4_0));
                    serWorker.Append(serWorker.Create(OpCodes.Ceq));
                    serWorker.Append(serWorker.Create(OpCodes.Brtrue, nullLabel));

                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    serWorker.Append(serWorker.Create(OpCodes.Ldfld, netIdField));
                    serWorker.Append(serWorker.Create(OpCodes.Call, Weaver.FindLocalObjectReference));

                    // return value of FindLocalObjectReference is on stack, assign it to the syncvar
                    serWorker.Append(serWorker.Create(OpCodes.Stfld, syncVar));

                    // Generates: end crateNetId != 0
                    serWorker.Append(nullLabel);
                }
            }
            if (preStartMethod != null)
            {
                serWorker.Append(serWorker.Create(OpCodes.Ret));
                m_td.Methods.Add(preStartMethod);
            }
        }

        void GenerateDeSerialization()
        {
            Weaver.DLog(m_td, "  GenerateDeSerialization");
            int netIdFieldCounter  = 0;

            foreach (var m in m_td.Methods)
            {
                if (m.Name == "OnDeserialize")
                    return;
            }

            MethodDefinition serialize = new MethodDefinition("OnDeserialize", MethodAttributes.Public |
                    MethodAttributes.Virtual |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);

            serialize.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.scriptDef.MainModule.ImportReference(Weaver.NetworkReaderType)));
            serialize.Parameters.Add(new ParameterDefinition("initialState", ParameterAttributes.None, Weaver.boolType));
            ILProcessor serWorker = serialize.Body.GetILProcessor();

            MethodReference baseDeserialize = Weaver.ResolveMethod(m_td.BaseType, "OnDeserialize");
            if (baseDeserialize != null)
            {
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0)); // base
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1)); // reader
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_2)); // initialState
                serWorker.Append(serWorker.Create(OpCodes.Call, baseDeserialize));
            }

            if (m_SyncVars.Count == 0)
            {
                serWorker.Append(serWorker.Create(OpCodes.Ret));
                m_td.Methods.Add(serialize);
                return;
            }

            // Generates: if (initialState);
            Instruction initialStateLabel = serWorker.Create(OpCodes.Nop);

            serWorker.Append(serWorker.Create(OpCodes.Ldarg_2));
            serWorker.Append(serWorker.Create(OpCodes.Brfalse, initialStateLabel));

            foreach (var syncVar in m_SyncVars)
            {
                // assign value
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));

                if (syncVar.FieldType.FullName == Weaver.gameObjectType.FullName)
                {
                    // GameObject SyncVar - assign to generated netId var
                    FieldDefinition netIdField = m_SyncVarNetIds[netIdFieldCounter];
                    netIdFieldCounter += 1;

                    serWorker.Append(serWorker.Create(OpCodes.Callvirt, Weaver.NetworkReaderReadPacked32));
                    serWorker.Append(serWorker.Create(OpCodes.Stfld, netIdField));
                }
                else
                {
                    MethodReference readFunc = Weaver.GetReadFunc(syncVar.FieldType);
                    if (readFunc != null)
                    {
                        serWorker.Append(serWorker.Create(OpCodes.Call, readFunc));
                    }
                    else
                    {
                        Log.Error("GenerateDeSerialization for " + m_td.Name + " unknown type [" + syncVar.FieldType + "]. UNet [SyncVar] member variables must be basic types.");
                        Weaver.fail = true;
                        return;
                    }
                    serWorker.Append(serWorker.Create(OpCodes.Stfld, syncVar));
                }
            }

            serWorker.Append(serWorker.Create(OpCodes.Ret));

            // Generates: end if (initialState);
            serWorker.Append(initialStateLabel);

            // setup local for dirty bits
            serialize.Body.InitLocals = true;
            VariableDefinition dirtyBitsLocal = new VariableDefinition(Weaver.int64Type);
            serialize.Body.Variables.Add(dirtyBitsLocal);

            // get dirty bits
            serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
            serWorker.Append(serWorker.Create(OpCodes.Callvirt, Weaver.NetworkReaderReadPacked64));
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

                MethodReference readFunc = Weaver.GetReadFunc(syncVar.FieldType);
                if (readFunc == null)
                {
                    Log.Error("GenerateDeSerialization for " + m_td.Name + " unknown type [" + syncVar.FieldType + "]. UNet [SyncVar] member variables must be basic types.");
                    Weaver.fail = true;
                    return;
                }

                // check for Hook function
                MethodDefinition foundMethod;
                if (!CheckForHookFunction(syncVar, out foundMethod))
                {
                    return;
                }

                if (foundMethod == null)
                {
                    // just assign value
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                    serWorker.Append(serWorker.Create(OpCodes.Call, readFunc));
                    serWorker.Append(serWorker.Create(OpCodes.Stfld, syncVar));
                }
                else
                {
                    // call hook instead
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                    serWorker.Append(serWorker.Create(OpCodes.Call, readFunc));
                    serWorker.Append(serWorker.Create(OpCodes.Call, foundMethod));
                }
                serWorker.Append(varLabel);
                dirtyBit += 1;
            }

            if (Weaver.generateLogErrors)
            {
                serWorker.Append(serWorker.Create(OpCodes.Ldstr, "Injected Deserialize " + m_td.Name));
                serWorker.Append(serWorker.Create(OpCodes.Call, Weaver.logErrorReference));
            }

            serWorker.Append(serWorker.Create(OpCodes.Ret));
            m_td.Methods.Add(serialize);
        }

        bool ProcessNetworkReaderParameters(MethodDefinition md, ILProcessor worker, bool skipFirst)
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
                    Log.Error("ProcessNetworkReaderParameters for " + m_td.Name + ":" + md.Name + " type " + arg.ParameterType + " not supported");
                    Weaver.fail = true;
                    return false;
                }
            }
            return true;
        }

        /*
            // generates code like:
            protected static void InvokeCmdCmdThrust(NetworkBehaviour obj, NetworkReader reader)
            {
                if (!NetworkServer.active)
                {
                    return;
                }
                ((ShipControl)obj).CmdThrust(reader.ReadSingle(), (int)reader.ReadPackedUInt32());
            }
        */
        MethodDefinition ProcessCommandInvoke(MethodDefinition md)
        {
            MethodDefinition cmd = new MethodDefinition(k_CmdPrefix + md.Name, MethodAttributes.Family |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);

            ILProcessor cmdWorker = cmd.Body.GetILProcessor();
            Instruction label = cmdWorker.Create(OpCodes.Nop);

            WriteServerActiveCheck(cmdWorker, md.Name, label, "Command");

            // setup for reader
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldarg_0));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Castclass, m_td));

            if (!ProcessNetworkReaderParameters(md, cmdWorker, false))
                return null;

            // invoke actual command function
            cmdWorker.Append(cmdWorker.Create(OpCodes.Callvirt, md));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ret));

            AddInvokeParameters(cmd.Parameters);

            return cmd;
        }

        static void AddInvokeParameters(ICollection<ParameterDefinition> collection)
        {
            collection.Add(new ParameterDefinition("obj", ParameterAttributes.None, Weaver.NetworkBehaviourType2));
            collection.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.scriptDef.MainModule.ImportReference(Weaver.NetworkReaderType)));
        }

        /*
            // generates code like:
            public void CallCmdThrust(float thrusting, int spin)
            {
                Debug.LogError("Call Command function CmdThrust");
                if (!NetworkClient.active)
                {
                    Debug.LogError("Command function CmdThrust called on server.");
                    return;
                }

                if (isServer)
                {
                    // we are ON the server, invoke directly
                    CmdThrust(thrusting, spin);
                    return;
                }

                NetworkWriter networkWriter = new NetworkWriter();
                networkWriter.Write(thrusting);
                networkWriter.WritePackedUInt32((uint)spin);
                base.SendCommandInternal(ShipControl.kCmdCmdThrust, networkWriter, cmdName);
            }
        */
        MethodDefinition ProcessCommandCall(MethodDefinition md, CustomAttribute ca)
        {
            MethodDefinition cmd = new MethodDefinition("Call" +  md.Name, MethodAttributes.Public |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);

            // add paramters
            foreach (ParameterDefinition pd in md.Parameters)
            {
                cmd.Parameters.Add(new ParameterDefinition(pd.Name, ParameterAttributes.None, pd.ParameterType));
            }

            ILProcessor cmdWorker = cmd.Body.GetILProcessor();
            Instruction label = cmdWorker.Create(OpCodes.Nop);

            WriteSetupLocals(cmdWorker);

            if (Weaver.generateLogErrors)
            {
                cmdWorker.Append(cmdWorker.Create(OpCodes.Ldstr, "Call Command function " + md.Name));
                cmdWorker.Append(cmdWorker.Create(OpCodes.Call, Weaver.logErrorReference));
            }

            WriteClientActiveCheck(cmdWorker, md.Name, label, "Command function");

            // local client check
            Instruction localClientLabel = cmdWorker.Create(OpCodes.Nop);
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldarg_0));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Call, Weaver.UBehaviourIsServer));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Brfalse, localClientLabel));

            // call the cmd function directly.
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldarg_0));
            for (int i = 0; i < md.Parameters.Count; i++)
            {
                cmdWorker.Append(cmdWorker.Create(OpCodes.Ldarg, i + 1));
            }
            cmdWorker.Append(cmdWorker.Create(OpCodes.Call, md));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ret));
            cmdWorker.Append(localClientLabel);

            // NetworkWriter writer = new NetworkWriter();
            WriteCreateWriter(cmdWorker);

            // create the command id constant
            FieldDefinition cmdConstant = new FieldDefinition("kCmd" + md.Name,
                    FieldAttributes.Static | FieldAttributes.Private,
                    Weaver.int32Type);
            m_td.Fields.Add(cmdConstant);

            // write all the arguments that the user passed to the Cmd call
            if (!WriteArguments(cmdWorker, md, "Command", false))
                return null;

            var cmdName = md.Name;
            int index = cmdName.IndexOf(k_CmdPrefix);
            if (index > -1)
            {
                cmdName = cmdName.Substring(k_CmdPrefix.Length);
            }

            // invoke interal send and return
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldarg_0)); // load 'base.' to call the SendCommand function with
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldsfld, cmdConstant)); // cmdHash
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldloc_0)); // writer
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldc_I4, GetChannelId(ca)));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldstr, cmdName));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Call, Weaver.sendCommandInternal));

            cmdWorker.Append(cmdWorker.Create(OpCodes.Ret));

            return cmd;
        }

        MethodDefinition ProcessTargetRpcInvoke(MethodDefinition md)
        {
            MethodDefinition rpc = new MethodDefinition(k_RpcPrefix + md.Name, MethodAttributes.Family |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);

            ILProcessor rpcWorker = rpc.Body.GetILProcessor();
            Instruction label = rpcWorker.Create(OpCodes.Nop);

            WriteClientActiveCheck(rpcWorker, md.Name, label, "TargetRPC");

            // setup for reader
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldarg_0));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Castclass, m_td));

            //ClientScene.readyconnection
            rpcWorker.Append(rpcWorker.Create(OpCodes.Call, Weaver.ReadyConnectionReference));

            if (!ProcessNetworkReaderParameters(md, rpcWorker, true))
                return null;

            // invoke actual command function
            rpcWorker.Append(rpcWorker.Create(OpCodes.Callvirt, md));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ret));

            AddInvokeParameters(rpc.Parameters);

            return rpc;
        }

        MethodDefinition ProcessRpcInvoke(MethodDefinition md)
        {
            MethodDefinition rpc = new MethodDefinition(k_RpcPrefix + md.Name, MethodAttributes.Family |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);

            ILProcessor rpcWorker = rpc.Body.GetILProcessor();
            Instruction label = rpcWorker.Create(OpCodes.Nop);

            WriteClientActiveCheck(rpcWorker, md.Name, label, "RPC");

            // setup for reader
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldarg_0));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Castclass, m_td));

            if (!ProcessNetworkReaderParameters(md, rpcWorker, false))
                return null;

            // invoke actual command function
            rpcWorker.Append(rpcWorker.Create(OpCodes.Callvirt, md));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ret));

            AddInvokeParameters(rpc.Parameters);

            return rpc;
        }

        /* generates code like:
        public void CallTargetTest (NetworkConnection conn, int param)
        {
            if (!NetworkServer.get_active ()) {
                Debug.LogError((object)"TargetRPC Function TargetTest called on client.");
            } else if (((?)conn) is ULocalConnectionToServer) {
                Debug.LogError((object)"TargetRPC Function TargetTest called on connection to server");
            } else {
                NetworkWriter writer = new NetworkWriter ();
                writer.WritePackedUInt32 ((uint)param);
                base.SendTargetRPCInternal (conn, Player.kTargetRpcTargetTest, val, "TargetTest");
            }
        }
        */
        MethodDefinition ProcessTargetRpcCall(MethodDefinition md, CustomAttribute ca)
        {
            MethodDefinition rpc = new MethodDefinition("Call" +  md.Name, MethodAttributes.Public |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);

            // add paramters
            foreach (ParameterDefinition pd in md.Parameters)
            {
                rpc.Parameters.Add(new ParameterDefinition(pd.Name, ParameterAttributes.None, pd.ParameterType));
            }

            ILProcessor rpcWorker = rpc.Body.GetILProcessor();
            Instruction label = rpcWorker.Create(OpCodes.Nop);

            WriteSetupLocals(rpcWorker);

            WriteServerActiveCheck(rpcWorker, md.Name, label, "TargetRPC Function");

            Instruction labelConnectionCheck = rpcWorker.Create(OpCodes.Nop);

            // check specifically for ULocalConnectionToServer so a host is not trying to send
            // an TargetRPC to the "server" from it's local client.
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldarg_1));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Isinst, Weaver.ULocalConnectionToServerType));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Brfalse, labelConnectionCheck));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldstr, string.Format("TargetRPC Function {0} called on connection to server", md.Name)));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Call, Weaver.logErrorReference));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ret));
            rpcWorker.Append(labelConnectionCheck);

            WriteCreateWriter(rpcWorker);

            // create the targetrpc id constant
            FieldDefinition rpcConstant = new FieldDefinition("kTargetRpc" + md.Name,
                    FieldAttributes.Static | FieldAttributes.Private,
                    Weaver.int32Type);
            m_td.Fields.Add(rpcConstant);

            // write all the arguments that the user passed to the TargetRpc call
            if (!WriteArguments(rpcWorker, md, "TargetRPC", true))
                return null;

            var rpcName = md.Name;
            int index = rpcName.IndexOf(k_TargetRpcPrefix);
            if (index > -1)
            {
                rpcName = rpcName.Substring(k_TargetRpcPrefix.Length);
            }

            // invoke SendInternal and return
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldarg_0)); // this
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldarg_1)); // connection
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldsfld, rpcConstant)); // rpcHash
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldloc_0)); // writer
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldc_I4, GetChannelId(ca)));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldstr, rpcName));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Callvirt, Weaver.sendTargetRpcInternal));

            rpcWorker.Append(rpcWorker.Create(OpCodes.Ret));

            return rpc;
        }

        /* generates code like:
        public void CallRpcTest (int param)
        {
            if (!NetworkServer.get_active ()) {
                Debug.LogError ((object)"RPC Function RpcTest called on client.");
            } else {
                NetworkWriter writer = new NetworkWriter ();
                writer.WritePackedUInt32((uint)param);
                base.SendRPCInternal(Player.kRpcRpcTest, writer, 0, "RpcTest");
            }
        }
        */
        MethodDefinition ProcessRpcCall(MethodDefinition md, CustomAttribute ca)
        {
            MethodDefinition rpc = new MethodDefinition("Call" +  md.Name, MethodAttributes.Public |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);

            // add paramters
            foreach (ParameterDefinition pd in md.Parameters)
            {
                rpc.Parameters.Add(new ParameterDefinition(pd.Name, ParameterAttributes.None, pd.ParameterType));
            }

            ILProcessor rpcWorker = rpc.Body.GetILProcessor();
            Instruction label = rpcWorker.Create(OpCodes.Nop);

            WriteSetupLocals(rpcWorker);

            WriteServerActiveCheck(rpcWorker, md.Name, label, "RPC Function");

            WriteCreateWriter(rpcWorker);

            // create the rpc id constant
            FieldDefinition rpcConstant = new FieldDefinition("kRpc" + md.Name,
                    FieldAttributes.Static | FieldAttributes.Private,
                    Weaver.int32Type);
            m_td.Fields.Add(rpcConstant);

            // write all the arguments that the user passed to the Rpc call
            if (!WriteArguments(rpcWorker, md, "RPC", false))
                return null;

            var rpcName = md.Name;
            int index = rpcName.IndexOf(k_RpcPrefix);
            if (index > -1)
            {
                rpcName = rpcName.Substring(k_RpcPrefix.Length);
            }

            // invoke SendInternal and return
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldarg_0)); // this
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldsfld, rpcConstant)); // rpcHash
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldloc_0)); // writer
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldc_I4, GetChannelId(ca)));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldstr, rpcName));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Callvirt, Weaver.sendRpcInternal));

            rpcWorker.Append(rpcWorker.Create(OpCodes.Ret));

            return rpc;
        }

        bool ProcessMethodsValidateFunction(MethodReference md, string actionType)
        {
            if (md.ReturnType.FullName == Weaver.IEnumeratorType.FullName)
            {
                Log.Error(actionType + " function [" + m_td.FullName + ":" + md.Name + "] cannot be a coroutine");
                Weaver.fail = true;
                return false;
            }
            if (md.ReturnType.FullName != Weaver.voidType.FullName)
            {
                Log.Error(actionType + " function [" + m_td.FullName + ":" + md.Name + "] must have a void return type.");
                Weaver.fail = true;
                return false;
            }
            if (md.HasGenericParameters)
            {
                Log.Error(actionType + " [" + m_td.FullName + ":" + md.Name + "] cannot have generic parameters");
                Weaver.fail = true;
                return false;
            }
            return true;
        }

        bool ProcessMethodsValidateParameters(MethodReference md, CustomAttribute ca, string actionType)
        {
            for (int i = 0; i < md.Parameters.Count; ++i)
            {
                var p = md.Parameters[i];
                if (p.IsOut)
                {
                    Log.Error(actionType + " function [" + m_td.FullName + ":" + md.Name + "] cannot have out parameters");
                    Weaver.fail = true;
                    return false;
                }
                if (p.IsOptional)
                {
                    Log.Error(actionType + "function [" + m_td.FullName + ":" + md.Name + "] cannot have optional parameters");
                    Weaver.fail = true;
                    return false;
                }
                if (p.ParameterType.Resolve().IsAbstract)
                {
                    Log.Error(actionType + " function [" + m_td.FullName + ":" + md.Name + "] cannot have abstract parameters");
                    Weaver.fail = true;
                    return false;
                }
                if (p.ParameterType.IsByReference)
                {
                    Log.Error(actionType + " function [" + m_td.FullName + ":" + md.Name + "] cannot have ref parameters");
                    Weaver.fail = true;
                    return false;
                }
                // TargetRPC is an exception to this rule and can have a NetworkConnection as first parameter
                if (p.ParameterType.FullName == Weaver.NetworkConnectionType.FullName &&
                    !(ca.AttributeType.FullName == Weaver.TargetRpcType.FullName && i == 0))
                {
                    Log.Error(actionType + " [" + m_td.FullName + ":" + md.Name + "] cannot use a NetworkConnection as a parameter. To access a player object's connection on the server use connectionToClient");
                    Log.Error("Name: " + ca.AttributeType.FullName + " parameter: " + md.Parameters[0].ParameterType.FullName);
                    Weaver.fail = true;
                    return false;
                }
                if (Weaver.IsDerivedFrom(p.ParameterType.Resolve(), Weaver.ComponentType))
                {
                    if (p.ParameterType.FullName != Weaver.NetworkIdentityType.FullName)
                    {
                        Log.Error(actionType + " function [" + m_td.FullName + ":" + md.Name + "] parameter [" + p.Name +
                            "] is of the type [" +
                            p.ParameterType.Name +
                            "] which is a Component. You cannot pass a Component to a remote call. Try passing data from within the component.");
                        Weaver.fail = true;
                        return false;
                    }
                }
            }
            return true;
        }

        private bool ProcessMethodsValidateCommand(MethodDefinition md, CustomAttribute ca)
        {
            if (md.IsStatic)
            {
                Log.Error("Command function [" + m_td.FullName + ":" + md.Name + "] cant be a static method");
                Weaver.fail = true;
                return false;
            }

            if (!ProcessMethodsValidateFunction(md, "Command"))
            {
                return false;
            }

            if (!ProcessMethodsValidateParameters(md, ca, "Command"))
            {
                return false;
            }
            return true;
        }

        bool ProcessMethodsValidateTargetRpc(MethodDefinition md, CustomAttribute ca)
        {
            if (md.IsStatic)
            {
                Log.Error("TargetRpc function [" + m_td.FullName + ":" + md.Name + "] cant be a static method");
                Weaver.fail = true;
                return false;
            }

            if (!ProcessMethodsValidateFunction(md, "Target Rpc"))
            {
                return false;
            }

            if (md.Parameters.Count < 1)
            {
                Log.Error("Target Rpc function [" + m_td.FullName + ":" + md.Name + "] must have a NetworkConnection as the first parameter");
                Weaver.fail = true;
                return false;
            }

            if (md.Parameters[0].ParameterType.FullName != Weaver.NetworkConnectionType.FullName)
            {
                Log.Error("Target Rpc function [" + m_td.FullName + ":" + md.Name + "] first parameter must be a NetworkConnection");
                Weaver.fail = true;
                return false;
            }

            if (!ProcessMethodsValidateParameters(md, ca, "Target Rpc"))
            {
                return false;
            }
            return true;
        }

        bool ProcessMethodsValidateRpc(MethodDefinition md, CustomAttribute ca)
        {
            if (md.IsStatic)
            {
                Log.Error("ClientRpc function [" + m_td.FullName + ":" + md.Name + "] cant be a static method");
                Weaver.fail = true;
                return false;
            }

            if (!ProcessMethodsValidateFunction(md, "Rpc"))
            {
                return false;
            }

            if (!ProcessMethodsValidateParameters(md, ca, "Rpc"))
            {
                return false;
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
                foreach (var ca in md.CustomAttributes)
                {
                    if (ca.AttributeType.FullName == Weaver.CommandType.FullName)
                    {
                        if (!ProcessMethodsValidateCommand(md, ca))
                            return;

                        if (names.Contains(md.Name))
                        {
                            Log.Error("Duplicate Command name [" + m_td.FullName + ":" + md.Name + "]");
                            Weaver.fail = true;
                            return;
                        }
                        names.Add(md.Name);
                        m_Cmds.Add(md);

                        MethodDefinition cmdFunc = ProcessCommandInvoke(md);
                        if (cmdFunc != null)
                        {
                            m_CmdInvocationFuncs.Add(cmdFunc);
                        }

                        MethodDefinition cmdCallFunc = ProcessCommandCall(md, ca);
                        if (cmdCallFunc != null)
                        {
                            m_CmdCallFuncs.Add(cmdCallFunc);
                            Weaver.lists.replacedMethods.Add(md);
                            Weaver.lists.replacementMethods.Add(cmdCallFunc);
                        }
                        break;
                    }

                    if (ca.AttributeType.FullName == Weaver.TargetRpcType.FullName)
                    {
                        if (!ProcessMethodsValidateTargetRpc(md, ca))
                            return;

                        if (names.Contains(md.Name))
                        {
                            Log.Error("Duplicate Target Rpc name [" + m_td.FullName + ":" + md.Name + "]");
                            Weaver.fail = true;
                            return;
                        }
                        names.Add(md.Name);
                        m_TargetRpcs.Add(md);

                        MethodDefinition rpcFunc = ProcessTargetRpcInvoke(md);
                        if (rpcFunc != null)
                        {
                            m_TargetRpcInvocationFuncs.Add(rpcFunc);
                        }

                        MethodDefinition rpcCallFunc = ProcessTargetRpcCall(md, ca);
                        if (rpcCallFunc != null)
                        {
                            m_TargetRpcCallFuncs.Add(rpcCallFunc);
                            Weaver.lists.replacedMethods.Add(md);
                            Weaver.lists.replacementMethods.Add(rpcCallFunc);
                        }
                        break;
                    }

                    if (ca.AttributeType.FullName == Weaver.ClientRpcType.FullName)
                    {
                        if (!ProcessMethodsValidateRpc(md, ca))
                            return;

                        if (names.Contains(md.Name))
                        {
                            Log.Error("Duplicate ClientRpc name [" + m_td.FullName + ":" + md.Name + "]");
                            Weaver.fail = true;
                            return;
                        }
                        names.Add(md.Name);
                        m_Rpcs.Add(md);

                        MethodDefinition rpcFunc = ProcessRpcInvoke(md);
                        if (rpcFunc != null)
                        {
                            m_RpcInvocationFuncs.Add(rpcFunc);
                        }

                        MethodDefinition rpcCallFunc = ProcessRpcCall(md, ca);
                        if (rpcCallFunc != null)
                        {
                            m_RpcCallFuncs.Add(rpcCallFunc);
                            Weaver.lists.replacedMethods.Add(md);
                            Weaver.lists.replacementMethods.Add(rpcCallFunc);
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

        MethodDefinition ProcessEventInvoke(EventDefinition ed)
        {
            // find the field that matches the event
            FieldDefinition eventField = null;
            foreach (FieldDefinition fd in m_td.Fields)
            {
                if (fd.FullName == ed.FullName)
                {
                    eventField = fd;
                    break;
                }
            }
            if (eventField == null)
            {
                Weaver.DLog(m_td, "ERROR: no event field?!");
                Weaver.fail = true;
                return null;
            }

            MethodDefinition cmd = new MethodDefinition("InvokeSyncEvent" + ed.Name, MethodAttributes.Family |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);

            ILProcessor cmdWorker = cmd.Body.GetILProcessor();
            Instruction label1 = cmdWorker.Create(OpCodes.Nop);
            Instruction label2 = cmdWorker.Create(OpCodes.Nop);

            WriteClientActiveCheck(cmdWorker, ed.Name, label1, "Event");

            // null event check
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldarg_0));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Castclass, m_td));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldfld, eventField));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Brtrue, label2));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ret));
            cmdWorker.Append(label2);

            // setup reader
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldarg_0));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Castclass, m_td));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldfld, eventField));

            // read the event arguments
            MethodReference invoke = Weaver.ResolveMethod(eventField.FieldType, "Invoke");
            if (!ProcessNetworkReaderParameters(invoke.Resolve(), cmdWorker, false))
                return null;

            // invoke actual event delegate function
            cmdWorker.Append(cmdWorker.Create(OpCodes.Callvirt, invoke));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ret));

            AddInvokeParameters(cmd.Parameters);

            return cmd;
        }

        MethodDefinition ProcessEventCall(EventDefinition ed, CustomAttribute ca)
        {
            MethodReference invoke = Weaver.ResolveMethod(ed.EventType, "Invoke");
            MethodDefinition evt = new MethodDefinition("Call" +  ed.Name, MethodAttributes.Public |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);
            // add paramters
            foreach (ParameterDefinition pd in invoke.Parameters)
            {
                evt.Parameters.Add(new ParameterDefinition(pd.Name, ParameterAttributes.None, pd.ParameterType));
            }

            ILProcessor evtWorker = evt.Body.GetILProcessor();
            Instruction label = evtWorker.Create(OpCodes.Nop);

            WriteSetupLocals(evtWorker);

            WriteServerActiveCheck(evtWorker, ed.Name, label, "Event");

            WriteCreateWriter(evtWorker);

            // create the syncevent id constant
            FieldDefinition evtConstant = new FieldDefinition("kEvent" + ed.Name,
                    FieldAttributes.Static | FieldAttributes.Private,
                    Weaver.int32Type);
            m_td.Fields.Add(evtConstant);

            // write all the arguments that the user passed to the syncevent
            if (!WriteArguments(evtWorker, invoke.Resolve(), "SyncEvent", false))
                return null;

            // invoke interal send and return
            evtWorker.Append(evtWorker.Create(OpCodes.Ldarg_0)); // this
            evtWorker.Append(evtWorker.Create(OpCodes.Ldsfld, evtConstant)); // eventHash
            evtWorker.Append(evtWorker.Create(OpCodes.Ldloc_0)); // writer
            evtWorker.Append(evtWorker.Create(OpCodes.Ldc_I4, GetChannelId(ca)));
            evtWorker.Append(evtWorker.Create(OpCodes.Ldstr, ed.Name));
            evtWorker.Append(evtWorker.Create(OpCodes.Call, Weaver.sendEventInternal));

            evtWorker.Append(evtWorker.Create(OpCodes.Ret));

            return evt;
        }

        void ProcessEvents()
        {
            // find events
            foreach (EventDefinition ed in m_td.Events)
            {
                foreach (var ca in ed.CustomAttributes)
                {
                    if (ca.AttributeType.FullName == Weaver.SyncEventType.FullName)
                    {
                        if (ed.Name.Length > 4 && ed.Name.Substring(0, 5) != "Event")
                        {
                            Log.Error("Event  [" + m_td.FullName + ":" + ed.FullName + "] doesnt have 'Event' prefix");
                            Weaver.fail = true;
                            return;
                        }

                        if (ed.EventType.Resolve().HasGenericParameters)
                        {
                            Log.Error("Event  [" + m_td.FullName + ":" + ed.FullName + "] cannot have generic parameters");
                            Weaver.fail = true;
                            return;
                        }

                        m_Events.Add(ed);
                        MethodDefinition eventFunc = ProcessEventInvoke(ed);
                        if (eventFunc == null)
                        {
                            return;
                        }

                        m_td.Methods.Add(eventFunc);
                        m_EventInvocationFuncs.Add(eventFunc);

                        Weaver.DLog(m_td, "ProcessEvent " + ed);

                        MethodDefinition eventCallFunc = ProcessEventCall(ed, ca);
                        m_td.Methods.Add(eventCallFunc);

                        Weaver.lists.replacedEvents.Add(ed);
                        Weaver.lists.replacementEvents.Add(eventCallFunc);

                        Weaver.DLog(m_td, "  Event: " + ed.Name);
                        break;
                    }
                }
            }
        }

        static MethodDefinition ProcessSyncVarGet(FieldDefinition fd, string originalName)
        {
            //Create the get method
            MethodDefinition get = new MethodDefinition(
                    "get_Network" + originalName, MethodAttributes.Public |
                    MethodAttributes.SpecialName |
                    MethodAttributes.HideBySig,
                    fd.FieldType);

            ILProcessor getWorker = get.Body.GetILProcessor();

            getWorker.Append(getWorker.Create(OpCodes.Ldarg_0));
            getWorker.Append(getWorker.Create(OpCodes.Ldfld, fd));
            getWorker.Append(getWorker.Create(OpCodes.Ret));

            get.Body.Variables.Add(new VariableDefinition(fd.FieldType));
            get.Body.InitLocals = true;
            get.SemanticsAttributes = MethodSemanticsAttributes.Getter;

            return get;
        }

        MethodDefinition ProcessSyncVarSet(FieldDefinition fd, string originalName, long dirtyBit, FieldDefinition netFieldId)
        {
            //Create the set method
            MethodDefinition set = new MethodDefinition("set_Network" + originalName, MethodAttributes.Public |
                    MethodAttributes.SpecialName |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);

            ILProcessor setWorker = set.Body.GetILProcessor();

            // this
            setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));

            // new value to set
            setWorker.Append(setWorker.Create(OpCodes.Ldarg_1));

            // reference to field to set
            setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
            setWorker.Append(setWorker.Create(OpCodes.Ldflda, fd));

            // dirty bit
            setWorker.Append(setWorker.Create(OpCodes.Ldc_I8, dirtyBit)); // 8 byte integer aka long

            MethodDefinition hookFunctionMethod;
            CheckForHookFunction(fd, out hookFunctionMethod);

            if (hookFunctionMethod != null)
            {
                //if (NetworkServer.localClientActive && !syncVarHookGuard)
                Instruction label = setWorker.Create(OpCodes.Nop);
                setWorker.Append(setWorker.Create(OpCodes.Call, Weaver.NetworkServerGetLocalClientActive));
                setWorker.Append(setWorker.Create(OpCodes.Brfalse, label));
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
                setWorker.Append(setWorker.Create(OpCodes.Call, Weaver.getSyncVarHookGuard));
                setWorker.Append(setWorker.Create(OpCodes.Brtrue, label));

                // syncVarHookGuard = true;
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
                setWorker.Append(setWorker.Create(OpCodes.Ldc_I4_1));
                setWorker.Append(setWorker.Create(OpCodes.Call, Weaver.setSyncVarHookGuard));

                // call hook
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_1));
                setWorker.Append(setWorker.Create(OpCodes.Call, hookFunctionMethod));

                // syncVarHookGuard = false;
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
                setWorker.Append(setWorker.Create(OpCodes.Ldc_I4_0));
                setWorker.Append(setWorker.Create(OpCodes.Call, Weaver.setSyncVarHookGuard));

                setWorker.Append(label);
            }

            if (fd.FieldType.FullName == Weaver.gameObjectType.FullName)
            {
                // reference to netId Field to set
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
                setWorker.Append(setWorker.Create(OpCodes.Ldflda, netFieldId));

                setWorker.Append(setWorker.Create(OpCodes.Call, Weaver.setSyncVarGameObjectReference));
            }
            else
            {
                // make generic version of SetSyncVar with field type
                GenericInstanceMethod gm = new GenericInstanceMethod(Weaver.setSyncVarReference);
                gm.GenericArguments.Add(fd.FieldType);

                // invoke SetSyncVar
                setWorker.Append(setWorker.Create(OpCodes.Call, gm));
            }

            setWorker.Append(setWorker.Create(OpCodes.Ret));

            set.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.In, fd.FieldType));
            set.SemanticsAttributes = MethodSemanticsAttributes.Setter;

            return set;
        }

        void ProcessSyncVar(FieldDefinition fd, long dirtyBit)
        {
            string originalName = fd.Name;

            Weaver.lists.replacedFields.Add(fd);
            Weaver.DLog(m_td, "Sync Var " + fd.Name + " " + fd.FieldType + " " + Weaver.gameObjectType);

            // GameObject SyncVars have a new field for netId
            FieldDefinition netFieldId = null;
            if (fd.FieldType.FullName == Weaver.gameObjectType.FullName)
            {
                netFieldId = new FieldDefinition("___" + fd.Name + "NetId",
                                                 FieldAttributes.Private,
                                                 Weaver.uint32Type);

                m_SyncVarNetIds.Add(netFieldId);
                Weaver.lists.netIdFields.Add(netFieldId);
            }

            var get = ProcessSyncVarGet(fd, originalName);
            var set = ProcessSyncVarSet(fd, originalName, dirtyBit, netFieldId);

            //NOTE: is property even needed? Could just use a setter function?
            //create the property
            PropertyDefinition propertyDefinition = new PropertyDefinition("Network" + originalName, PropertyAttributes.None, fd.FieldType)
            {
                GetMethod = get, SetMethod = set
            };

            //add the methods and property to the type.
            m_td.Methods.Add(get);
            m_td.Methods.Add(set);
            m_td.Properties.Add(propertyDefinition);
            Weaver.lists.replacementProperties.Add(set);
        }

        void ProcessSyncVars()
        {
            int numSyncVars = 0;

            // the mapping of dirtybits to sync-vars is implicit in the order of the fields here. this order is recorded in m_replacementProperties.
            // start assigning syncvars at the place the base class stopped, if any
            int dirtyBitCounter = Weaver.GetSyncVarStart(m_td.BaseType.FullName);

            m_SyncVarNetIds.Clear();

            // find syncvars
            foreach (FieldDefinition fd in m_td.Fields)
            {
                foreach (var ca in fd.CustomAttributes)
                {
                    if (ca.AttributeType.FullName == Weaver.SyncVarType.FullName)
                    {
                        var resolvedField = fd.FieldType.Resolve();

                        if (Weaver.IsDerivedFrom(resolvedField, Weaver.NetworkBehaviourType))
                        {
                            Log.Error("SyncVar [" + fd.FullName + "] cannot be derived from NetworkBehaviour.");
                            Weaver.fail = true;
                            return;
                        }

                        if (Weaver.IsDerivedFrom(resolvedField, Weaver.ScriptableObjectType))
                        {
                            Log.Error("SyncVar [" + fd.FullName + "] cannot be derived from ScriptableObject.");
                            Weaver.fail = true;
                            return;
                        }

                        if ((fd.Attributes & FieldAttributes.Static) != 0)
                        {
                            Log.Error("SyncVar [" + fd.FullName + "] cannot be static.");
                            Weaver.fail = true;
                            return;
                        }

                        if (resolvedField.HasGenericParameters)
                        {
                            Log.Error("SyncVar [" + fd.FullName + "] cannot have generic parameters.");
                            Weaver.fail = true;
                            return;
                        }

                        if (resolvedField.IsInterface)
                        {
                            Log.Error("SyncVar [" + fd.FullName + "] cannot be an interface.");
                            Weaver.fail = true;
                            return;
                        }

                        var fieldModuleName = resolvedField.Module.Name;
                        if (fieldModuleName != Weaver.scriptDef.MainModule.Name &&
                            fieldModuleName != Weaver.m_UnityAssemblyDefinition.MainModule.Name &&
                            fieldModuleName != Weaver.m_UNetAssemblyDefinition.MainModule.Name &&
                            fieldModuleName != Weaver.corLib.Name &&
                            fieldModuleName != "System.Runtime.dll" && // this is only for Metro, built-in types are not in corlib on metro
                            fieldModuleName != "netstandard.dll" // handle built-in types when weaving new C#7 compiler assemblies
                            )
                        {
                            Log.Error("SyncVar [" + fd.FullName + "] from " + resolvedField.Module.ToString() + " cannot be a different module.");
                            Weaver.fail = true;
                            return;
                        }

                        if (fd.FieldType.IsArray)
                        {
                            Log.Error("SyncVar [" + fd.FullName + "] cannot be an array. Use a SyncList instead.");
                            Weaver.fail = true;
                            return;
                        }

                        if (Helpers.ImplementsSyncObject(fd.FieldType))
                        {
                            Log.Warning(string.Format("Script class [{0}] has [SyncVar] attribute on SyncList field {1}, SyncLists should not be marked with SyncVar.", m_td.FullName, fd.Name));
                            break;
                        }

                        m_SyncVars.Add(fd);

                        ProcessSyncVar(fd, 1L << dirtyBitCounter);
                        dirtyBitCounter += 1;
                        numSyncVars += 1;

                        if (dirtyBitCounter == k_SyncVarLimit)
                        {
                            Log.Error("Script class [" + m_td.FullName + "] has too many SyncVars (" + k_SyncVarLimit + "). (This could include base classes)");
                            Weaver.fail = true;
                            return;
                        }
                        break;
                    }
                }

                if (fd.FieldType.FullName.Contains("Mirror.SyncListStruct"))
                {
                    Log.Error("SyncListStruct member variable [" + fd.FullName + "] must use a dervied class, like \"class MySyncList : SyncListStruct<MyStruct> {}\".");
                    Weaver.fail = true;
                    return;
                }

                if (Weaver.ImplementsInterface(fd.FieldType.Resolve(), Weaver.SyncObjectType))
                {
                    if (fd.IsStatic)
                    {
                        Log.Error("SyncList [" + m_td.FullName + ":" + fd.FullName + "] cannot be a static");
                        Weaver.fail = true;
                        return;
                    }

                    m_SyncObjects.Add(fd);
                }
            }

            foreach (FieldDefinition fd in m_SyncVarNetIds)
            {
                m_td.Fields.Add(fd);
            }

            Weaver.SetNumSyncVars(m_td.FullName, numSyncVars);
        }

        // stable hash code for strings,  stable accross mono and .net versions
        private static int GetHashCode(string str)
        {
            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }

        bool HasMethod(string name)
        {
            foreach (var method in m_td.Methods)
            {
                if (method.Name == name)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
