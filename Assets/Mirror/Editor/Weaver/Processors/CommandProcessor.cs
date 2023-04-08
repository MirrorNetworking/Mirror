using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    // Processes [Command] methods in NetworkBehaviour
    public static class CommandProcessor
    {
        /*
            // generates code like:
            public void CmdThrust(float thrusting, int spin)
            {
                NetworkWriterPooled writer = NetworkWriterPool.Get();
                writer.Write(thrusting);
                writer.WritePackedUInt32((uint)spin);
                base.SendCommandInternal(cmdName, cmdHash, writer, channel);
                NetworkWriterPool.Return(writer);
            }

            public void CallCmdThrust(float thrusting, int spin)
            {
                // whatever the user was doing before
            }

            Originally HLAPI put the send message code inside the Call function
            and then proceeded to replace every call to CmdTrust with CallCmdTrust

            This method moves all the user's code into the "CallCmd" method
            and replaces the body of the original method with the send message code.
            This way we do not need to modify the code anywhere else,  and this works
            correctly in dependent assemblies
        */
        public static MethodDefinition ProcessCommandCall(WeaverTypes weaverTypes, Writers writers, Logger Log, TypeDefinition td, MethodDefinition md, CustomAttribute commandAttr, ref bool WeavingFailed)
        {
            MethodDefinition cmd = MethodProcessor.SubstituteMethod(Log, td, md, ref WeavingFailed);

            ILProcessor worker = md.Body.GetILProcessor();

            NetworkBehaviourProcessor.WriteSetupLocals(worker, weaverTypes);

            // NetworkWriter writer = new NetworkWriter();
            NetworkBehaviourProcessor.WriteGetWriter(worker, weaverTypes);

            // write all the arguments that the user passed to the Cmd call
            if (!NetworkBehaviourProcessor.WriteArguments(worker, writers, Log, md, RemoteCallType.Command, ref WeavingFailed))
                return null;

            int channel = commandAttr.GetField("channel", 0);
            bool requiresAuthority = commandAttr.GetField("requiresAuthority", true);

            // invoke internal send and return
            // load 'base.' to call the SendCommand function with
            worker.Emit(OpCodes.Ldarg_0);
            // pass full function name to avoid ClassA.Func <-> ClassB.Func collisions
            worker.Emit(OpCodes.Ldstr, md.FullName);
            // pass the function hash so we don't have to compute it at runtime
            // otherwise each GetStableHash call requires O(N) complexity.
            // noticeable for long function names:
            // https://github.com/MirrorNetworking/Mirror/issues/3375
            worker.Emit(OpCodes.Ldc_I4, md.FullName.GetStableHashCode());
            // writer
            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Ldc_I4, channel);
            // requiresAuthority ? 1 : 0
            worker.Emit(requiresAuthority ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            worker.Emit(OpCodes.Call, weaverTypes.sendCommandInternal);

            NetworkBehaviourProcessor.WriteReturnWriter(worker, weaverTypes);

            worker.Emit(OpCodes.Ret);
            return cmd;
        }

        /*
            // generates code like:
            protected static void InvokeCmdCmdThrust(NetworkBehaviour obj, NetworkReader reader, NetworkConnection senderConnection)
            {
                if (!NetworkServer.active)
                {
                    return;
                }
                ((ShipControl)obj).CmdThrust(reader.ReadSingle(), (int)reader.ReadPackedUInt32());
            }
        */
        public static MethodDefinition ProcessCommandInvoke(WeaverTypes weaverTypes, Readers readers, Logger Log, TypeDefinition td, MethodDefinition method, MethodDefinition cmdCallFunc, ref bool WeavingFailed)
        {
            string cmdName = Weaver.GenerateMethodName(Weaver.InvokeRpcPrefix, method);

            MethodDefinition cmd = new MethodDefinition(cmdName,
                MethodAttributes.Family | MethodAttributes.Static | MethodAttributes.HideBySig,
                weaverTypes.Import(typeof(void)));

            ILProcessor worker = cmd.Body.GetILProcessor();
            Instruction label = worker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteServerActiveCheck(worker, weaverTypes, method.Name, label, "Command");

            // setup for reader
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Castclass, td);

            if (!NetworkBehaviourProcessor.ReadArguments(method, readers, Log, worker, RemoteCallType.Command, ref WeavingFailed))
                return null;

            AddSenderConnection(method, worker);

            // invoke actual command function
            worker.Emit(OpCodes.Callvirt, cmdCallFunc);
            worker.Emit(OpCodes.Ret);

            NetworkBehaviourProcessor.AddInvokeParameters(weaverTypes, cmd.Parameters);

            td.Methods.Add(cmd);
            return cmd;
        }

        static void AddSenderConnection(MethodDefinition method, ILProcessor worker)
        {
            foreach (ParameterDefinition param in method.Parameters)
            {
                if (NetworkBehaviourProcessor.IsSenderConnection(param, RemoteCallType.Command))
                {
                    // NetworkConnection is 3nd arg (arg0 is "obj" not "this" because method is static)
                    // example: static void InvokeCmdCmdSendCommand(NetworkBehaviour obj, NetworkReader reader, NetworkConnection connection)
                    worker.Emit(OpCodes.Ldarg_2);
                }
            }
        }
    }
}
