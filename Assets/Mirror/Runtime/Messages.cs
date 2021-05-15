using System;
using UnityEngine;

namespace Mirror
{
    // Deprecated 10/06/2020
    [Obsolete("Implement NetworkMessage instead. Use extension methods instead of Serialize/Deserialize, see https://github.com/vis2k/Mirror/pull/2317", true)]
    public interface IMessageBase {}

    // Deprecated 10/06/2020
    [Obsolete("Implement NetworkMessage instead. Use extension methods instead of Serialize/Deserialize, see https://github.com/vis2k/Mirror/pull/2317", true)]
    public class MessageBase : IMessageBase {}

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
        public int componentIndex;
        public int functionHash;
        // the parameters for the Cmd function
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;
    }

    public struct RpcMessage : NetworkMessage
    {
        public uint netId;
        public int componentIndex;
        public int functionHash;
        // the parameters for the Cmd function
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;
    }

    public struct ObjectHideMessage : NetworkMessage
    {
        public uint netId;
    }

    // one entry of PartialWorldState
    // serialized/deserialize to/from PartialWorldState entitiesPayload
    public struct PartialWorldStateEntity
    {
        // netId of new or existing object
        public uint netId;

        // Local position
        public Vector3 position;
        // Local rotation
        public Quaternion rotation;
        // Local scale
        public Vector3 scale;

        // for spawning
        public bool isLocalPlayer;
        // Sets hasAuthority on the spawned object
        public bool isOwner;
        public ulong sceneId;
        // If sceneId != 0 then it is used instead of assetId
        public Guid assetId;

        // serialized component data
        // ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;

        public PartialWorldStateEntity(uint netId, Vector3 position, Quaternion rotation, Vector3 scale, bool isLocalPlayer, bool isOwner, ulong sceneId, Guid assetId, ArraySegment<byte> payload)
        {
            this.netId = netId;
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
            this.isLocalPlayer = isLocalPlayer;
            this.isOwner = isOwner;
            this.sceneId = sceneId;
            this.assetId = assetId;
            this.payload = payload;
        }

        // we need to know bytes needed to serialize this one
        // TODO have a test to guarantee serialize.bytes == bytesneeded
        public int TotalSize() =>
            4 + // netId
            12 + // Vector3
            16 + // Quaternion
            12 + // Vector3
            1 + // bool
            1 + // bool
            1 + // idType
            (sceneId != 0 ? 4 : 16) + // sceneId || assetId
            4 + payload.Count; // payload size, payload

        public void Serialize(NetworkWriter writer)
        {
            writer.WriteUInt32(netId);

            writer.WriteVector3(position);
            writer.WriteQuaternion(rotation);
            writer.WriteVector3(scale);

            writer.WriteBoolean(isLocalPlayer);
            writer.WriteBoolean(isOwner);

            // we only need to send one of the ids
            byte idType = (byte)(sceneId != 0 ? 0 : 1);
            writer.WriteByte(idType);
            if (idType == 0)
                writer.WriteUInt64(sceneId);
            else
                writer.WriteGuid(assetId);

            writer.WriteBytesAndSizeSegment(payload);
        }

        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadUInt32();

            position = reader.ReadVector3();
            rotation = reader.ReadQuaternion();
            scale = reader.ReadVector3();

            isLocalPlayer = reader.ReadBoolean();
            isOwner = reader.ReadBoolean();

            // we only need to send one of the ids
            byte idType = reader.ReadByte();
            if (idType == 0)
                sceneId = reader.ReadUInt64();
            else
                assetId = reader.ReadGuid();

            payload = reader.ReadBytesAndSizeSegment();
        }
    }

    // a snapshot for the part of the world that a connection sees
    public struct PartialWorldStateMessage : NetworkMessage
    {
        // serialized entities
        // <<payloadsize:ulong, PartialWorldStateEntity, ...>>
        public ArraySegment<byte> entitiesPayload;

        // calculate total size
        // TODO include Rpcs etc. later
        public int TotalSize() => entitiesPayload.Count;
    }

    // A client sends this message to the server
    // to calculate RTT and synchronize time
    public struct NetworkPingMessage : NetworkMessage
    {
        public double clientTime;

        public NetworkPingMessage(double value)
        {
            clientTime = value;
        }
    }

    // The server responds with this message
    // The client can use this to calculate RTT and sync time
    public struct NetworkPongMessage : NetworkMessage
    {
        public double clientTime;
        public double serverTime;
    }
}
