using System;

#if ENABLE_UNET

namespace UnityEngine.Networking
{
    [AttributeUsage(AttributeTargets.Class)]
    public class NetworkSettingsAttribute : Attribute
    {
        public int channel = Channels.DefaultReliable;
        public float sendInterval = 0.1f;
    }

    // SyncTarget enum is cleaner than 'bool onlyToOwner and allows for more options if needed
    public enum SyncTarget {Observers, Owner};

    [AttributeUsage(AttributeTargets.Field)]
    public class SyncVarAttribute : Attribute
    {
        public string hook;
        public SyncTarget target = SyncTarget.Observers; // for 'only sync to owner' support
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
        public int channel = Channels.DefaultReliable;  // this is zero
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ServerAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ServerCallbackAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ClientAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ClientCallbackAttribute : Attribute
    {
    }
}
#endif //ENABLE_UNET
