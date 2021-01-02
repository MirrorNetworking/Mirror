using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mirror.RemoteCalls
{
    /// <summary>
    /// Delegate for ServerRpc functions.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    public delegate void CmdDelegate(NetworkBehaviour obj, NetworkReader reader, INetworkConnection senderConnection, int replyId);
    public delegate UniTask<T> RequestDelegate<T>(NetworkBehaviour obj, NetworkReader reader, INetworkConnection senderConnection, int replyId);

    class Skeleton
    {
        public Type invokeClass;
        public MirrorInvokeType invokeType;
        public CmdDelegate invokeFunction;
        public bool cmdRequireAuthority;

        public bool AreEqual(Type invokeClass, MirrorInvokeType invokeType, CmdDelegate invokeFunction)
        {
            return this.invokeClass == invokeClass &&
                    this.invokeType == invokeType &&
                    this.invokeFunction == invokeFunction;
        }

        // InvokeCmd/Rpc can all use the same function here
        internal void Invoke(NetworkReader reader, NetworkBehaviour invokingType, INetworkConnection senderConnection = null, int replyId=0)
        {
            if (invokeClass.IsInstanceOfType(invokingType))
            {
                invokeFunction(invokingType, reader, senderConnection, replyId);
                return;
            }
            throw new MethodInvocationException($"Invalid Rpc call {invokeFunction} for component {invokingType}");
        }
    }

    /// <summary>
    /// Used to help manage remote calls for NetworkBehaviours
    /// </summary>
    public static class RemoteCallHelper
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(RemoteCallHelper));

        static readonly Dictionary<int, Skeleton> cmdHandlerDelegates = new Dictionary<int, Skeleton>();

        /// <summary>
        /// Creates hash from Type and method name
        /// </summary>
        /// <param name="invokeClass"></param>
        /// <param name="methodName"></param>
        /// <returns></returns>
        internal static int GetMethodHash(Type invokeClass, string methodName)
        {
            // (invokeClass + ":" + cmdName).GetStableHashCode() would cause allocations.
            // so hash1 + hash2 is better.
            unchecked
            {
                int hash = invokeClass.FullName.GetStableHashCode();
                return hash * 503 + methodName.GetStableHashCode();
            }
        }

        /// <summary>
        /// helper function register a ServerRpc/Rpc delegate
        /// </summary>
        /// <param name="invokeClass"></param>
        /// <param name="cmdName"></param>
        /// <param name="invokerType"></param>
        /// <param name="func"></param>
        /// <param name="cmdRequireAuthority"></param>
        /// <returns>remote function hash</returns>
        public static int RegisterDelegate(Type invokeClass, string cmdName, MirrorInvokeType invokerType, CmdDelegate func, bool cmdRequireAuthority = true)
        {
            // type+func so Inventory.RpcUse != Equipment.RpcUse
            int cmdHash = GetMethodHash(invokeClass, cmdName);

            if (CheckIfDelegateExists(invokeClass, invokerType, func, cmdHash))
                return cmdHash;

            var invoker = new Skeleton
            {
                invokeType = invokerType,
                invokeClass = invokeClass,
                invokeFunction = func,
                cmdRequireAuthority = cmdRequireAuthority,
            };

            cmdHandlerDelegates[cmdHash] = invoker;

            if (logger.LogEnabled())
            {
                string requireAuthorityMessage = invokerType == MirrorInvokeType.ServerRpc ? $" RequireAuthority:{cmdRequireAuthority}" : "";
                logger.Log($"RegisterDelegate hash: {cmdHash} invokerType: {invokerType} method: {func.GetMethodName()}{requireAuthorityMessage}");
            }

            return cmdHash;
        }

        public static void RegisterRequestDelegate<T>(Type invokeClass, string cmdName, RequestDelegate<T> func, bool cmdRequireAuthority = true)
        {
            async UniTaskVoid Wrapper(NetworkBehaviour obj, NetworkReader reader, INetworkConnection senderConnection, int replyId)
            {
                /// invoke the serverRpc and send a reply message
                T result = await func(obj, reader, senderConnection, replyId);

                using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
                {
                    writer.Write(result);
                    var serverRpcReply = new ServerRpcReply
                    {
                        replyId = replyId,
                        payload = writer.ToArraySegment()
                    };

                    senderConnection.Send(serverRpcReply);
                }
            }

            void CmdWrapper(NetworkBehaviour obj, NetworkReader reader, INetworkConnection senderConnection, int replyId)
            {
                Wrapper(obj, reader, senderConnection, replyId).Forget();
            }

            RegisterDelegate(invokeClass, cmdName, MirrorInvokeType.ServerRpc, CmdWrapper, cmdRequireAuthority);
        }

        static bool CheckIfDelegateExists(Type invokeClass, MirrorInvokeType invokerType, CmdDelegate func, int cmdHash)
        {
            if (cmdHandlerDelegates.ContainsKey(cmdHash))
            {
                // something already registered this hash
                Skeleton oldInvoker = cmdHandlerDelegates[cmdHash];
                if (oldInvoker.AreEqual(invokeClass, invokerType, func))
                {
                    // it's all right,  it was the same function
                    return true;
                }

                logger.LogError($"Function {oldInvoker.invokeClass}.{oldInvoker.invokeFunction.GetMethodName()} and {invokeClass}.{func.GetMethodName()} have the same hash.  Please rename one of them");
            }

            return false;
        }

        public static void RegisterServerRpcDelegate(Type invokeClass, string cmdName, CmdDelegate func, bool requireAuthority)
        {
            RegisterDelegate(invokeClass, cmdName, MirrorInvokeType.ServerRpc, func, requireAuthority);
        }

        public static void RegisterRpcDelegate(Type invokeClass, string rpcName, CmdDelegate func)
        {
            RegisterDelegate(invokeClass, rpcName, MirrorInvokeType.ClientRpc, func);
        }

        /// <summary>
        /// We need this in order to clean up tests
        /// </summary>
        internal static void RemoveDelegate(int hash)
        {
            cmdHandlerDelegates.Remove(hash);
        }

        internal static Skeleton GetSkeleton(int cmdHash)
        {

            if (cmdHandlerDelegates.TryGetValue(cmdHash, out Skeleton invoker))
            {
                return invoker;
            }

            throw new MethodInvocationException($"No RPC method found for hash {cmdHash}");
        }

        /// <summary>
        /// Gets the handler function for a given hash
        /// Can be used by profilers and debuggers
        /// </summary>
        /// <param name="cmdHash">rpc function hash</param>
        /// <returns>The function delegate that will handle the ServerRpc</returns>
        public static CmdDelegate GetDelegate(int cmdHash)
        {
            if (cmdHandlerDelegates.TryGetValue(cmdHash, out Skeleton invoker))
            {
                return invoker.invokeFunction;
            }
            return null;
        }
    }
}

