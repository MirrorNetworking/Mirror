// all the [ServerRpc] code from NetworkBehaviourProcessor in one place
using System;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace Mirror.Weaver
{
    /// <summary>
    /// Processes [ServerRpc] methods in NetworkBehaviour
    /// </summary>
    public static class ServerRpcProcessor
    {
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
        public static MethodDefinition GenerateStub(MethodDefinition md, CustomAttribute serverRpcAttr)
        {
            MethodDefinition cmd = MethodProcessor.SubstituteMethod(md);

            ILProcessor worker = md.Body.GetILProcessor();

            NetworkBehaviourProcessor.WriteSetupLocals(worker);

            // NetworkWriter writer = new NetworkWriter();
            NetworkBehaviourProcessor.WriteCreateWriter(worker);

            // write all the arguments that the user passed to the Cmd call
            if (!NetworkBehaviourProcessor.WriteArguments(worker, md, RemoteCallType.ServerRpc))
                return cmd;

            string cmdName = md.Name;

            int channel = serverRpcAttr.GetField("channel", 0);
            bool requireAuthority = serverRpcAttr.GetField("requireAuthority", true);


            // invoke internal send and return
            // load 'base.' to call the SendServerRpc function with
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldtoken, md.DeclaringType));
            // invokerClass
            worker.Append(worker.Create(OpCodes.Call, WeaverTypes.getTypeFromHandleReference));
            worker.Append(worker.Create(OpCodes.Ldstr, cmdName));
            // writer
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Ldc_I4, channel));
            worker.Append(worker.Create(requireAuthority ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
            CallSendServerRpc(md, worker);

            NetworkBehaviourProcessor.WriteRecycleWriter(worker);

            worker.Append(worker.Create(OpCodes.Ret));

            return cmd;
        }

        private static void CallSendServerRpc(MethodDefinition md, ILProcessor worker)
        {
            if (md.ReturnType.Is(typeof(void)))
            {
                worker.Append(worker.Create(OpCodes.Call, WeaverTypes.sendServerRpcInternal));
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
        public static MethodDefinition GenerateSkeleton(MethodDefinition method, MethodDefinition userCodeFunc)
        {
            var cmd = new MethodDefinition(MethodProcessor.SkeletonPrefix + method.Name,
                MethodAttributes.Family | MethodAttributes.Static | MethodAttributes.HideBySig,
                userCodeFunc.ReturnType);

            NetworkBehaviourProcessor.AddInvokeParameters(cmd.Parameters);
            method.DeclaringType.Methods.Add(cmd);

            ILProcessor worker = cmd.Body.GetILProcessor();

            // setup for reader
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Castclass, method.DeclaringType));

            if (!NetworkBehaviourProcessor.ReadArguments(method, worker, false))
                return cmd;

            AddSenderConnection(method, worker);

            // invoke actual ServerRpc function
            worker.Append(worker.Create(OpCodes.Callvirt, userCodeFunc));
            worker.Append(worker.Create(OpCodes.Ret));

            return cmd;
        }

        static void AddSenderConnection(MethodDefinition method, ILProcessor worker)
        {
            foreach (ParameterDefinition param in method.Parameters)
            {
                if (NetworkBehaviourProcessor.IsNetworkConnection(param.ParameterType))
                {
                    // NetworkConnection is 3nd arg (arg0 is "obj" not "this" because method is static)
                    // exmaple: static void InvokeCmdCmdSendServerRpc(NetworkBehaviour obj, NetworkReader reader, NetworkConnection connection)
                    worker.Append(worker.Create(OpCodes.Ldarg_2));
                }
            }
        }

        internal static bool Validate(MethodDefinition md, CustomAttribute serverRpcAttr)
        {
            Type unitaskType = typeof(UniTask<int>).GetGenericTypeDefinition();
            if (!md.ReturnType.Is(typeof(void)) && !md.ReturnType.Is(unitaskType))
            {
                Weaver.Error($"Use UniTask<{ md.ReturnType}> to return values from [ServerRpc]", md);
                return false;
            }

            return true;
        }
    }
}
