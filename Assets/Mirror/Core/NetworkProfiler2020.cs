// [Obsolete] path for our old Network Profiler because the new one doesn't work with Unity 2020.
// we can't change NetworkProfiler, but we can restore the old API with [Obsolete] paths.
using System;
using System.Collections.Generic;
using Mirror.RemoteCalls;

namespace Mirror
{
    static class NetworkProfiler2020
    {
        public const string ObsoleteMessage = "Obsolete API is only kept for old Network Profiler and Unity 2020 support.";
    }

    // OBSOLETE PATH FOR OLD NETWORK PROFILER IN UNITY 2020
    [Obsolete(NetworkProfiler2020.ObsoleteMessage)]
    public interface IMessageBase{}

    [Obsolete(NetworkProfiler2020.ObsoleteMessage)]
    public struct SyncEventMessage : NetworkMessage
    {
        public ushort functionHash;
        public uint netId;
    }

    [Obsolete(NetworkProfiler2020.ObsoleteMessage)]
    public struct UpdateVarsMessage : NetworkMessage
    {
        public uint netId;
    }

    [Obsolete(NetworkProfiler2020.ObsoleteMessage)]
    public abstract partial class NetworkBehaviour
    {
        public static RemoteCallDelegate GetRpcHandler(int functionHash) =>
            RemoteProcedureCalls.GetDelegate((ushort)functionHash);
    }

    [Obsolete(NetworkProfiler2020.ObsoleteMessage)]
    public sealed partial class NetworkIdentity
    {
        public static Dictionary<uint, NetworkIdentity> spawned
        {
            get
            {
                // server / host mode: use the one from server.
                // host mode has access to all spawned.
                if (NetworkServer.active)
                    return NetworkServer.spawned;

                // client
                if (NetworkClient.active)
                    return NetworkClient.spawned;

                return null;
            }
        }
    }
}
