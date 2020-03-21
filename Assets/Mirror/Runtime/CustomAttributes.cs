using System;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// SyncVars are used to synchronize a variable from the server to all clients automatically.
    /// <para>Value must be changed on server, not directly by clients.  Hook parameter allows you to define a client-side method to be invoked when the client gets an update from the server.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class SyncVarAttribute : PropertyAttribute
    {
        ///<summary>A function that should be called on the client when the value changes.</summary>
        public string hook;
    }

    /// <summary>
    /// Call this from a client to run this function on the server.
    /// <para>Make sure to validate input etc. It's not possible to call this from a server.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CommandAttribute : Attribute
    {
        // this is zero
        public int channel = Channels.DefaultReliable;
    }

    /// <summary>
    /// The server uses a Remote Procedure Call (RPC) to run this function on clients.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientRpcAttribute : Attribute
    {
        // this is zero
        public int channel = Channels.DefaultReliable;
    }

    /// <summary>
    /// The server uses a Remote Procedure Call (RPC) to run this function on a specific client.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class TargetRpcAttribute : Attribute
    {
        // this is zero
        public int channel = Channels.DefaultReliable;
    }

    /// <summary>
    /// SyncEvents are networked events like ClientRpc's, but instead of calling a function on the game object, they trigger Events instead.
    /// </summary>
    [AttributeUsage(AttributeTargets.Event)]
    public class SyncEventAttribute : Attribute
    {
        // this is zero
        public int channel = Channels.DefaultReliable;
    }

    /// <summary>
    /// Prevents clients from running this method.
    /// <para>Prints a warning if a client tries to execute this method.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ServerAttribute : Attribute { }

    /// <summary>
    /// Prevents clients from running this method.
    /// <para>No warning is thrown.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ServerCallbackAttribute : Attribute { }

    /// <summary>
    /// Prevents the server from running this method.
    /// <para>Prints a warning if the server tries to execute this method.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientAttribute : Attribute { }

    /// <summary>
    /// Prevents the server from running this method.
    /// <para>No warning is printed.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientCallbackAttribute : Attribute { }

    /// <summary>
    /// Converts a string property into a Scene property in the inspector
    /// </summary>
    public class SceneAttribute : PropertyAttribute { }

    /// <summary>
    /// Used to show private SyncList in the inspector,
    /// <para> Use instead of SerializeField for non Serializable types </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ShowInInspectorAttribute : Attribute { }
}
