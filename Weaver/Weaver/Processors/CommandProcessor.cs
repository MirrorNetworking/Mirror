// all the [Command] code from NetworkBehaviourProcessor in one place
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    public static class CommandProcessor
    {
        const string k_CmdPrefix = "InvokeCmd";

        /*
            // generates code like:
            public void CallCmdThrust(float thrusting, int spin)
            {
                if (isServer)
                {
                    // we are ON the server, invoke directly
                    CmdThrust(thrusting, spin);
                    return;
                }

                NetworkWriter networkWriter = new NetworkWriter();
                networkWriter.Write(thrusting);
                networkWriter.WritePackedUInt32((uint)spin);
                base.SendCommandInternal(cmdName, networkWriter, cmdName);
            }
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

            ILProcessor cmdWorker = cmd.Body.GetILProcessor();
            Instruction label = cmdWorker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteSetupLocals(cmdWorker);

            if (Weaver.GenerateLogErrors)
            {
                cmdWorker.Append(cmdWorker.Create(OpCodes.Ldstr, "Call Command function " + md.Name));
                cmdWorker.Append(cmdWorker.Create(OpCodes.Call, Weaver.logErrorReference));
            }

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
            NetworkBehaviourProcessor.WriteCreateWriter(cmdWorker);

            // write all the arguments that the user passed to the Cmd call
            if (!NetworkBehaviourProcessor.WriteArguments(cmdWorker, md, "Command", false))
                return null;

            string cmdName = md.Name;
            int index = cmdName.IndexOf(k_CmdPrefix);
            if (index > -1)
            {
                cmdName = cmdName.Substring(k_CmdPrefix.Length);
            }

            // invoke internal send and return
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldarg_0)); // load 'base.' to call the SendCommand function with
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldtoken, td));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Call, Weaver.getTypeFromHandleReference)); // invokerClass
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldstr, cmdName));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldloc_0)); // writer
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldc_I4, NetworkBehaviourProcessor.GetChannelId(ca)));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Call, Weaver.sendCommandInternal));

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
        public static MethodDefinition ProcessCommandInvoke(TypeDefinition td, MethodDefinition md)
        {
            MethodDefinition cmd = new MethodDefinition(k_CmdPrefix + md.Name,
                MethodAttributes.Family | MethodAttributes.Static | MethodAttributes.HideBySig,
                Weaver.voidType);

            ILProcessor cmdWorker = cmd.Body.GetILProcessor();
            Instruction label = cmdWorker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteServerActiveCheck(cmdWorker, md.Name, label, "Command");

            // setup for reader
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldarg_0));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Castclass, td));

            if (!NetworkBehaviourProcessor.ProcessNetworkReaderParameters(td, md, cmdWorker, false))
                return null;

            // invoke actual command function
            cmdWorker.Append(cmdWorker.Create(OpCodes.Callvirt, md));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ret));

            NetworkBehaviourProcessor.AddInvokeParameters(cmd.Parameters);

            return cmd;
        }

        public static bool ProcessMethodsValidateCommand(TypeDefinition td, MethodDefinition md, CustomAttribute ca)
        {
            if (md.Name.Length > 2 && md.Name.Substring(0, 3) != "Cmd")
            {
                Weaver.Error("Command function [" + td.FullName + ":" + md.Name + "] doesnt have 'Cmd' prefix");
                return false;
            }

            if (md.IsStatic)
            {
                Weaver.Error("Command function [" + td.FullName + ":" + md.Name + "] cant be a static method");
                return false;
            }

            // validate
            return NetworkBehaviourProcessor.ProcessMethodsValidateFunction(td, md, "Command") &&
                   NetworkBehaviourProcessor.ProcessMethodsValidateParameters(td, md, ca, "Command");
        }
    }
}
