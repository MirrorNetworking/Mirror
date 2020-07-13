using Mono.Cecil;
using Mono.Cecil.Cil;
namespace Mirror.Weaver
{
    /// <summary>
    /// Processes [Rpc] methods in NetworkBehaviour
    /// </summary>
    public static class RpcProcessor
    {
        public const string SkeletonPrefix = "Skeleton_";
        private const string UserCodePrefix = "UserCode_";

        /// <summary>
        /// Generates a skeleton for an RPC
        /// </summary>
        /// <param name="td"></param>
        /// <param name="method"></param>
        /// <param name="cmdCallFunc"></param>
        /// <returns>The newly created skeleton method</returns>
        /// <remarks>
        /// Generates code like this:
        /// <code>
        /// protected static void Skeleton_Test(NetworkBehaviour obj, NetworkReader reader, NetworkConnection senderConnection)
        /// {
        ///     if (!obj.netIdentity.server.active)
        ///     {
        ///         return;
        ///     }
        ///     ((ShipControl) obj).UserCode_Test(reader.ReadSingle(), (int) reader.ReadPackedUInt32());
        /// }
        /// </code>
        /// </remarks>
        public static MethodDefinition GenerateSkeleton(MethodDefinition md, MethodDefinition userCodeFunc)
        {
            var rpc = new MethodDefinition(
                SkeletonPrefix + md.Name,
                MethodAttributes.Family | MethodAttributes.Static | MethodAttributes.HideBySig,
                Weaver.voidType);

            ILProcessor worker = rpc.Body.GetILProcessor();
            Instruction label = worker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteClientActiveCheck(worker, md.Name, label, "RPC");

            // setup for reader
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Castclass, md.DeclaringType));

            if (!NetworkBehaviourProcessor.ReadArguments(md, worker, RemoteCallType.ClientRpc))
                return null;

            // invoke actual command function
            worker.Append(worker.Create(OpCodes.Callvirt, userCodeFunc));
            worker.Append(worker.Create(OpCodes.Ret));

            NetworkBehaviourProcessor.AddInvokeParameters(rpc.Parameters);
            md.DeclaringType.Methods.Add(rpc);
            return rpc;
        }

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
        /// public void Test (int param)
        /// {
        ///     NetworkWriter writer = new NetworkWriter();
        ///     writer.WritePackedUInt32((uint) param);
        ///     base.SendRPCInternal(typeof(class),"RpcTest", writer, 0);
        /// }
        /// public void UserCode_Test(int param)
        /// {
        ///     // whatever the user did before
        /// }
        /// </code>
        /// </remarks>
        public static MethodDefinition GenerateStub(MethodDefinition md, CustomAttribute clientRpcAttr)
        {
            MethodDefinition rpc = MethodProcessor.SubstituteMethod(md, UserCodePrefix + md.Name);

            ILProcessor worker = md.Body.GetILProcessor();

            NetworkBehaviourProcessor.WriteSetupLocals(worker);

            NetworkBehaviourProcessor.WriteCreateWriter(worker);

            // write all the arguments that the user passed to the Rpc call
            if (!NetworkBehaviourProcessor.WriteArguments(worker, md, RemoteCallType.ClientRpc))
                return null;

            string rpcName = md.Name;

            int channel = clientRpcAttr.GetField("channel", 0);
            bool excludeOwner = clientRpcAttr.GetField("excludeOwner", false);

            // invoke SendInternal and return
            // this
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldtoken, md.DeclaringType));
            // invokerClass
            worker.Append(worker.Create(OpCodes.Call, Weaver.getTypeFromHandleReference));
            worker.Append(worker.Create(OpCodes.Ldstr, rpcName));
            // writer
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Ldc_I4, channel));
            worker.Append(worker.Create(excludeOwner ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
            worker.Append(worker.Create(OpCodes.Callvirt, Weaver.sendRpcInternal));

            NetworkBehaviourProcessor.WriteRecycleWriter(worker);

            worker.Append(worker.Create(OpCodes.Ret));

            return rpc;
        }

        public static bool Validate(MethodDefinition md)
        {
            if (md.IsAbstract)
            {
                Weaver.Error("Abstract ClientRpc are currently not supported, use virtual method instead", md);
                return false;
            }

            if (md.IsStatic)
            {
                Weaver.Error($"{md.Name} must not be static", md);
                return false;
            }

            // validate
            return NetworkBehaviourProcessor.ProcessMethodsValidateFunction(md) &&
                   NetworkBehaviourProcessor.ProcessMethodsValidateParameters(md, RemoteCallType.ClientRpc);
        }
    }
}
