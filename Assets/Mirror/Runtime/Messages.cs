﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    // This can't be an interface because users don't need to implement the
    // serialization functions, we'll code generate it for them when they omit it.
    public abstract class MessageBase
    {
        // De-serialize the contents of the reader into this message
        public virtual void Deserialize(NetworkReader reader) {}

        // Serialize the contents of this message into the writer
        public virtual void Serialize(NetworkWriter writer) {}
    }

    // ---------- General Typed Messages -------------------

    public class StringMessage : MessageBase
    {
        public string value;

        public StringMessage()
        {
        }

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
            writer.Write(value);
        }
    }

    public class ByteMessage : MessageBase
    {
        public byte value;

        public ByteMessage()
        {
        }

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
            writer.Write(value);
        }
    }

    public class BytesMessage : MessageBase
    {
        public byte[] value;

        public BytesMessage()
        {
        }

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

        public IntegerMessage()
        {
        }

        public IntegerMessage(int v)
        {
            value = v;
        }

        public override void Deserialize(NetworkReader reader)
        {
            value = (int)reader.ReadPackedUInt32();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32((uint)value);
        }
    }

    public class DoubleMessage : MessageBase
    {
        public double value;

        public DoubleMessage()
        {
        }

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
            writer.Write(value);
        }
    }

    public class EmptyMessage : MessageBase
    {
        public override void Deserialize(NetworkReader reader)
        {
        }

        public override void Serialize(NetworkWriter writer)
        {
        }
    }

    // ---------- Public System Messages -------------------

    public class ErrorMessage : ByteMessage {}

    public class ReadyMessage : EmptyMessage {}

    public class NotReadyMessage : EmptyMessage {}

    public class AddPlayerMessage : BytesMessage {}

    public class RemovePlayerMessage : EmptyMessage {}

    // ---------- System Messages requried for code gen path -------------------

    // remote calls like Rpc/Cmd/SyncEvent all use the same message type
    class RemoteCallMessage : MessageBase
    {
        public uint netId;
        public int componentIndex;
        public int functionHash;
        public byte[] payload; // the parameters for the Cmd function

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            componentIndex = (int)reader.ReadPackedUInt32();
            functionHash = reader.ReadInt32(); // hash is always 4 full bytes, WritePackedInt would send 1 extra byte here
            payload = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WritePackedUInt32((uint)componentIndex);
            writer.Write(functionHash);
            writer.WriteBytesAndSize(payload);
        }
    }

    class CommandMessage : RemoteCallMessage {}

    class RpcMessage : RemoteCallMessage {}

    class SyncEventMessage : RemoteCallMessage {}

    // ---------- Internal System Messages -------------------

    class SpawnPrefabMessage : MessageBase
    {
        public uint netId;
        public Guid assetId;
        public Vector3 position;
        public Quaternion rotation;
        public byte[] payload;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            assetId = reader.ReadGuid();
            position = reader.ReadVector3();
            rotation = reader.ReadQuaternion();
            payload = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.Write(assetId);
            writer.Write(position);
            writer.Write(rotation);
            writer.WriteBytesAndSize(payload);
        }
    }

    class SpawnSceneObjectMessage : MessageBase
    {
        public uint netId;
        public uint sceneId;
        public Vector3 position;
        public Quaternion rotation;
        public byte[] payload;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            sceneId = reader.ReadPackedUInt32();
            position = reader.ReadVector3();
            rotation = reader.ReadQuaternion();
            payload = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WritePackedUInt32(sceneId);
            writer.Write(position);
            writer.Write(rotation);
            writer.WriteBytesAndSize(payload);
        }
    }

    class ObjectSpawnStartedMessage : EmptyMessage {}

    class ObjectSpawnFinishedMessage : EmptyMessage {}

    class ObjectDestroyMessage : MessageBase
    {
        public uint netId;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
        }
    }

    class OwnerMessage : MessageBase
    {
        public uint netId;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
        }
    }

    class ClientAuthorityMessage : MessageBase
    {
        public uint netId;
        public bool authority;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            authority = reader.ReadBoolean();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.Write(authority);
        }
    }

    class UpdateVarsMessage : MessageBase
    {
        public uint netId;
        public byte[] payload;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            payload = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WriteBytesAndSize(payload);
        }
    }

    // A client sends this message to the server
    // to calculate RTT and synchronize time
    class NetworkPingMessage : DoubleMessage
    {
        public NetworkPingMessage()
        {
        }

        public NetworkPingMessage(double value) : base(value)
        {
        }
    }

    // The server responds with this message
    // The client can use this to calculate RTT and sync time
    class NetworkPongMessage : MessageBase
    {
        public double clientTime;
        public double serverTime;

        public override void Deserialize(NetworkReader reader)
        {
            clientTime = reader.ReadDouble();
            serverTime = reader.ReadDouble();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(clientTime);
            writer.Write(serverTime);
        }
    }
}
