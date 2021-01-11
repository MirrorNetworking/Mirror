using System;
using System.Collections.Generic;
using Mirror.RemoteCalls;
using Mono.Cecil;
using Mono.Cecil.Cil;
namespace Mirror.Weaver
{
    public enum Client { Owner, Observers, Connection }

    /// <summary>
    /// Processes [Rpc] methods in NetworkBehaviour
    /// </summary>
    public class ClientRpcProcessor : RpcProcessor
    {
        struct ClientRpcMethod
        {
            public MethodDefinition stub;
            public Client target;
            public bool excludeOwner;
            public MethodDefinition skeleton;
        }

        readonly List<ClientRpcMethod> clientRpcs = new List<ClientRpcMethod>();

        public ClientRpcProcessor(ModuleDefinition module, Readers readers, Writers writers, IWeaverLogger logger) : base(module, readers, writers, logger)
        {
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
        MethodDefinition GenerateSkeleton(MethodDefinition md, MethodDefinition userCodeFunc, CustomAttribute clientRpcAttr)
        {
            MethodDefinition rpc = md.DeclaringType.AddMethod(
                SkeletonPrefix + md.Name,
                MethodAttributes.Family | MethodAttributes.Static | MethodAttributes.HideBySig);

            _ = rpc.AddParam<NetworkBehaviour>("obj");
            _ = rpc.AddParam<NetworkReader>("reader");
            _ = rpc.AddParam<INetworkConnection>("senderConnection");
            _ = rpc.AddParam<int>("replyId");

            ILProcessor worker = rpc.Body.GetILProcessor();

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
                worker.Append(worker.Create(OpCodes.Call, (NetworkBehaviour nb) => nb.ConnectionToServer));
            }
            
            if (!ReadArguments(md, worker, hasNetworkConnection))
                return rpc;

            // invoke actual ServerRpc function
            worker.Append(worker.Create(OpCodes.Callvirt, userCodeFunc));
            worker.Append(worker.Create(OpCodes.Ret));

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
        ///     base.SendRpcInternal(typeof(class),"RpcTest", writer, 0);
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
        ///     base.SendTargetRpcInternal(conn, typeof(class), "TargetTest", val);
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
        ///     base.SendTargetRpcInternal(null, typeof(class), "TargetTest", val);
        /// }
        /// 
        /// public void UserCode_TargetTest(int param)
        /// {
        ///     // whatever the user did before
        /// }
        /// </code>
        /// </remarks>
        MethodDefinition GenerateStub(MethodDefinition md, CustomAttribute clientRpcAttr)
        {
            MethodDefinition rpc = SubstituteMethod(md);

            ILProcessor worker = md.Body.GetILProcessor();

            // NetworkWriter writer = NetworkWriterPool.GetWriter()
            VariableDefinition writer = md.AddLocal<PooledNetworkWriter>();
            worker.Append(worker.Create(OpCodes.Call, () => NetworkWriterPool.GetWriter()));
            worker.Append(worker.Create(OpCodes.Stloc, writer));

            // write all the arguments that the user passed to the Rpc call
            if (!WriteArguments(worker, md, writer, RemoteCallType.ClientRpc))
                return rpc;

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
            worker.Append(worker.Create(OpCodes.Call, () => Type.GetTypeFromHandle(default)));
            worker.Append(worker.Create(OpCodes.Ldstr, rpcName));
            // writer
            worker.Append(worker.Create(OpCodes.Ldloc, writer));
            worker.Append(worker.Create(OpCodes.Ldc_I4, channel));

            if (target == Client.Observers)
            {
                worker.Append(worker.Create(excludeOwner ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
                MethodReference sendRpcRef = md.Module.ImportReference<NetworkBehaviour>(nb => nb.SendRpcInternal(default, default, default, default, default));
                worker.Append(worker.Create(OpCodes.Callvirt, sendRpcRef));
            }
            else
            {
                MethodReference sendTargetRpcRef = md.Module.ImportReference<NetworkBehaviour>(nb => nb.SendTargetRpcInternal(default, default, default, default, default));
                worker.Append(worker.Create(OpCodes.Callvirt, sendTargetRpcRef));
            }

            // NetworkWriterPool.Recycle(writer);
            worker.Append(worker.Create(OpCodes.Ldloc, writer));
            worker.Append(worker.Create(OpCodes.Call, () => NetworkWriterPool.Recycle(default)));

            worker.Append(worker.Create(OpCodes.Ret));

            return rpc;
        }

        bool Validate(MethodDefinition md, CustomAttribute clientRpcAttr)
        {
            if (!md.ReturnType.Is(typeof(void)))
            {
                logger.Error($"{md.Name} cannot return a value.  Make it void instead", md);
                return false;
            }

            Client target = clientRpcAttr.GetField("target", Client.Observers); 
            if (target == Client.Connection && !HasNetworkConnectionParameter(md))
            {
                logger.Error("ClientRpc with Client.Connection needs a network connection parameter", md);
                return false;
            }

            bool excludeOwner = clientRpcAttr.GetField("excludeOwner", false);
            if (target == Client.Owner && excludeOwner)
            {
                logger.Error("ClientRpc with Client.Owner cannot have excludeOwner set as true", md);
                return false;
            }
            return true;

        }


        public void RegisterClientRpcs(ILProcessor cctorWorker)
        {
            foreach (ClientRpcMethod clientRpcResult in clientRpcs)
            {
                GenerateRegisterRemoteDelegate(cctorWorker, clientRpcResult.skeleton, clientRpcResult.stub.Name);
            }
        }

        /*
            // This generates code like:
            NetworkBehaviour.RegisterServerRpcDelegate(base.GetType(), "CmdThrust", new NetworkBehaviour.CmdDelegate(ShipControl.InvokeCmdCmdThrust));
        */
        void GenerateRegisterRemoteDelegate(ILProcessor worker, MethodDefinition func, string cmdName)
        {
            TypeDefinition netBehaviourSubclass = func.DeclaringType;
            worker.Append(worker.Create(OpCodes.Ldtoken, netBehaviourSubclass));
            worker.Append(worker.Create(OpCodes.Call, () => Type.GetTypeFromHandle(default)));
            worker.Append(worker.Create(OpCodes.Ldstr, cmdName));
            worker.Append(worker.Create(OpCodes.Ldnull));
            CreateRpcDelegate(worker, func);
            worker.Append(worker.Create(OpCodes.Call, () => RemoteCallHelper.RegisterRpcDelegate(default, default, default)));
        }

        public void ProcessClientRpc(MethodDefinition md, CustomAttribute clientRpcAttr)
        {
            if (!ValidateRemoteCallAndParameters(md, RemoteCallType.ClientRpc))
            {
                return;
            }

            if (!Validate(md, clientRpcAttr))
                return;

            Client clientTarget = clientRpcAttr.GetField("target", Client.Observers);
            bool excludeOwner = clientRpcAttr.GetField("excludeOwner", false);

            MethodDefinition userCodeFunc = GenerateStub(md, clientRpcAttr);

            MethodDefinition skeletonFunc = GenerateSkeleton(md, userCodeFunc, clientRpcAttr);
            clientRpcs.Add(new ClientRpcMethod
            {
                stub = md,
                target = clientTarget,
                excludeOwner = excludeOwner,
                skeleton = skeletonFunc
            });
        }
    }
}
