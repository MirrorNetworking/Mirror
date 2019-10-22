// all the [Command] code from NetworkBehaviourProcessor in one place
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    public static class CommandProcessor
    {
        const string CmdPrefix = "InvokeCmd";

        /*
            // generates code like:
            public void CmdThrust(float thrusting, int spin)
            {
                NetworkWriter networkWriter = new NetworkWriter();
                networkWriter.Write(thrusting);
                networkWriter.WritePackedUInt32((uint)spin);
                base.SendCommandInternal(cmdName, networkWriter, cmdName);
            }

            public void CallCmdThrust(float thrusting, int spin)
            {
                // whatever the user was doing before
            }

            Originally HLAPI put the send message code inside the Call function
            and then proceeded to replace every call to CmdTrust with CallCmdTrust

            This method moves all the user's code into the "Call" method
            and replaces the body of the original method with the send message code.
            This way we do not need to modify the code anywhere else,  and this works
            correctly in dependent assemblies
        */
        public static MethodDefinition ProcessCommandCall(TypeDefinition td, MethodDefinition md, CustomAttribute ca)
        {
            MethodDefinition cmd = new MethodDefinition("Call" + md.Name,
                    MethodAttributes.Public | MethodAttributes.HideBySig,
                    Weaver.voidType);

            // add parameters
            foreach (ParameterDefinition pd in md.Parameters)
            {
                cmd.Parameters.Add(new ParameterDefinition(pd.Name, ParameterAttributes.None, pd.ParameterType));
            }

            // move the old body to the new function
            MethodBody newBody = cmd.Body;
            cmd.Body = md.Body;
            md.Body = newBody;

            ILProcessor cmdWorker = md.Body.GetILProcessor();

            NetworkBehaviourProcessor.WriteSetupLocals(cmdWorker);

            if (Weaver.GenerateLogErrors)
            {
                cmdWorker.Append(cmdWorker.Create(OpCodes.Ldstr, "Call Command function " + md.Name));
                cmdWorker.Append(cmdWorker.Create(OpCodes.Call, Weaver.logErrorReference));
            }

            // NetworkWriter writer = new NetworkWriter();
            NetworkBehaviourProcessor.WriteCreateWriter(cmdWorker);

            // write all the arguments that the user passed to the Cmd call
            if (!NetworkBehaviourProcessor.WriteArguments(cmdWorker, md, false))
                return null;

            string cmdName = md.Name;
            int index = cmdName.IndexOf(CmdPrefix);
            if (index > -1)
            {
                cmdName = cmdName.Substring(CmdPrefix.Length);
            }

            // invoke internal send and return
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldarg_0)); // load 'base.' to call the SendCommand function with
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldtoken, td));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Call, Weaver.getTypeFromHandleReference)); // invokerClass
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldstr, cmdName));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldloc_0)); // writer
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldc_I4, NetworkBehaviourProcessor.GetChannelId(ca)));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Call, Weaver.sendCommandInternal));

            NetworkBehaviourProcessor.WriteRecycleWriter(cmdWorker);

            cmdWorker.Append(cmdWorker.Create(OpCodes.Ret));

            return cmd;
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
        public static MethodDefinition ProcessCommandInvoke(TypeDefinition td, MethodDefinition md, MethodDefinition cmdCallFunc)
        {
            MethodDefinition cmd = new MethodDefinition(CmdPrefix + md.Name,
                MethodAttributes.Family | MethodAttributes.Static | MethodAttributes.HideBySig,
                Weaver.voidType);

            ILProcessor cmdWorker = cmd.Body.GetILProcessor();
            Instruction label = cmdWorker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteServerActiveCheck(cmdWorker, md.Name, label, "Command");

            // setup for reader
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldarg_0));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Castclass, td));

            if (!NetworkBehaviourProcessor.ProcessNetworkReaderParameters(md, cmdWorker, false))
                return null;

            // invoke actual command function
            cmdWorker.Append(cmdWorker.Create(OpCodes.Callvirt, cmdCallFunc));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ret));

            NetworkBehaviourProcessor.AddInvokeParameters(cmd.Parameters);

            return cmd;
        }

        public static bool ProcessMethodsValidateCommand(MethodDefinition md, CustomAttribute ca)
        {
            if (!md.Name.StartsWith("Cmd"))
            {
                Weaver.Error($"{md} must start with Cmd.  Consider renaming it to Cmd{md.Name}");
                return false;
            }

            if (md.IsStatic)
            {
                Weaver.Error($"{md} cannot be static");
                return false;
            }

            // validate
            return NetworkBehaviourProcessor.ProcessMethodsValidateFunction(md) &&
                   NetworkBehaviourProcessor.ProcessMethodsValidateParameters(md, ca);
        }
    }
}
