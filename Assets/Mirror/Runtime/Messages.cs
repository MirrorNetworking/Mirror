using System;
using UnityEngine;

namespace Mirror
{
    public interface IMessageBase
    {
        void Deserialize(NetworkReader reader);

        void Serialize(NetworkWriter writer);
    }

    public abstract class MessageBase : IMessageBase
    {
        // De-serialize the contents of the reader into this message
        public virtual void Deserialize(NetworkReader reader) { }

        // Serialize the contents of this message into the writer
        public virtual void Serialize(NetworkWriter writer) { }
    }

    #region Public System Messages
    public struct ErrorMessage : IMessageBase
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

    public struct ReadyMessage : IMessageBase
    {
        public void Deserialize(NetworkReader reader) { }

        public void Serialize(NetworkWriter writer) { }
    }

    public struct NotReadyMessage : IMessageBase
    {
        public void Deserialize(NetworkReader reader) { }

        public void Serialize(NetworkWriter writer) { }
    }

    public struct AddPlayerMessage : IMessageBase
    {
        public void Deserialize(NetworkReader reader) { }

        public void Serialize(NetworkWriter writer) { }
    }

    // Deprecated 5/2/2020
    /// <summary>
    /// Obsolete: Removed as a security risk. Use <see cref="NetworkServer.RemovePlayerForConnection(NetworkConnection, bool)">NetworkServer.RemovePlayerForConnection</see> instead.
    /// </summary>
    [Obsolete("Removed as a security risk. Use NetworkServer.RemovePlayerForConnection(NetworkConnection conn, bool keepAuthority = false) instead")]
    public struct RemovePlayerMessage : IMessageBase
    {
        public void Deserialize(NetworkReader reader) { }

        public void Serialize(NetworkWriter writer) { }
    }

    public struct DisconnectMessage : IMessageBase
    {
        public void Deserialize(NetworkReader reader) { }

        public void Serialize(NetworkWriter writer) { }
    }

    public struct ConnectMessage : IMessageBase
    {
        public void Deserialize(NetworkReader reader) { }

        public void Serialize(NetworkWriter writer) { }
    }

    public struct SceneMessage : IMessageBase
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
    public struct CommandMessage : IMessageBase
    {
        public uint netId;
        public int componentIndex;
        public int functionHash;
        // the parameters for the Cmd function
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;

        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            componentIndex = (int)reader.ReadPackedUInt32();
            // hash is always 4 full bytes, WritePackedInt would send 1 extra byte here
            functionHash = reader.ReadInt32();
            payload = reader.ReadBytesAndSizeSegment();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WritePackedUInt32((uint)componentIndex);
            writer.WriteInt32(functionHash);
            writer.WriteBytesAndSizeSegment(payload);
        }
    }

    public struct RpcMessage : IMessageBase
    {
        public uint netId;
        public int componentIndex;
        public int functionHash;
        // the parameters for the Cmd function
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;

        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            componentIndex = (int)reader.ReadPackedUInt32();
            // hash is always 4 full bytes, WritePackedInt would send 1 extra byte here
            functionHash = reader.ReadInt32();
            payload = reader.ReadBytesAndSizeSegment();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WritePackedUInt32((uint)componentIndex);
            writer.WriteInt32(functionHash);
            writer.WriteBytesAndSizeSegment(payload);
        }
    }

    public struct SyncEventMessage : IMessageBase
    {
        public uint netId;
        public int componentIndex;
        public int functionHash;
        // the parameters for the Cmd function
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;

        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            componentIndex = (int)reader.ReadPackedUInt32();
            // hash is always 4 full bytes, WritePackedInt would send 1 extra byte here
            functionHash = reader.ReadInt32();
            payload = reader.ReadBytesAndSizeSegment();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WritePackedUInt32((uint)componentIndex);
            writer.WriteInt32(functionHash);
            writer.WriteBytesAndSizeSegment(payload);
        }
    }
    #endregion

    #region Internal System Messages
    public struct SpawnMessage : IMessageBase
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
            netId = reader.ReadPackedUInt32();
            isLocalPlayer = reader.ReadBoolean();
            isOwner = reader.ReadBoolean();
            sceneId = reader.ReadPackedUInt64();
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
            writer.WritePackedUInt32(netId);
            writer.WriteBoolean(isLocalPlayer);
            writer.WriteBoolean(isOwner);
            writer.WritePackedUInt64(sceneId);
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

    public struct ObjectSpawnStartedMessage : IMessageBase
    {
        public void Deserialize(NetworkReader reader) { }

        public void Serialize(NetworkWriter writer) { }
    }

    public struct ObjectSpawnFinishedMessage : IMessageBase
    {
        public void Deserialize(NetworkReader reader) { }

        public void Serialize(NetworkWriter writer) { }
    }

    public struct ObjectDestroyMessage : IMessageBase
    {
        public uint netId;

        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
        }
    }

    public struct ObjectHideMessage : IMessageBase
    {
        public uint netId;

        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
        }
    }

    public struct UpdateVarsMessage : IMessageBase
    {
        public uint netId;
        // the serialized component data
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;

        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            payload = reader.ReadBytesAndSizeSegment();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WriteBytesAndSizeSegment(payload);
        }
    }

    // A client sends this message to the server
    // to calculate RTT and synchronize time
    public struct NetworkPingMessage : IMessageBase
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
    public struct NetworkPongMessage : IMessageBase
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
