using System;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        public virtual void Deserialize(NetworkReader reader) {}

        // Serialize the contents of this message into the writer
        public virtual void Serialize(NetworkWriter writer) {}
    }

    #region General Typed Messages
    public class StringMessage : MessageBase
    {
        public string value;

        public StringMessage() {}

        public StringMessage(string v)
        {
            value = v;
        }

        public override void Deserialize(NetworkReader reader)
        {
            value = reader.ReadString();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WriteString(value);
        }
    }

    public class ByteMessage : MessageBase
    {
        public byte value;

        public ByteMessage() {}

        public ByteMessage(byte v)
        {
            value = v;
        }

        public override void Deserialize(NetworkReader reader)
        {
            value = reader.ReadByte();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WriteByte(value);
        }
    }

    public class BytesMessage : MessageBase
    {
        public byte[] value;

        public BytesMessage() {}

        public BytesMessage(byte[] v)
        {
            value = v;
        }

        public override void Deserialize(NetworkReader reader)
        {
            value = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WriteBytesAndSize(value);
        }
    }

    public class IntegerMessage : MessageBase
    {
        public int value;

        public IntegerMessage() {}

        public IntegerMessage(int v)
        {
            value = v;
        }

        public override void Deserialize(NetworkReader reader)
        {
            value = reader.ReadPackedInt32();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedInt32(value);
        }
    }

    public class DoubleMessage : MessageBase
    {
        public double value;

        public DoubleMessage() {}

        public DoubleMessage(double v)
        {
            value = v;
        }

        public override void Deserialize(NetworkReader reader)
        {
            value = reader.ReadDouble();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WriteDouble(value);
        }
    }

    public class EmptyMessage : MessageBase
    {
        public override void Deserialize(NetworkReader reader) {}

        public override void Serialize(NetworkWriter writer) {}
    }
    #endregion

    #region Public System Messages
    public class ErrorMessage : ByteMessage {}

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
        public byte[] value;

        public void Deserialize(NetworkReader reader)
        {
            value = reader.ReadBytesAndSize();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WriteBytesAndSize(value);
        }
    }

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
        public LoadSceneMode sceneMode; // Single = 0, Additive = 1
        public LocalPhysicsMode physicsMode; // None = 0, Physics3D = 1, Physics2D = 2

        public void Deserialize(NetworkReader reader)
        {
            sceneName = reader.ReadString();
            sceneMode = (LoadSceneMode)reader.ReadByte();
            physicsMode = (LocalPhysicsMode)reader.ReadByte();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WriteString(sceneName);
            writer.WriteByte((byte)sceneMode);
            writer.WriteByte((byte)physicsMode);
        }
    }
    #endregion

    #region System Messages requried for code gen path
    struct CommandMessage : IMessageBase
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
            functionHash = reader.ReadInt32(); // hash is always 4 full bytes, WritePackedInt would send 1 extra byte here
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

    struct RpcMessage : IMessageBase
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
            functionHash = reader.ReadInt32(); // hash is always 4 full bytes, WritePackedInt would send 1 extra byte here
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

    struct SyncEventMessage : IMessageBase
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
            functionHash = reader.ReadInt32(); // hash is always 4 full bytes, WritePackedInt would send 1 extra byte here
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
    struct SpawnPrefabMessage : IMessageBase
    {
        public uint netId;
        public bool owner;
        public Guid assetId;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        // the serialized component data
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;

        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            owner = reader.ReadBoolean();
            assetId = reader.ReadGuid();
            position = reader.ReadVector3();
            rotation = reader.ReadQuaternion();
            scale = reader.ReadVector3();
            payload = reader.ReadBytesAndSizeSegment();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WriteBoolean(owner);
            writer.WriteGuid(assetId);
            writer.WriteVector3(position);
            writer.WriteQuaternion(rotation);
            writer.WriteVector3(scale);
            writer.WriteBytesAndSizeSegment(payload);
        }
    }

    struct SpawnSceneObjectMessage : IMessageBase
    {
        public uint netId;
        public bool owner;
        public ulong sceneId;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        // the serialized component data
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;

        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            owner = reader.ReadBoolean();
            sceneId = reader.ReadUInt64();
            position = reader.ReadVector3();
            rotation = reader.ReadQuaternion();
            scale = reader.ReadVector3();
            payload = reader.ReadBytesAndSizeSegment();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WriteBoolean(owner);
            writer.WriteUInt64(sceneId);
            writer.WriteVector3(position);
            writer.WriteQuaternion(rotation);
            writer.WriteVector3(scale);
            writer.WriteBytesAndSizeSegment(payload);
        }
    }

    struct ObjectSpawnStartedMessage : IMessageBase
    {
        public void Deserialize(NetworkReader reader) { }

        public void Serialize(NetworkWriter writer) { }
    }

    struct ObjectSpawnFinishedMessage : IMessageBase
    {
        public void Deserialize(NetworkReader reader) { }

        public void Serialize(NetworkWriter writer) { }
    }

    struct ObjectDestroyMessage : IMessageBase
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

    struct ObjectHideMessage : IMessageBase
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

    struct ClientAuthorityMessage : IMessageBase
    {
        public uint netId;
        public bool authority;

        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            authority = reader.ReadBoolean();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WriteBoolean(authority);
        }
    }

    struct UpdateVarsMessage : IMessageBase
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
    struct NetworkPingMessage : IMessageBase
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
    struct NetworkPongMessage : IMessageBase
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
