using Mono.Cecil;
using Mono.Cecil.Cil;
namespace Mirror.Weaver
{
    public enum Client { Owner, Observers, Connection }

    /// <summary>
    /// Processes [Rpc] methods in NetworkBehaviour
    /// </summary>
    public static class RpcProcessor
    {
        // helper functions to check if the method has a NetworkConnection parameter
        public static bool HasNetworkConnectionParameter(MethodDefinition md)
        {
            return md.Parameters.Count > 0 &&
                   md.Parameters[0].ParameterType.Is<INetworkConnection>();
        }

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
        public static MethodDefinition GenerateSkeleton(MethodDefinition md, MethodDefinition userCodeFunc, CustomAttribute clientRpcAttr)
        {
            var rpc = new MethodDefinition(
                MethodProcessor.SkeletonPrefix + md.Name,
                MethodAttributes.Family | MethodAttributes.Static | MethodAttributes.HideBySig,
                WeaverTypes.Import(typeof(void)));

            ILProcessor worker = rpc.Body.GetILProcessor();
            Instruction label = worker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteClientActiveCheck(worker, md.Name, label, "RPC");

            // setup for reader
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Castclass, md.DeclaringType));

            // NetworkConnection parameter is only required for Client.Connection
            Client target = clientRpcAttr.GetField("target", Client.Observers);
            bool hasNetworkConnection = target == Client.Connection && HasNetworkConnectionParameter(md);

            if (hasNetworkConnection)
            {
                //client.connection
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Call, WeaverTypes.BehaviorConnectionToServerReference));
            }
            
            if (!NetworkBehaviourProcessor.ReadArguments(md, worker, hasNetworkConnection))
                return null;

            // invoke actual ServerRpc function
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
        /// <param name="ServerRpcAttr">The attribute that made this an RPC</param>
        /// <returns>The method containing the original code</returns>
        /// <remarks>
        /// Generates code like this: (Observers case)
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
        ///
        /// Generates code like this: (Owner/Connection case)
        /// <code>
        /// public void TargetTest(NetworkConnection conn, int param)
        /// {
        ///     NetworkWriter writer = new NetworkWriter();
        ///     writer.WritePackedUInt32((uint)param);
        ///     base.SendTargetRPCInternal(conn, typeof(class), "TargetTest", val);
        /// }
        /// 
        /// public void UserCode_TargetTest(NetworkConnection conn, int param)
        /// {
        ///     // whatever the user did before
        /// }
        /// </code>
        /// or if no connection is specified
        ///
        /// <code>
        /// public void TargetTest (int param)
        /// {
        ///     NetworkWriter writer = new NetworkWriter();
        ///     writer.WritePackedUInt32((uint) param);
        ///     base.SendTargetRPCInternal(null, typeof(class), "TargetTest", val);
        /// }
        /// 
        /// public void UserCode_TargetTest(int param)
        /// {
        ///     // whatever the user did before
        /// }
        /// </code>
        /// </remarks>
        public static MethodDefinition GenerateStub(MethodDefinition md, CustomAttribute clientRpcAttr)
        {
            MethodDefinition rpc = MethodProcessor.SubstituteMethod(md);

            ILProcessor worker = md.Body.GetILProcessor();

            NetworkBehaviourProcessor.WriteSetupLocals(worker);

            NetworkBehaviourProcessor.WriteCreateWriter(worker);

            // write all the arguments that the user passed to the Rpc call
            if (!NetworkBehaviourProcessor.WriteArguments(worker, md, RemoteCallType.ClientRpc))
                return null;

            string rpcName = md.Name;

            Client target = clientRpcAttr.GetField("target", Client.Observers); 
            int channel = clientRpcAttr.GetField("channel", 0);
            bool excludeOwner = clientRpcAttr.GetField("excludeOwner", false);

            // invoke SendInternal and return
            // this
            worker.Append(worker.Create(OpCodes.Ldarg_0));

            if (target == Client.Connection && HasNetworkConnectionParameter(md))
                worker.Append(worker.Create(OpCodes.Ldarg_1));
            else if (target == Client.Owner)
                worker.Append(worker.Create(OpCodes.Ldnull));

            worker.Append(worker.Create(OpCodes.Ldtoken, md.DeclaringType));
            // invokerClass
            worker.Append(worker.Create(OpCodes.Call, WeaverTypes.getTypeFromHandleReference));
            worker.Append(worker.Create(OpCodes.Ldstr, rpcName));
            // writer
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Ldc_I4, channel));

            if (target == Client.Observers)
            {
                worker.Append(worker.Create(excludeOwner ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
                worker.Append(worker.Create(OpCodes.Callvirt, WeaverTypes.sendRpcInternal));
            }
            else
            {
                worker.Append(worker.Create(OpCodes.Callvirt, WeaverTypes.sendTargetRpcInternal));
            }

            NetworkBehaviourProcessor.WriteRecycleWriter(worker);

            worker.Append(worker.Create(OpCodes.Ret));

            return rpc;
        }

        public static bool Validate(MethodDefinition md, CustomAttribute clientRpcAttr)
        {

            Client target = clientRpcAttr.GetField("target", Client.Observers); 
            if (target == Client.Connection && !HasNetworkConnectionParameter(md))
            {
                Weaver.Error("ClientRpc with Client.Connection needs a network connection parameter", md);
                return false;
            }

            bool excludeOwner = clientRpcAttr.GetField("excludeOwner", false);
            if (target == Client.Owner && excludeOwner)
            {
                Weaver.Error("ClientRpc with Client.Owner cannot have excludeOwner set as true", md);
                return false;
            }
            return true;

        }
    }
}
