using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.RemoteCalls
{
    /// <summary>
    /// Delegate for Command functions.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    public delegate void CmdDelegate(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection);

    class Invoker
    {
        public Type invokeClass;
        public MirrorInvokeType invokeType;
        public CmdDelegate invokeFunction;
        public bool cmdRequiresAuthority;

        public bool AreEqual(Type invokeClass, MirrorInvokeType invokeType, CmdDelegate invokeFunction)
        {
            return (this.invokeClass == invokeClass &&
                    this.invokeType == invokeType &&
                    this.invokeFunction == invokeFunction);
        }
    }

    public struct CommandInfo
    {
        public bool requiresAuthority;
    }

    /// <summary>
    /// Used to help manage remote calls for NetworkBehaviours
    /// </summary>
    public static class RemoteCallHelper
    {
        static readonly Dictionary<int, Invoker> cmdHandlerDelegates = new Dictionary<int, Invoker>();

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
        /// helper function register a Command/Rpc delegate
        /// </summary>
        /// <param name="invokeClass"></param>
        /// <param name="cmdName"></param>
        /// <param name="invokerType"></param>
        /// <param name="func"></param>
        /// <param name="cmdIgnoreAuthority"></param>
        /// <returns>remote function hash</returns>
        internal static int RegisterDelegate(Type invokeClass, string cmdName, MirrorInvokeType invokerType, CmdDelegate func, bool cmdRequiresAuthority = true)
        {
            // type+func so Inventory.RpcUse != Equipment.RpcUse
            int cmdHash = GetMethodHash(invokeClass, cmdName);

            if (CheckIfDeligateExists(invokeClass, invokerType, func, cmdHash))
                return cmdHash;

            Invoker invoker = new Invoker
            {
                invokeType = invokerType,
                invokeClass = invokeClass,
                invokeFunction = func,
                cmdRequiresAuthority = cmdRequiresAuthority,
            };

            cmdHandlerDelegates[cmdHash] = invoker;

            //string ingoreAuthorityMessage = invokerType == MirrorInvokeType.Command ? $" requiresAuthority:{cmdRequiresAuthority}" : "";
            //Debug.Log($"RegisterDelegate hash: {cmdHash} invokerType: {invokerType} method: {func.GetMethodName()}{ingoreAuthorityMessage}");

            return cmdHash;
        }

        static bool CheckIfDeligateExists(Type invokeClass, MirrorInvokeType invokerType, CmdDelegate func, int cmdHash)
        {
            if (cmdHandlerDelegates.ContainsKey(cmdHash))
            {
                // something already registered this hash
                Invoker oldInvoker = cmdHandlerDelegates[cmdHash];
                if (oldInvoker.AreEqual(invokeClass, invokerType, func))
                {
                    // it's all right,  it was the same function
                    return true;
                }

                Debug.LogError($"Function {oldInvoker.invokeClass}.{oldInvoker.invokeFunction.GetMethodName()} and {invokeClass}.{func.GetMethodName()} have the same hash.  Please rename one of them");
            }

            return false;
        }

        public static void RegisterCommandDelegate(Type invokeClass, string cmdName, CmdDelegate func, bool requiresAuthority)
        {
            RegisterDelegate(invokeClass, cmdName, MirrorInvokeType.Command, func, requiresAuthority);
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

        static bool GetInvokerForHash(int cmdHash, MirrorInvokeType invokeType, out Invoker invoker)
        {
            if (cmdHandlerDelegates.TryGetValue(cmdHash, out invoker) && invoker != null && invoker.invokeType == invokeType)
            {
                return true;
            }

            // debug message if not found, or null, or mismatched type
            // (no need to throw an error, an attacker might just be trying to
            //  call an cmd with an rpc's hash)
            // Debug.Log("GetInvokerForHash hash:" + cmdHash + " not found");

            return false;
        }

        // InvokeCmd/Rpc Delegate can all use the same function here
        internal static bool InvokeHandlerDelegate(int cmdHash, MirrorInvokeType invokeType, NetworkReader reader, NetworkBehaviour invokingType, NetworkConnectionToClient senderConnection = null)
        {
            if (GetInvokerForHash(cmdHash, invokeType, out Invoker invoker) && invoker.invokeClass.IsInstanceOfType(invokingType))
            {
                invoker.invokeFunction(invokingType, reader, senderConnection);

                return true;
            }
            return false;
        }

        internal static CommandInfo GetCommandInfo(int cmdHash, NetworkBehaviour invokingType)
        {
            if (GetInvokerForHash(cmdHash, MirrorInvokeType.Command, out Invoker invoker) && invoker.invokeClass.IsInstanceOfType(invokingType))
            {
                return new CommandInfo
                {
                    requiresAuthority = invoker.cmdRequiresAuthority
                };
            }
            return default;
        }

        /// <summary>
        /// Gets the handler function for a given hash
        /// Can be used by profilers and debuggers
        /// </summary>
        /// <param name="cmdHash">rpc function hash</param>
        /// <returns>The function delegate that will handle the command</returns>
        public static CmdDelegate GetDelegate(int cmdHash)
        {
            if (cmdHandlerDelegates.TryGetValue(cmdHash, out Invoker invoker))
            {
                return invoker.invokeFunction;
            }
            return null;
        }
    }
}

