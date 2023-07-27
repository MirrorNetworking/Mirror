using System;
using UnityEngine;

namespace Mirror
{
    // need to send time every sendInterval.
    // batching automatically includes remoteTimestamp.
    // all we need to do is ensure that an empty message is sent.
    // and react to it.
    // => we don't want to insert a snapshot on every batch.
    // => do it exactly every sendInterval on every TimeSnapshotMessage.
    public struct TimeSnapshotMessage : NetworkMessage {}

    public struct ReadyMessage : NetworkMessage {}

    public struct NotReadyMessage : NetworkMessage {}

    public struct AddPlayerMessage : NetworkMessage {}

    public struct SceneMessage : NetworkMessage
    {
        public string sceneName;
        // Normal = 0, LoadAdditive = 1, UnloadAdditive = 2
        public SceneOperation sceneOperation;
        public bool customHandling;
    }

    public enum SceneOperation : byte
    {
        Normal,
        LoadAdditive,
        UnloadAdditive
    }

    public struct CommandMessage : NetworkMessage
    {
        public uint netId;
        public byte componentIndex;
        public ushort functionHash;
        // the parameters for the Cmd function
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;
    }

    public struct RpcMessage : NetworkMessage
    {
        public uint netId;
        public byte componentIndex;
        public ushort functionHash;
        // the parameters for the Cmd function
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;
    }

    public struct SpawnMessage : NetworkMessage
    {
        // netId of new or existing object
        public uint netId;
        public bool isLocalPlayer;
        // Sets hasAuthority on the spawned object
        public bool isOwner;
        public ulong sceneId;
        // If sceneId != 0 then it is used instead of assetId
        public uint assetId;
        // Local position
        public Vector3 position;
        // Local rotation
        public Quaternion rotation;
        // Local scale
        public Vector3 scale;
        // serialized component data
        // ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;
    }

    public struct ChangeOwnerMessage : NetworkMessage
    {
        public uint netId;
        public bool isOwner;
        public bool isLocalPlayer;
    }

    public struct ObjectSpawnStartedMessage : NetworkMessage {}

    public struct ObjectSpawnFinishedMessage : NetworkMessage {}

    public struct ObjectDestroyMessage : NetworkMessage
    {
        public uint netId;
    }

    public struct ObjectHideMessage : NetworkMessage
    {
        public uint netId;
    }

    public struct EntityStateMessage : NetworkMessage
    {
        public uint netId;
        // the serialized component data
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;
    }

    // whoever wants to measure rtt, sends this to the other end.
    public struct NetworkPingMessage : NetworkMessage
    {
        public double localTime;

        public NetworkPingMessage(double value)
        {
            localTime = value;
        }
    }

    // the other end responds with this message.
    // we can use this to calculate rtt.
    public struct NetworkPongMessage : NetworkMessage
    {
        public double localTime;
    }
}
