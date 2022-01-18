using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.RemoteCalls
{
    // invoke type for Cmd/Rpc
    public enum RemoteCallType { Command, ClientRpc }

    // remote call function delegate
    public delegate void RemoteCallDelegate(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection);

    class Invoker
    {
        public Type invokeClass;
        public RemoteCallType remoteCallType;
        public RemoteCallDelegate invokeFunction;
        public bool cmdRequiresAuthority;

        public bool AreEqual(Type invokeClass, RemoteCallType remoteCallType, RemoteCallDelegate invokeFunction)
        {
            return (this.invokeClass == invokeClass &&
                    this.remoteCallType == remoteCallType &&
                    this.invokeFunction == invokeFunction);
        }
    }

    public struct CommandInfo
    {
        public bool requiresAuthority;
    }

    /// <summary>Used to help manage remote calls for NetworkBehaviours</summary>
    public static class RemoteProcedureCalls
    {
        // note: do not clear those with [RuntimeInitializeOnLoad]
        static readonly Dictionary<int, Invoker> remoteCallDelegates = new Dictionary<int, Invoker>();

        // pass full function name to avoid ClassA.Func & ClassB.Func collisions
        internal static int RegisterDelegate(Type invokeClass, string functionFullName, RemoteCallType remoteCallType, RemoteCallDelegate func, bool cmdRequiresAuthority = true)
        {
            // type+func so Inventory.RpcUse != Equipment.RpcUse
            int hash = functionFullName.GetStableHashCode();

            if (CheckIfDelegateExists(invokeClass, remoteCallType, func, hash))
                return hash;

            Invoker invoker = new Invoker
            {
                remoteCallType = remoteCallType,
                invokeClass = invokeClass,
                invokeFunction = func,
                cmdRequiresAuthority = cmdRequiresAuthority,
            };

            remoteCallDelegates[hash] = invoker;

            //string ingoreAuthorityMessage = invokerType == MirrorInvokeType.Command ? $" requiresAuthority:{cmdRequiresAuthority}" : "";
            //Debug.Log($"RegisterDelegate hash: {hash} invokerType: {invokerType} method: {func.GetMethodName()}{ingoreAuthorityMessage}");

            return hash;
        }

        static bool CheckIfDelegateExists(Type invokeClass, RemoteCallType remoteCallType, RemoteCallDelegate func, int cmdHash)
        {
            if (remoteCallDelegates.ContainsKey(cmdHash))
            {
                // something already registered this hash
                Invoker oldInvoker = remoteCallDelegates[cmdHash];
                if (oldInvoker.AreEqual(invokeClass, remoteCallType, func))
                {
                    // it's all right,  it was the same function
                    return true;
                }

                Debug.LogError($"Function {oldInvoker.invokeClass}.{oldInvoker.invokeFunction.GetMethodName()} and {invokeClass}.{func.GetMethodName()} have the same hash.  Please rename one of them");
            }

            return false;
        }

        // pass full function name to avoid ClassA.Func <-> ClassB.Func collisions
        public static void RegisterCommandDelegate(Type invokeClass, string functionFullName, RemoteCallDelegate func, bool requiresAuthority)
        {
            RegisterDelegate(invokeClass, functionFullName, RemoteCallType.Command, func, requiresAuthority);
        }

        // pass full function name to avoid ClassA.Func <-> ClassB.Func collisions
        public static void RegisterRpcDelegate(Type invokeClass, string functionFullName, RemoteCallDelegate func)
        {
            RegisterDelegate(invokeClass, functionFullName, RemoteCallType.ClientRpc, func);
        }

        //  We need this in order to clean up tests
        internal static void RemoveDelegate(int hash)
        {
            remoteCallDelegates.Remove(hash);
        }

        static bool GetInvokerForHash(int cmdHash, RemoteCallType remoteCallType, out Invoker invoker)
        {
            if (remoteCallDelegates.TryGetValue(cmdHash, out invoker) && invoker != null && invoker.remoteCallType == remoteCallType)
            {
                return true;
            }

            // debug message if not found, or null, or mismatched type
            // (no need to throw an error, an attacker might just be trying to
            //  call an cmd with an rpc's hash)
            // Debug.Log($"GetInvokerForHash hash {cmdHash} not found");
            return false;
        }

        // InvokeCmd/Rpc Delegate can all use the same function here
        internal static bool InvokeHandlerDelegate(int cmdHash, RemoteCallType remoteCallType, NetworkReader reader, NetworkBehaviour invokingType, NetworkConnectionToClient senderConnection = null)
        {
            if (GetInvokerForHash(cmdHash, remoteCallType, out Invoker invoker) && invoker.invokeClass.IsInstanceOfType(invokingType))
            {
                invoker.invokeFunction(invokingType, reader, senderConnection);
                return true;
            }
            return false;
        }

        internal static CommandInfo GetCommandInfo(int cmdHash, NetworkBehaviour invokingType)
        {
            if (GetInvokerForHash(cmdHash, RemoteCallType.Command, out Invoker invoker) && invoker.invokeClass.IsInstanceOfType(invokingType))
            {
                return new CommandInfo
                {
                    requiresAuthority = invoker.cmdRequiresAuthority
                };
            }
            return default;
        }

        /// <summary>Gets the handler function by hash. Useful for profilers and debuggers.</summary>
        public static RemoteCallDelegate GetDelegate(int cmdHash)
        {
            if (remoteCallDelegates.TryGetValue(cmdHash, out Invoker invoker))
            {
                return invoker.invokeFunction;
            }
            return null;
        }
    }
}

