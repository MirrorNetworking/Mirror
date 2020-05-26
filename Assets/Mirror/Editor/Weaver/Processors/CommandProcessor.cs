using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    /// <summary>
    /// Processes [Command] methods in NetworkBehaviour
    /// </summary>
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
        public static MethodDefinition ProcessCommandCall(TypeDefinition td, MethodDefinition md, CustomAttribute commandAttr)
        {
            MethodDefinition cmd = MethodProcessor.SubstituteMethod(td, md, "Call" + md.Name);

            ILProcessor worker = md.Body.GetILProcessor();

            NetworkBehaviourProcessor.WriteSetupLocals(worker);

            if (Weaver.GenerateLogErrors)
            {
                worker.Append(worker.Create(OpCodes.Ldstr, "Call Command function " + md.Name));
                worker.Append(worker.Create(OpCodes.Call, Weaver.logErrorReference));
            }

            // NetworkWriter writer = new NetworkWriter();
            NetworkBehaviourProcessor.WriteCreateWriter(worker);

            // write all the arguments that the user passed to the Cmd call
            if (!NetworkBehaviourProcessor.WriteArguments(worker, md, false))
                return null;

            string cmdName = md.Name;
            int index = cmdName.IndexOf(CmdPrefix);
            if (index > -1)
            {
                cmdName = cmdName.Substring(CmdPrefix.Length);
            }

            // invoke internal send and return
            // load 'base.' to call the SendCommand function with
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldtoken, td));
            // invokerClass
            worker.Append(worker.Create(OpCodes.Call, Weaver.getTypeFromHandleReference));
            worker.Append(worker.Create(OpCodes.Ldstr, cmdName));
            // writer
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Ldc_I4, commandAttr.GetField("channel", 0)));
            worker.Append(worker.Create(OpCodes.Call, Weaver.sendCommandInternal));

            NetworkBehaviourProcessor.WriteRecycleWriter(worker);

            worker.Append(worker.Create(OpCodes.Ret));

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

            ILProcessor worker = cmd.Body.GetILProcessor();
            Instruction label = worker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteServerActiveCheck(worker, md.Name, label, "Command");

            // setup for reader
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Castclass, td));

            if (!NetworkBehaviourProcessor.ProcessNetworkReaderParameters(md, worker, false))
                return null;

            // invoke actual command function
            worker.Append(worker.Create(OpCodes.Callvirt, cmdCallFunc));
            worker.Append(worker.Create(OpCodes.Ret));

            NetworkBehaviourProcessor.AddInvokeParameters(cmd.Parameters);
            td.Methods.Add(cmd);
            return cmd;
        }

        public static bool ProcessMethodsValidateCommand(MethodDefinition md, CustomAttribute commandAttr)
        {
            if (!md.Name.StartsWith("Cmd"))
            {
                Weaver.Error($"{md.Name} must start with Cmd.  Consider renaming it to Cmd{md.Name}", md);
                return false;
            }

            if (md.IsStatic)
            {
                Weaver.Error($"{md.Name} cannot be static", md);
                return false;
            }

            // validate
            return NetworkBehaviourProcessor.ProcessMethodsValidateFunction(md) &&
                   NetworkBehaviourProcessor.ProcessMethodsValidateParameters(md, commandAttr);
        }
    }
}
