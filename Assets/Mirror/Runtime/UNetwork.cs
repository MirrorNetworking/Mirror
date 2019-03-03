using System;
using UnityEngine;

namespace Mirror
{
    // Handles network messages on client and server
    public delegate void NetworkMessageDelegate(NetworkConnection conn, NetworkReader reader);

    // Handles requests to spawn objects on the client
    public delegate GameObject SpawnDelegate(Vector3 position, Guid assetId);

    // Handles requests to unspawn objects on the client
    public delegate void UnSpawnDelegate(GameObject spawned);

    // invoke type for Cmd/Rpc/SyncEvents
    public enum UNetInvokeType
    {
        Command,
        ClientRpc,
        SyncEvent
    }

    public enum Version
    {
        Current = 1
    }

    public static class Channels
    {
        public const int DefaultReliable = 0;
        public const int DefaultUnreliable = 1;
    }
}
