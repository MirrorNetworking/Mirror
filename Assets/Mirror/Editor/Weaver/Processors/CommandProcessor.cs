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
                // on host, invoke the original function immediately.
                // -> Mirror's Host mode is just a server, we can't simulate an independent client in Unity
                // -> delaying this for later introduces cooldown & prediction issues in games.
                //    for example, assume CmdFireWeapon function with 100ms cooldown between shots.
                //
                //    client-only mode:
                //      simply calling CmdFireWeapon and waiting for [SyncVar] cooldown would be too irregular (i.e. 150ms)
                //      client has to predict the cooldown locally in order to call CmdFireWeapon every 100ms
                //      this works fine, and that's how games need to predict weapon firing / skill usage / etc.
                //
                //    host mode:
                //       Cmds usued to be queued up for network message processing to 'simulate' a client
                //       this introduces a massive headache:
                //          firing 3x would queue up CmdFireWeapon 3 times, without ever setting the cooldown yet
                //          eventually messages are processed:
                //            CmdFireWeapon first call goes through, sets cooldown
                //            CmdFireWeapon second/third call would be rejected: "user attempted to fire on cooldown"
                //          in other words, we would need to predict cooldowns on host too, which is super weird since host is the server
                //
                //     common sense: on host, calling a Cmd should happen immediately, anything else is too much magic
                //                   and causes edge cases until Unity supports true server/client separation on host!
                //
                if (isServer && isClient) // isHost
                {
                    UserCode_CmdThrust(value);
                    return;
                }

                // otherwise send a command message over the network
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

            Instruction skipIfNotHost = worker.Create(OpCodes.Nop);

            // Check if isServer && isClient
            // note that we don't use NetworkServer/Client.active here,
            // otherwise [Command] tests which simulate server/client separation would fail.
            worker.Emit(OpCodes.Ldarg_0); // loads this. for isServer check later
            worker.Emit(OpCodes.Call, weaverTypes.NetworkBehaviourIsServerReference);
            worker.Emit(OpCodes.Brfalse, skipIfNotHost);

            worker.Emit(OpCodes.Ldarg_0); // loads this. for isClient check later
            worker.Emit(OpCodes.Call, weaverTypes.NetworkBehaviourIsClientReference);
            worker.Emit(OpCodes.Brfalse, skipIfNotHost);

            // Load 'this' reference (Ldarg_0)
            worker.Emit(OpCodes.Ldarg_0);

            // Load all the remaining arguments (Ldarg_1, Ldarg_2, ...)
            for (int i = 0; i < md.Parameters.Count; i++)
            {
                // special case: NetworkConnection parameter in command needs to be
                // filled by the sender's connection on server/host.
                ParameterDefinition param = md.Parameters[i];
                if (NetworkBehaviourProcessor.IsSenderConnection(param, RemoteCallType.Command))
                {
                    // load 'this.'
                    worker.Emit(OpCodes.Ldarg_0);
                    // call get_connectionToClient
                    worker.Emit(OpCodes.Call, weaverTypes.NetworkBehaviourConnectionToClientReference);
                }
                else
                {
                    worker.Emit(OpCodes.Ldarg, i + 1); // Ldarg_0 is for 'this.'
                }
            }

            // Call the original function directly (UserCode_CmdTest__Int32)
            worker.Emit(OpCodes.Call, cmd);
            worker.Emit(OpCodes.Ret);

            worker.Append(skipIfNotHost);

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
            string cmdName = Weaver.GenerateMethodName(RemoteCalls.RemoteProcedureCalls.InvokeRpcPrefix, method);

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
                    // NetworkConnection is 3rd arg (arg0 is "obj" not "this" because method is static)
                    // example: static void InvokeCmdCmdSendCommand(NetworkBehaviour obj, NetworkReader reader, NetworkConnection connection)
                    worker.Emit(OpCodes.Ldarg_2);
                }
            }
        }
    }
}
