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

    [Flags] public enum SpawnFlags : byte
    {
        None          = 0,
        isOwner       = 1 << 0,
        isLocalPlayer = 1 << 1
    }

    public struct SpawnMessage : NetworkMessage
    {
        // netId of new or existing object
        public uint netId;
        // isOwner and isLocalPlayer are merged into one byte via bitwise op
        public SpawnFlags spawnFlags;
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

        // Backwards compatibility after implementing spawnFlags
        public bool isOwner
        {
            get => spawnFlags.HasFlag(SpawnFlags.isOwner);
            set => spawnFlags = 
                value 
                ? spawnFlags | SpawnFlags.isOwner 
                : spawnFlags & ~SpawnFlags.isOwner;
        }

        // Backwards compatibility after implementing spawnFlags
        public bool isLocalPlayer
        {
            get => spawnFlags.HasFlag(SpawnFlags.isLocalPlayer);
            set => spawnFlags = 
                value 
                ? spawnFlags | SpawnFlags.isLocalPlayer 
                : spawnFlags & ~SpawnFlags.isLocalPlayer;
        }
    }

    public struct ChangeOwnerMessage : NetworkMessage
    {
        public uint netId;
        // isOwner and isLocalPlayer are merged into one byte via bitwise op
        public SpawnFlags spawnFlags;

        // Backwards compatibility after implementing spawnFlags
        public bool isOwner
        {
            get => spawnFlags.HasFlag(SpawnFlags.isOwner);
            set => spawnFlags = 
                value 
                ? spawnFlags | SpawnFlags.isOwner 
                : spawnFlags & ~SpawnFlags.isOwner;
        }

        // Backwards compatibility after implementing spawnFlags
        public bool isLocalPlayer
        {
            get => spawnFlags.HasFlag(SpawnFlags.isLocalPlayer);
            set => spawnFlags = 
                value 
                ? spawnFlags | SpawnFlags.isLocalPlayer 
                : spawnFlags & ~SpawnFlags.isLocalPlayer;
        }
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

    // state update for reliable sync
    public struct EntityStateMessage : NetworkMessage
    {
        public uint netId;
        // the serialized component data
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;
    }

    // state update for unreliable sync.
    // baseline is always sent over Reliable channel.
    public struct EntityStateMessageUnreliableBaseline : NetworkMessage
    {
        // baseline messages send their tick number as byte.
        // delta messages are checked against that tick to avoid applying a
        // delta on top of the wrong baseline.
        // (byte is enough, we just need something small to compare against)
        public byte baselineTick;

        public uint netId;
        // the serialized component data
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;
    }

    // state update for unreliable sync
    // delta is always sent over Unreliable channel.
    public struct EntityStateMessageUnreliableDelta : NetworkMessage
    {
        // baseline messages send their tick number as byte.
        // delta messages are checked against that tick to avoid applying a
        // delta on top of the wrong baseline.
        // (byte is enough, we just need something small to compare against)
        public byte baselineTick;

        public uint netId;
        // the serialized component data
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;
    }

    // whoever wants to measure rtt, sends this to the other end.
    public struct NetworkPingMessage : NetworkMessage
    {
        // local time is used to calculate round trip time,
        // and to calculate the predicted time offset.
        public double localTime;

        // predicted time is sent to compare the final error, for debugging only
        public double predictedTimeAdjusted;

        public NetworkPingMessage(double localTime, double predictedTimeAdjusted)
        {
            this.localTime = localTime;
            this.predictedTimeAdjusted = predictedTimeAdjusted;
        }
    }

    // the other end responds with this message.
    // we can use this to calculate rtt.
    public struct NetworkPongMessage : NetworkMessage
    {
        // local time is used to calculate round trip time.
        public double localTime;

        // predicted error is used to adjust the predicted timeline.
        public double predictionErrorUnadjusted;
        public double predictionErrorAdjusted; // for debug purposes

        public NetworkPongMessage(double localTime, double predictionErrorUnadjusted, double predictionErrorAdjusted)
        {
            this.localTime = localTime;
            this.predictionErrorUnadjusted = predictionErrorUnadjusted;
            this.predictionErrorAdjusted = predictionErrorAdjusted;
        }
    }
}
