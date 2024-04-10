using System;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// SyncVars are used to automatically synchronize a variable between the server and all clients. The direction of synchronization depends on the Sync Direction property, ServerToClient by default.
    /// <para>
    /// When Sync Direction is equal to ServerToClient, the value should be changed on the server side and synchronized to all clients.
    /// Otherwise, the value should be changed on the client side and synchronized to server and other clients.
    /// </para>
    /// <para>Hook parameter allows you to define a method to be invoked when gets an value update. Notice that the hook method will not be called on the change side.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class SyncVarAttribute : PropertyAttribute
    {
        public string hook;
    }

    /// <summary>
    /// Call this from a client to run this function on the server.
    /// <para>Make sure to validate input etc. It's not possible to call this from a server.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CommandAttribute : Attribute
    {
        public int channel = Channels.Reliable;
        public bool requiresAuthority = true;
    }

    /// <summary>
    /// The server uses a Remote Procedure Call (RPC) to run this function on clients.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientRpcAttribute : Attribute
    {
        public int channel = Channels.Reliable;
        public bool includeOwner = true;
    }

    /// <summary>
    /// The server uses a Remote Procedure Call (RPC) to run this function on a specific client.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class TargetRpcAttribute : Attribute
    {
        public int channel = Channels.Reliable;
    }

    /// <summary>
    /// Prevents clients from running this method.
    /// <para>Prints a warning if a client tries to execute this method.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ServerAttribute : Attribute {}

    /// <summary>
    /// Prevents clients from running this method.
    /// <para>No warning is thrown.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ServerCallbackAttribute : Attribute {}

    /// <summary>
    /// Prevents the server from running this method.
    /// <para>Prints a warning if the server tries to execute this method.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientAttribute : Attribute {}

    /// <summary>
    /// Prevents the server from running this method.
    /// <para>No warning is printed.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientCallbackAttribute : Attribute {}

    /// <summary>
    /// Converts a string property into a Scene property in the inspector
    /// </summary>
    public class SceneAttribute : PropertyAttribute {}

    /// <summary>
    /// Used to show private SyncList in the inspector,
    /// <para> Use instead of SerializeField for non Serializable types </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ShowInInspectorAttribute : Attribute {}

    /// <summary>
    /// Used to make a field readonly in the inspector
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ReadOnlyAttribute : PropertyAttribute {}
}
