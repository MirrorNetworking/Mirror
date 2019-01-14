using System;
using UnityEngine;

namespace Mirror
{
    [Obsolete("Use NetworkBehaviour.syncInterval field instead. Can be modified in the Inspector too.")]
    [AttributeUsage(AttributeTargets.Class)]
    public class NetworkSettingsAttribute : Attribute
    {
        public float sendInterval = 0.1f;
    }

    /// <summary>
    /// Automatically syncs a value from the server to clients.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class SyncVarAttribute : Attribute
    {
        ///<summary>A function that should be called when the value changes on the client.</summary>
        public string hook;
    }

    /// <summary>
    /// Command functions must start with 'Cmd' and they allow clients to send a command to the server to invoke a method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CommandAttribute : Attribute
    {
        public int channel = Channels.DefaultReliable; // this is zero
    }

    /// <summary>
    /// ClientRpc functions must start with 'Rpc' and they allow methods to be invoked on clients from the server.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientRpcAttribute : Attribute
    {
        public int channel = Channels.DefaultReliable; // this is zero
    }

    /// <summary>
    /// TargetRpc functions must start with 'Target' and they allow methods to be invoked on a specific client from the server.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class TargetRpcAttribute : Attribute
    {
        public int channel = Channels.DefaultReliable; // this is zero
    }

    /// <summary>
    /// SyncEvent events must start with 'Event' and they allow events to be invoked on a client from the server.
    /// </summary>
    [AttributeUsage(AttributeTargets.Event)]
    public class SyncEventAttribute : Attribute
    {
        public int channel = Channels.DefaultReliable; // this is zero
    }

    /// <summary>
    /// Only allows code to be run on the server, and generates a warning if a client tries to run it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ServerAttribute : Attribute
    {
    }

    /// <summary>
    /// Only allows code to be run on the server, and does not generate a warning if a client tries to run it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ServerCallbackAttribute : Attribute
    {
    }

    /// <summary>
    /// Only allows code to be run on clients, and generates a warning if the server tries to run it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientAttribute : Attribute
    {
    }

    /// <summary>
    /// Only allows code to be run on clients, and does not generate a warning if the server tries to run it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientCallbackAttribute : Attribute
    {
    }
}
