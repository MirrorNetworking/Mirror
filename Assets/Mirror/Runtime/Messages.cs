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

    public struct ReadyMessage : NetworkMessage
    {
        public void Deserialize(NetworkReader reader) {}
        public void Serialize(NetworkWriter writer) {}
    }

    public struct NotReadyMessage : NetworkMessage
    {
        public void Deserialize(NetworkReader reader) {}
        public void Serialize(NetworkWriter writer) {}
    }

    public struct AddPlayerMessage : NetworkMessage
    {
        public void Deserialize(NetworkReader reader) {}
        public void Serialize(NetworkWriter writer) {}
    }

    public struct SceneMessage : NetworkMessage
    {
        public string sceneName;
        // Normal = 0, LoadAdditive = 1, UnloadAdditive = 2
        public SceneOperation sceneOperation;
        public bool customHandling;

        // rollback: weaver doesn't weave Mirror itself yet
        public void Deserialize(NetworkReader reader)
        {
            sceneName = reader.ReadString();
            sceneOperation = (SceneOperation)reader.ReadByte();
            customHandling = reader.ReadBoolean();
        }

        // rollback: weaver doesn't weave Mirror itself yet
        public void Serialize(NetworkWriter writer)
        {
            writer.WriteString(sceneName);
            writer.WriteByte((byte)sceneOperation);
            writer.WriteBoolean(customHandling);
        }
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

        // rollback: weaver doesn't weave Mirror itself yet
        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadUInt32();
            componentIndex = (int)reader.ReadUInt32();
            functionHash = reader.ReadInt32();
            payload = reader.ReadBytesAndSizeSegment();
        }

        // rollback: weaver doesn't weave Mirror itself yet
        public void Serialize(NetworkWriter writer)
        {
            writer.WriteUInt32(netId);
            writer.WriteUInt32((uint)componentIndex);
            writer.WriteInt32(functionHash);
            writer.WriteBytesAndSizeSegment(payload);
        }
    }

    public struct RpcMessage : NetworkMessage
    {
        public uint netId;
        public int componentIndex;
        public int functionHash;
        // the parameters for the Cmd function
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;

        // rollback: weaver doesn't weave Mirror itself yet
        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadUInt32();
            componentIndex = (int)reader.ReadUInt32();
            functionHash = reader.ReadInt32();
            payload = reader.ReadBytesAndSizeSegment();
        }

        // rollback: weaver doesn't weave Mirror itself yet
        public void Serialize(NetworkWriter writer)
        {
            writer.WriteUInt32(netId);
            writer.WriteUInt32((uint)componentIndex);
            writer.WriteInt32(functionHash);
            writer.WriteBytesAndSizeSegment(payload);
        }
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
        public Guid assetId;
        // Local position
        public Vector3 position;
        // Local rotation
        public Quaternion rotation;
        // Local scale
        public Vector3 scale;
        // serialized component data
        // ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;

        // rollback: weaver doesn't weave Mirror itself yet
        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadUInt32();
            isLocalPlayer = reader.ReadBoolean();
            isOwner = reader.ReadBoolean();
            sceneId = reader.ReadUInt64();
            if (sceneId == 0)
            {
                assetId = reader.ReadGuid();
            }
            position = reader.ReadVector3();
            rotation = reader.ReadQuaternion();
            scale = reader.ReadVector3();
            payload = reader.ReadBytesAndSizeSegment();
        }

        // rollback: weaver doesn't weave Mirror itself yet
        public void Serialize(NetworkWriter writer)
        {
            writer.WriteUInt32(netId);
            writer.WriteBoolean(isLocalPlayer);
            writer.WriteBoolean(isOwner);
            writer.WriteUInt64(sceneId);
            if (sceneId == 0)
            {
                writer.WriteGuid(assetId);
            }
            writer.WriteVector3(position);
            writer.WriteQuaternion(rotation);
            writer.WriteVector3(scale);
            writer.WriteBytesAndSizeSegment(payload);
        }
    }

    public struct ObjectSpawnStartedMessage : NetworkMessage
    {
        public void Deserialize(NetworkReader reader) {}
        public void Serialize(NetworkWriter writer) {}
    }

    public struct ObjectSpawnFinishedMessage : NetworkMessage
    {
        public void Deserialize(NetworkReader reader) {}
        public void Serialize(NetworkWriter writer) {}
    }

    public struct ObjectDestroyMessage : NetworkMessage
    {
        public uint netId;

        // rollback: weaver doesn't weave Mirror itself yet
        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadUInt32();
        }

        // rollback: weaver doesn't weave Mirror itself yet
        public void Serialize(NetworkWriter writer)
        {
            writer.WriteUInt32(netId);
        }
    }

    public struct ObjectHideMessage : NetworkMessage
    {
        public uint netId;

        // rollback: weaver doesn't weave Mirror itself yet
        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadUInt32();
        }

        // rollback: weaver doesn't weave Mirror itself yet
        public void Serialize(NetworkWriter writer)
        {
            writer.WriteUInt32(netId);
        }
    }

    public struct UpdateVarsMessage : NetworkMessage
    {
        public uint netId;
        // the serialized component data
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;

        // rollback: weaver doesn't weave Mirror itself yet
        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadUInt32();
            payload = reader.ReadBytesAndSizeSegment();
        }

        // rollback: weaver doesn't weave Mirror itself yet
        public void Serialize(NetworkWriter writer)
        {
            writer.WriteUInt32(netId);
            writer.WriteBytesAndSizeSegment(payload);
        }
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

        // rollback: weaver doesn't weave Mirror itself yet
        public void Deserialize(NetworkReader reader)
        {
            clientTime = reader.ReadDouble();
        }

        // rollback: weaver doesn't weave Mirror itself yet
        public void Serialize(NetworkWriter writer)
        {
            writer.WriteDouble(clientTime);
        }
    }

    // The server responds with this message
    // The client can use this to calculate RTT and sync time
    public struct NetworkPongMessage : NetworkMessage
    {
        public double clientTime;
        public double serverTime;

        // rollback: weaver doesn't weave Mirror itself yet
        public void Deserialize(NetworkReader reader)
        {
            clientTime = reader.ReadDouble();
            serverTime = reader.ReadDouble();
        }

        // rollback: weaver doesn't weave Mirror itself yet
        public void Serialize(NetworkWriter writer)
        {
            writer.WriteDouble(clientTime);
            writer.WriteDouble(serverTime);
        }
    }
}
