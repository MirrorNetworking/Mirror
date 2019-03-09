using System;
using UnityEngine;

namespace Mirror
{
    // Handles network messages on client and server
    public delegate void NetworkMessageDelegate(NetworkMessage netMsg);

    // Handles requests to spawn objects on the client
    public delegate GameObject SpawnDelegate(Vector3 position, Guid assetId);

    // Handles requests to unspawn objects on the client
    public delegate void UnSpawnDelegate(GameObject spawned);

    // invoke type for Cmd/Rpc/SyncEvents
    public enum MirrorInvokeType
    {
        Command,
        ClientRpc,
        SyncEvent
    }

    // built-in system network messages
    // original HLAPI uses short, so let's keep short to not break packet header etc.
    // => use .ToString() to get the field name from the field value
    // => we specify the short values so it's easier to look up opcodes when debugging packets
    [Obsolete("Use Send<T>  with no message id instead")]
    public enum MsgType : short
    {
        // internal system messages - cannot be replaced by user code
        ObjectDestroy = 1,
        Rpc = 2,
        Owner = 4,
        Command = 5,
        SyncEvent = 7,
        UpdateVars = 8,
        SpawnPrefab = 3,
        SpawnSceneObject = 10,
        SpawnStarted = 11,
        SpawnFinished = 12,
        ObjectHide = 13,
        LocalClientAuthority = 15,

        // public system messages - can be replaced by user code
        Connect = 32,
        Disconnect = 33,
        Error = 34,
        Ready = 35,
        NotReady = 36,
        AddPlayer = 37,
        RemovePlayer = 38,
        Scene = 39,

        // time synchronization
        Ping = 43,
        Pong = 44,

        Highest = 47
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
