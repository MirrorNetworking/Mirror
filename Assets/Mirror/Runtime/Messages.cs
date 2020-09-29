using System;
using UnityEngine;

namespace Mirror
{
    public interface NetworkMessage
    {
        void Deserialize(NetworkReader reader);

        void Serialize(NetworkWriter writer);
    }

    #region Public System Messages
    public struct ErrorMessage : NetworkMessage
    {
        public byte value;

        public ErrorMessage(byte v)
        {
            value = v;
        }

        public void Deserialize(NetworkReader reader)
        {
            value = reader.ReadByte();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WriteByte(value);
        }
    }

    public struct ReadyMessage : NetworkMessage
    {
        public void Deserialize(NetworkReader reader) { }

        public void Serialize(NetworkWriter writer) { }
    }

    public struct NotReadyMessage : NetworkMessage
    {
        public void Deserialize(NetworkReader reader) { }

        public void Serialize(NetworkWriter writer) { }
    }

    public struct AddPlayerMessage : NetworkMessage
    {
        public void Deserialize(NetworkReader reader) { }

        public void Serialize(NetworkWriter writer) { }
    }

    public struct DisconnectMessage : NetworkMessage
    {
        public void Deserialize(NetworkReader reader) { }

        public void Serialize(NetworkWriter writer) { }
    }

    public struct ConnectMessage : NetworkMessage
    {
        public void Deserialize(NetworkReader reader) { }

        public void Serialize(NetworkWriter writer) { }
    }

    public struct SceneMessage : NetworkMessage
    {
        public string sceneName;
        // Normal = 0, LoadAdditive = 1, UnloadAdditive = 2
        public SceneOperation sceneOperation;
        public bool customHandling;

        public void Deserialize(NetworkReader reader)
        {
            sceneName = reader.ReadString();
            sceneOperation = (SceneOperation)reader.ReadByte();
            customHandling = reader.ReadBoolean();
        }

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

    #endregion

    #region System Messages requried for code gen path
    public struct CommandMessage : NetworkMessage
    {
        public uint netId;
        public int componentIndex;
        public int functionHash;
        // the parameters for the Cmd function
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;

        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadUInt32();
            componentIndex = (int)reader.ReadUInt32();
            // hash is always 4 full bytes, WritePackedInt would send 1 extra byte here
            functionHash = reader.ReadInt32();
            payload = reader.ReadBytesAndSizeSegment();
        }

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

        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadUInt32();
            componentIndex = (int)reader.ReadUInt32();
            // hash is always 4 full bytes, WritePackedInt would send 1 extra byte here
            functionHash = reader.ReadInt32();
            payload = reader.ReadBytesAndSizeSegment();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WriteUInt32(netId);
            writer.WriteUInt32((uint)componentIndex);
            writer.WriteInt32(functionHash);
            writer.WriteBytesAndSizeSegment(payload);
        }
    }
    #endregion

    #region Internal System Messages
    public struct SpawnMessage : NetworkMessage
    {
        /// <summary>
        /// netId of new or existing object
        /// </summary>
        public uint netId;
        /// <summary>
        /// Is the spawning object the local player. Sets ClientScene.localPlayer
        /// </summary>
        public bool isLocalPlayer;
        /// <summary>
        /// Sets hasAuthority on the spawned object
        /// </summary>
        public bool isOwner;
        /// <summary>
        /// The id of the scene object to spawn
        /// </summary>
        public ulong sceneId;
        /// <summary>
        /// The id of the prefab to spawn
        /// <para>If sceneId != 0 then it is used instead of assetId</para>
        /// </summary>
        public Guid assetId;
        /// <summary>
        /// Local position
        /// </summary>
        public Vector3 position;
        /// <summary>
        /// Local rotation
        /// </summary>
        public Quaternion rotation;
        /// <summary>
        /// Local scale
        /// </summary>
        public Vector3 scale;
        /// <summary>
        /// The serialized component data
        /// <remark>ArraySegment to avoid unnecessary allocations</remark>
        /// </summary>
        public ArraySegment<byte> payload;

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
        public void Deserialize(NetworkReader reader) { }

        public void Serialize(NetworkWriter writer) { }
    }

    public struct ObjectSpawnFinishedMessage : NetworkMessage
    {
        public void Deserialize(NetworkReader reader) { }

        public void Serialize(NetworkWriter writer) { }
    }

    public struct ObjectDestroyMessage : NetworkMessage
    {
        public uint netId;

        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadUInt32();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WriteUInt32(netId);
        }
    }

    public struct ObjectHideMessage : NetworkMessage
    {
        public uint netId;

        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadUInt32();
        }

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

        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadUInt32();
            payload = reader.ReadBytesAndSizeSegment();
        }

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

        public void Deserialize(NetworkReader reader)
        {
            clientTime = reader.ReadDouble();
        }

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

        public void Deserialize(NetworkReader reader)
        {
            clientTime = reader.ReadDouble();
            serverTime = reader.ReadDouble();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WriteDouble(clientTime);
            writer.WriteDouble(serverTime);
        }
    }
    #endregion
}
