using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror.RemoteCalls
{
    // invoke type for Cmd/Rpc
    public enum RemoteCallType { Command, ClientRpc }

    // remote call function delegate
    public delegate void RemoteCallDelegate(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection);

    class Invoker
    {
        // GameObjects might have multiple components of TypeA.CommandA().
        // when invoking, we check if 'TypeA' is an instance of the type.
        // the hash itself isn't enough because we wouldn't know which component
        // to invoke it on if there are multiple of the same type.
        public Type componentType;
        public RemoteCallType callType;
        public RemoteCallDelegate function;
        public bool cmdRequiresAuthority;

        public bool AreEqual(Type componentType, RemoteCallType remoteCallType, RemoteCallDelegate invokeFunction) =>
            this.componentType == componentType &&
            this.callType == remoteCallType &&
            this.function == invokeFunction;
    }

    /// <summary>Used to help manage remote calls for NetworkBehaviours</summary>
    public static class RemoteProcedureCalls
    {
        // sending rpc/cmd function hash would require 4 bytes each time.
        // instead, let's only send the index to save bandwidth.
        // => 1 byte index with 255 rpcs in total would not be enough.
        // => 1 byte index with 255 rpcs per type is doable but lookup is hard,
        //    because an rpc might be in the actual type or in the base type etc
        // => 2 byte index allows for 64k Rpcs and is very easy to implement
        //    with a SortedList + .IndexOfKey.
        //
        // NOTE: this could be 1 byte most of the time via VarInt!
        //       but requires custom serialization for Command/RpcMessages.
        //
        // SortedList still doesn't allow duplicate keys, which is good.
        // But it allows accessing keys by index.
        static readonly SortedList<int, Invoker> remoteCallDelegates = new SortedList<int, Invoker>();

        // hash -> index reverse lookup to cache .IndexOfKey() binary search.
        static readonly Dictionary<int, ushort> remoteCallIndexLookup = new Dictionary<int, ushort>();

        // helper function to get rpc/cmd index from function name / hash.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort GetIndexFromFunctionHash(string functionFullName)
        {
            int hash = functionFullName.GetStableHashCode();

            // IndexOfKey runs a binary search.
            // cache results in lookup.
            // IMPORTANT: can't cache results when registering rpcs/cmds as the
            //            indices would only be valid after ALL were registered.
            // return (ushort)remoteCallDelegates.IndexOfKey(hash);

            // reuse cached index if possible
            if (remoteCallIndexLookup.TryGetValue(hash, out ushort index))
                return index;

            // otherwise search and cache
            ushort searchedIndex = (ushort)remoteCallDelegates.IndexOfKey(hash);
            remoteCallIndexLookup[hash] = searchedIndex;
            return searchedIndex;
        }

        static bool CheckIfDelegateExists(Type componentType, RemoteCallType remoteCallType, RemoteCallDelegate func, int functionHash)
        {
            if (remoteCallDelegates.ContainsKey(functionHash))
            {
                // something already registered this hash.
                // it's okay if it was the same function.
                Invoker oldInvoker = remoteCallDelegates[functionHash];
                if (oldInvoker.AreEqual(componentType, remoteCallType, func))
                {
                    return true;
                }

                // otherwise notify user. there is a rare chance of string
                // hash collisions.
                Debug.LogError($"Function {oldInvoker.componentType}.{oldInvoker.function.GetMethodName()} and {componentType}.{func.GetMethodName()} have the same hash.  Please rename one of them");
            }

            return false;
        }

        // pass full function name to avoid ClassA.Func & ClassB.Func collisions
        internal static int RegisterDelegate(Type componentType, string functionFullName, RemoteCallType remoteCallType, RemoteCallDelegate func, bool cmdRequiresAuthority = true)
        {
            // type+func so Inventory.RpcUse != Equipment.RpcUse
            int hash = functionFullName.GetStableHashCode();

            if (CheckIfDelegateExists(componentType, remoteCallType, func, hash))
                return hash;

            // register invoker by hash
            remoteCallDelegates[hash] = new Invoker
            {
                callType = remoteCallType,
                componentType = componentType,
                function = func,
                cmdRequiresAuthority = cmdRequiresAuthority
            };
            return hash;
        }

        // pass full function name to avoid ClassA.Func <-> ClassB.Func collisions
        // need to pass componentType to support invoking on GameObjects with
        // multiple components of same type with same remote call.
        public static void RegisterCommand(Type componentType, string functionFullName, RemoteCallDelegate func, bool requiresAuthority) =>
            RegisterDelegate(componentType, functionFullName, RemoteCallType.Command, func, requiresAuthority);

        // pass full function name to avoid ClassA.Func <-> ClassB.Func collisions
        // need to pass componentType to support invoking on GameObjects with
        // multiple components of same type with same remote call.
        public static void RegisterRpc(Type componentType, string functionFullName, RemoteCallDelegate func) =>
            RegisterDelegate(componentType, functionFullName, RemoteCallType.ClientRpc, func);

        // to clean up tests
        internal static void RemoveDelegate(int hash) =>
            remoteCallDelegates.Remove(hash);

        // note: no need to throw an error if not found.
        // an attacker might just try to call a cmd with an rpc's hash etc.
        // returning false is enough.
        static bool GetInvoker(ushort functionIndex, RemoteCallType remoteCallType, out Invoker invoker)
        {
            // valid index?
            if (functionIndex <= remoteCallDelegates.Count)
            {
                // get key by index
                int functionHash = remoteCallDelegates.Keys[functionIndex];
                invoker = remoteCallDelegates[functionHash];
                // check rpc type. don't allow calling cmds from rpcs, etc.
                return invoker != null &&
                       invoker.callType == remoteCallType;
            }
            invoker = null;
            return false;
        }

        // InvokeCmd/Rpc Delegate can all use the same function here
        // => invoke by index to save bandwidth (2 bytes instead of 4 bytes)
        internal static bool Invoke(ushort functionIndex, RemoteCallType remoteCallType, NetworkReader reader, NetworkBehaviour component, NetworkConnectionToClient senderConnection = null)
        {
            // IMPORTANT: we check if the message's componentIndex component is
            //            actually of the right type. prevents attackers trying
            //            to invoke remote calls on wrong components.
            if (GetInvoker(functionIndex, remoteCallType, out Invoker invoker) &&
                invoker.componentType.IsInstanceOfType(component))
            {
                // invoke function on this component
                invoker.function(component, reader, senderConnection);
                return true;
            }
            return false;
        }

        // check if the command 'requiresAuthority' which is set in the attribute
        internal static bool CommandRequiresAuthority(ushort cmdIndex) =>
            GetInvoker(cmdIndex, RemoteCallType.Command, out Invoker invoker) &&
            invoker.cmdRequiresAuthority;

        /// <summary>Gets the handler function by hash. Useful for profilers and debuggers.</summary>
        public static RemoteCallDelegate GetDelegate(int functionHash) =>
            remoteCallDelegates.TryGetValue(functionHash, out Invoker invoker)
            ? invoker.function
            : null;

        // RuntimeInitializeOnLoadMethod -> fast playmode without domain reload
        [RuntimeInitializeOnLoadMethod]
        internal static void ResetStatics()
        {
            // clear rpc lookup every time.
            // otherwise tests may have issues.
            remoteCallIndexLookup.Clear();
        }
    }
}
