using System;
using System.ComponentModel;
using UnityEngine;

namespace Mirror
{
    [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use NetworkBehaviour.syncInterval field instead. Can be modified in the Inspector too.")]
    [AttributeUsage(AttributeTargets.Class)]
    public class NetworkSettingsAttribute : Attribute
    {
        public float sendInterval = 0.1f;
    }

    ///<summary>Automatically syncs a value from the server to clients.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class SyncVarAttribute : Attribute
    {
        ///<summary>A function that should be called on the client when the value changes.</summary>
        public string hook;
    }

    ///<summary>Command functions must start with 'Cmd'. Command functions can be called on clients to invoke code on the server.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CommandAttribute : Attribute
    {
        public int channel = Channels.DefaultReliable; // this is zero
    }

    ///<summary>ClientRpc functions must start with 'Rpc'. ClientRpc functions can be called on the server to invoke code on clients.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientRpcAttribute : Attribute
    {
        public int channel = Channels.DefaultReliable; // this is zero
    }

    ///<summary><para>TargetRpc functions must start with 'Target' and needs a NetworkConnection object as the first argument.</para>
    ///TargetRpc functions can be called on the server to invoke code on a specific client.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class TargetRpcAttribute : Attribute
    {
        public int channel = Channels.DefaultReliable; // this is zero
    }

    ///<summary>SyncEvent events must start with 'Event'. SyncEvent events can be invoked on the server and they will automatically be invoked on all clients.</summary>
    [AttributeUsage(AttributeTargets.Event)]
    public class SyncEventAttribute : Attribute
    {
        public int channel = Channels.DefaultReliable; // this is zero
    }

    ///<summary>Only allows code to be run on the server, and generates a warning if a client tries to run it.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ServerAttribute : Attribute {}

    ///<summary>Only allows code to be run on the server, and does not generate a warning if a client tries to run it.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ServerCallbackAttribute : Attribute {}

    ///<summary>Only allows code to be run on clients, and generates a warning if the server tries to run it.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientAttribute : Attribute {}

    ///<summary>Only allows code to be run on clients, and does not generate a warning if the server tries to run it.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientCallbackAttribute : Attribute {}

    // For Scene property Drawer
    public class SceneAttribute : PropertyAttribute {}

    [AttributeUsage(AttributeTargets.Method)]
    public class NetworkWriterAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class NetworkReaderAttribute : Attribute { }
}
