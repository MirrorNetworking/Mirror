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

    [AttributeUsage(AttributeTargets.Field)]
    public class SyncVarAttribute : Attribute
    {
        public string hook;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class CommandAttribute : Attribute
    {
        public int channel = Channels.DefaultReliable; // this is zero
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ClientRpcAttribute : Attribute
    {
        public int channel = Channels.DefaultReliable; // this is zero
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class TargetRpcAttribute : Attribute
    {
        public int channel = Channels.DefaultReliable; // this is zero
    }

    [AttributeUsage(AttributeTargets.Event)]
    public class SyncEventAttribute : Attribute
    {
        public int channel = Channels.DefaultReliable; // this is zero
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ServerAttribute : Attribute {}

    [AttributeUsage(AttributeTargets.Method)]
    public class ServerCallbackAttribute : Attribute {}

    [AttributeUsage(AttributeTargets.Method)]
    public class ClientAttribute : Attribute {}

    [AttributeUsage(AttributeTargets.Method)]
    public class ClientCallbackAttribute : Attribute {}

    // For Scene property Drawer
    public class SceneAttribute : PropertyAttribute {}
}
