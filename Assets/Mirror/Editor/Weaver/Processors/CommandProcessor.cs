// all the [Command] code from NetworkBehaviourProcessor in one place
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    /// <summary>
    /// Processes [Command] methods in NetworkBehaviour
    /// </summary>
    public static class CommandProcessor
    {
        const string SkeletonPrefix = "Skeleton_";
        private const string UserCodePrefix = "UserCode_";

        /// <summary>
        /// Replaces the user code with a stub.
        /// Moves the original code to a new method
        /// </summary>
        /// <param name="td">The class containing the method </param>
        /// <param name="md">The method to be stubbed </param>
        /// <param name="commandAttr">The attribute that made this an RPC</param>
        /// <returns>The method containing the original code</returns>
        /// <remarks>
        /// Generates code like this:
        /// <code>
        /// public void MyCommand(float thrusting, int spin)
        /// {
        ///     NetworkWriter networkWriter = new NetworkWriter();
        ///     networkWriter.Write(thrusting);
        ///     networkWriter.WritePackedUInt32((uint) spin);
        ///     base.SendCommandInternal(cmdName, networkWriter, cmdName);
        /// }
        ///
        /// public void UserCode_MyCommand(float thrusting, int spin)
        /// {
        ///     // whatever the user was doing before
        ///
        /// }
        /// </code>
        /// </remarks>
        public static MethodDefinition GenerateStub(MethodDefinition md, CustomAttribute commandAttr)
        {
            MethodDefinition cmd = MethodProcessor.SubstituteMethod(md, UserCodePrefix + md.Name);

            ILProcessor worker = md.Body.GetILProcessor();

            NetworkBehaviourProcessor.WriteSetupLocals(worker);

            // NetworkWriter writer = new NetworkWriter();
            NetworkBehaviourProcessor.WriteCreateWriter(worker);

            // write all the arguments that the user passed to the Cmd call
            if (!NetworkBehaviourProcessor.WriteArguments(worker, md, RemoteCallType.Command))
                return null;

            string cmdName = md.Name;

            int channel = commandAttr.GetField("channel", 0);
            bool requireAuthority = commandAttr.GetField("requireAuthority", true);


            // invoke internal send and return
            // load 'base.' to call the SendCommand function with
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldtoken, md.DeclaringType));
            // invokerClass
            worker.Append(worker.Create(OpCodes.Call, Weaver.getTypeFromHandleReference));
            worker.Append(worker.Create(OpCodes.Ldstr, cmdName));
            // writer
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Ldc_I4, channel));
            worker.Append(worker.Create(requireAuthority ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
            worker.Append(worker.Create(OpCodes.Call, Weaver.sendCommandInternal));

            NetworkBehaviourProcessor.WriteRecycleWriter(worker);

            worker.Append(worker.Create(OpCodes.Ret));

            return cmd;
        }

        /// <summary>
        /// Generates a skeleton for a command
        /// </summary>
        /// <param name="td"></param>
        /// <param name="method"></param>
        /// <param name="userCodeFunc"></param>
        /// <returns>The newly created skeleton method</returns>
        /// <remarks>
        /// Generates code like this:
        /// <code>
        /// protected static void Skeleton_MyCommand(NetworkBehaviour obj, NetworkReader reader, NetworkConnection senderConnection)
        /// {
        ///     if (!obj.netIdentity.server.active)
        ///     {
        ///         return;
        ///     }
        ///     ((ShipControl) obj).UserCode_Thrust(reader.ReadSingle(), (int) reader.ReadPackedUInt32());
        /// }
        /// </code>
        /// </remarks>
        public static MethodDefinition GenerateSkeleton(MethodDefinition method, MethodDefinition userCodeFunc)
        {
            var cmd = new MethodDefinition(SkeletonPrefix + method.Name,
                MethodAttributes.Family | MethodAttributes.Static | MethodAttributes.HideBySig,
                Weaver.voidType);

            ILProcessor worker = cmd.Body.GetILProcessor();
            Instruction label = worker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteServerActiveCheck(worker, method.Name, label, "Command");

            // setup for reader
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Castclass, method.DeclaringType));

            if (!NetworkBehaviourProcessor.ReadArguments(method, worker, RemoteCallType.Command))
                return null;

            AddSenderConnection(method, worker);

            // invoke actual command function
            worker.Append(worker.Create(OpCodes.Callvirt, userCodeFunc));
            worker.Append(worker.Create(OpCodes.Ret));

            NetworkBehaviourProcessor.AddInvokeParameters(cmd.Parameters);

            method.DeclaringType.Methods.Add(cmd);
            return cmd;
        }

        static void AddSenderConnection(MethodDefinition method, ILProcessor worker)
        {
            foreach (ParameterDefinition param in method.Parameters)
            {
                if (NetworkBehaviourProcessor.IsNetworkConnection(param.ParameterType))
                {
                    // NetworkConnection is 3nd arg (arg0 is "obj" not "this" because method is static)
                    // exmaple: static void InvokeCmdCmdSendCommand(NetworkBehaviour obj, NetworkReader reader, NetworkConnection connection)
                    worker.Append(worker.Create(OpCodes.Ldarg_2));
                }
            }
        }

        public static bool ProcessMethodsValidateCommand(MethodDefinition md)
        {
            if (md.IsStatic)
            {
                Weaver.Error($"{md.Name} cannot be static", md);
                return false;
            }

            // validate
            return NetworkBehaviourProcessor.ProcessMethodsValidateFunction(md) &&
                   NetworkBehaviourProcessor.ProcessMethodsValidateParameters(md, RemoteCallType.Command);
        }
    }
}
