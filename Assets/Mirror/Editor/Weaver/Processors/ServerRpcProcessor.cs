// all the [ServerRpc] code from NetworkBehaviourProcessor in one place
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Mirror.RemoteCalls;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace Mirror.Weaver
{
    /// <summary>
    /// Processes [ServerRpc] methods in NetworkBehaviour
    /// </summary>
    public class ServerRpcProcessor :RpcProcessor
    {
        struct ServerRpcMethod
        {
            public MethodDefinition stub;
            public bool requireAuthority;
            public MethodDefinition skeleton;
        }

        readonly List<ServerRpcMethod> serverRpcs = new List<ServerRpcMethod>();

        public ServerRpcProcessor(ModuleDefinition module, Readers readers, Writers writers, IWeaverLogger logger) : base(module, readers, writers, logger)
        {            
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
        /// Generates code like this:
        /// <code>
        /// public void MyServerRpc(float thrusting, int spin)
        /// {
        ///     NetworkWriter networkWriter = new NetworkWriter();
        ///     networkWriter.Write(thrusting);
        ///     networkWriter.WritePackedUInt32((uint) spin);
        ///     base.SendServerRpcInternal(cmdName, networkWriter, cmdName);
        /// }
        ///
        /// public void UserCode_MyServerRpc(float thrusting, int spin)
        /// {
        ///     // whatever the user was doing before
        ///
        /// }
        /// </code>
        /// </remarks>
        MethodDefinition GenerateStub(MethodDefinition md, CustomAttribute serverRpcAttr)
        {
            MethodDefinition cmd = SubstituteMethod(md);

            ILProcessor worker = md.Body.GetILProcessor();

            // NetworkWriter writer = NetworkWriterPool.GetWriter()
            VariableDefinition writer = md.AddLocal<PooledNetworkWriter>();
            worker.Append(worker.Create(OpCodes.Call, md.Module.ImportReference(() => NetworkWriterPool.GetWriter())));
            worker.Append(worker.Create(OpCodes.Stloc, writer));

            // write all the arguments that the user passed to the Cmd call
            if (!WriteArguments(worker, md, writer, RemoteCallType.ServerRpc))
                return cmd;

            string cmdName = md.Name;

            int channel = serverRpcAttr.GetField("channel", 0);
            bool requireAuthority = serverRpcAttr.GetField("requireAuthority", true);


            // invoke internal send and return
            // load 'base.' to call the SendServerRpc function with
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldtoken, md.DeclaringType));
            // invokerClass
            worker.Append(worker.Create(OpCodes.Call, () => Type.GetTypeFromHandle(default)));
            worker.Append(worker.Create(OpCodes.Ldstr, cmdName));
            // writer
            worker.Append(worker.Create(OpCodes.Ldloc, writer));
            worker.Append(worker.Create(OpCodes.Ldc_I4, channel));
            worker.Append(worker.Create(requireAuthority ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
            CallSendServerRpc(md, worker);

            // NetworkWriterPool.Recycle(writer);
            worker.Append(worker.Create(OpCodes.Ldloc, writer));
            worker.Append(worker.Create(OpCodes.Call, () => NetworkWriterPool.Recycle(default)));

            worker.Append(worker.Create(OpCodes.Ret));

            return cmd;
        }

        private void CallSendServerRpc(MethodDefinition md, ILProcessor worker)
        {
            if (md.ReturnType.Is(typeof(void)))
            {
                MethodReference sendServerRpcRef = md.Module.ImportReference<NetworkBehaviour>(nb => nb.SendServerRpcInternal(default, default, default, default, default));
                worker.Append(worker.Create(OpCodes.Call, sendServerRpcRef));
            }
            else
            {
                // call SendServerRpcWithReturn<T> and return the result
                Type netBehaviour = typeof(NetworkBehaviour);

                MethodInfo sendMethod = netBehaviour.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).First(m => m.Name == nameof(NetworkBehaviour.SendServerRpcWithReturn));
                MethodReference sendRef = md.Module.ImportReference(sendMethod);

                var returnType = md.ReturnType as GenericInstanceType;

                var instanceMethod = new GenericInstanceMethod(sendRef);
                instanceMethod.GenericArguments.Add(returnType.GenericArguments[0]);

                worker.Append(worker.Create(OpCodes.Call, instanceMethod));

            }
        }

        /// <summary>
        /// Generates a skeleton for a ServerRpc
        /// </summary>
        /// <param name="td"></param>
        /// <param name="method"></param>
        /// <param name="userCodeFunc"></param>
        /// <returns>The newly created skeleton method</returns>
        /// <remarks>
        /// Generates code like this:
        /// <code>
        /// protected static void Skeleton_MyServerRpc(NetworkBehaviour obj, NetworkReader reader, NetworkConnection senderConnection)
        /// {
        ///     if (!obj.netIdentity.server.active)
        ///     {
        ///         return;
        ///     }
        ///     ((ShipControl) obj).UserCode_Thrust(reader.ReadSingle(), (int) reader.ReadPackedUInt32());
        /// }
        /// </code>
        /// </remarks>
        MethodDefinition GenerateSkeleton(MethodDefinition method, MethodDefinition userCodeFunc)
        {
            MethodDefinition cmd = method.DeclaringType.AddMethod(SkeletonPrefix + method.Name,
                MethodAttributes.Family | MethodAttributes.Static | MethodAttributes.HideBySig,
                userCodeFunc.ReturnType);

            _ = cmd.AddParam<NetworkBehaviour>("obj");
            _ = cmd.AddParam<NetworkReader>("reader");
            _ = cmd.AddParam<INetworkConnection>("senderConnection");
            _ = cmd.AddParam<int>("replyId");


            ILProcessor worker = cmd.Body.GetILProcessor();

            // setup for reader
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Castclass, method.DeclaringType));

            if (!ReadArguments(method, worker, false))
                return cmd;

            AddSenderConnection(method, worker);

            // invoke actual ServerRpc function
            worker.Append(worker.Create(OpCodes.Callvirt, userCodeFunc));
            worker.Append(worker.Create(OpCodes.Ret));

            return cmd;
        }

        void AddSenderConnection(MethodDefinition method, ILProcessor worker)
        {
            foreach (ParameterDefinition param in method.Parameters)
            {
                if (IsNetworkConnection(param.ParameterType))
                {
                    // NetworkConnection is 3nd arg (arg0 is "obj" not "this" because method is static)
                    // exmaple: static void InvokeCmdCmdSendServerRpc(NetworkBehaviour obj, NetworkReader reader, NetworkConnection connection)
                    worker.Append(worker.Create(OpCodes.Ldarg_2));
                }
            }
        }

        internal bool Validate(MethodDefinition md)
        {
            Type unitaskType = typeof(UniTask<int>).GetGenericTypeDefinition();
            if (!md.ReturnType.Is(typeof(void)) && !md.ReturnType.Is(unitaskType))
            {
                logger.Error($"Use UniTask<{ md.ReturnType}> to return values from [ServerRpc]", md);
                return false;
            }

            return true;
        }

        public void RegisterServerRpcs(ILProcessor cctorWorker)
        {
            foreach (ServerRpcMethod cmdResult in serverRpcs)
            {
                GenerateRegisterServerRpcDelegate(cctorWorker, cmdResult);
            }
        }

        void GenerateRegisterServerRpcDelegate(ILProcessor worker, ServerRpcMethod cmdResult)
        {
            MethodDefinition skeleton = cmdResult.skeleton;
            MethodReference registerMethod = GetRegisterMethod(skeleton);
            string cmdName = cmdResult.stub.Name;
            bool requireAuthority = cmdResult.requireAuthority;

            TypeDefinition netBehaviourSubclass = skeleton.DeclaringType;
            worker.Append(worker.Create(OpCodes.Ldtoken, netBehaviourSubclass));
            worker.Append(worker.Create(OpCodes.Call, () => Type.GetTypeFromHandle(default)));
            worker.Append(worker.Create(OpCodes.Ldstr, cmdName));
            worker.Append(worker.Create(OpCodes.Ldnull));
            CreateRpcDelegate(worker, skeleton);

            worker.Append(worker.Create(requireAuthority ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));

            worker.Append(worker.Create(OpCodes.Call, registerMethod));
        }

        private static MethodReference GetRegisterMethod(MethodDefinition func)
        {
            if (func.ReturnType.Is(typeof(void)))
                return func.Module.ImportReference(() => RemoteCallHelper.RegisterServerRpcDelegate(default, default, default, default));

            var taskReturnType = func.ReturnType as GenericInstanceType;

            TypeReference returnType = taskReturnType.GenericArguments[0];

            var genericRegisterMethod = func.Module.ImportReference(() => RemoteCallHelper.RegisterRequestDelegate<object>(default, default, default, default)) as GenericInstanceMethod;

            var registerInstance = new GenericInstanceMethod(genericRegisterMethod.ElementMethod);
            registerInstance.GenericArguments.Add(returnType);
            return registerInstance;
        }

        public void ProcessServerRpc(MethodDefinition md, CustomAttribute serverRpcAttr)
        {
            if (!ValidateRemoteCallAndParameters(md, RemoteCallType.ServerRpc))
                return;

            if (!Validate(md))
                return;

            bool requireAuthority = serverRpcAttr.GetField("requireAuthority", false);

            MethodDefinition userCodeFunc = GenerateStub(md, serverRpcAttr);

            MethodDefinition skeletonFunc = GenerateSkeleton(md, userCodeFunc);
            serverRpcs.Add(new ServerRpcMethod
            {
                stub = md,
                requireAuthority = requireAuthority,
                skeleton = skeletonFunc
            });
        }
    }
}
