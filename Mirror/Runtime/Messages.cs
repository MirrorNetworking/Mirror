using System;
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

    public class ErrorMessage : MessageBase
    {
        public byte errorCode; // byte instead of int because NetworkServer uses byte anyway. saves bandwidth.

        public override void Deserialize(NetworkReader reader)
        {
            errorCode = reader.ReadByte();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(errorCode);
        }
    }

    public class ReadyMessage : EmptyMessage
    {
    }

    public class NotReadyMessage : EmptyMessage
    {
    }

    public class AddPlayerMessage : MessageBase
    {
        public byte[] msgData;

        public override void Deserialize(NetworkReader reader)
        {
            msgData = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WriteBytesAndSize(msgData);
        }
    }

    public class RemovePlayerMessage : EmptyMessage
    {
    }

    // ---------- System Messages requried for code gen path -------------------

    class CommandMessage : MessageBase
    {
        public uint netId;
        public int componentIndex;
        public int cmdHash;
        public byte[] payload; // the parameters for the Cmd function

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            componentIndex = (int)reader.ReadPackedUInt32();
            cmdHash = reader.ReadInt32(); // hash is always 4 full bytes, WritePackedInt would send 1 extra byte here
            payload = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WritePackedUInt32((uint)componentIndex);
            writer.Write(cmdHash);
            writer.WriteBytesAndSize(payload);
        }
    }

    class RpcMessage : MessageBase
    {
        public uint netId;
        public int componentIndex;
        public int rpcHash;
        public byte[] payload; // the parameters for the Rpc function

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            componentIndex = (int)reader.ReadPackedUInt32();
            rpcHash = reader.ReadInt32(); // hash is always 4 full bytes, WritePackedInt would send 1 extra byte here
            payload = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WritePackedUInt32((uint)componentIndex);
            writer.Write(rpcHash);
            writer.WriteBytesAndSize(payload);
        }
    }

    class SyncEventMessage : MessageBase
    {
        public uint netId;
        public int componentIndex;
        public int eventHash;
        public byte[] payload; // the parameters for the Rpc function

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            componentIndex = (int)reader.ReadPackedUInt32();
            eventHash = reader.ReadInt32(); // hash is always 4 full bytes, WritePackedInt would send 1 extra byte here
            payload = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WritePackedUInt32((uint)componentIndex);
            writer.Write(eventHash);
            writer.WriteBytesAndSize(payload);
        }
    }

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
        public byte[] payload;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            sceneId = reader.ReadPackedUInt32();
            position = reader.ReadVector3();
            payload = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WritePackedUInt32(sceneId);
            writer.Write(position);
            writer.WriteBytesAndSize(payload);
        }
    }

    class ObjectSpawnFinishedMessage : MessageBase
    {
        public byte state; // byte because it's always 0 or 1

        public override void Deserialize(NetworkReader reader)
        {
            state = reader.ReadByte();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(state);
        }
    }

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

    class AnimationMessage : MessageBase
    {
        public uint netId;
        public int      stateHash;      // if non-zero, then Play() this animation, skipping transitions
        public float    normalizedTime;
        public byte[]   parameters;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            stateHash = (int)reader.ReadPackedUInt32();
            normalizedTime = reader.ReadSingle();
            parameters = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WritePackedUInt32((uint)stateHash);
            writer.Write(normalizedTime);
            writer.WriteBytesAndSize(parameters);
        }
    }

    class AnimationParametersMessage : MessageBase
    {
        public uint netId;
        public byte[] parameters;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            parameters = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WriteBytesAndSize(parameters);
        }
    }

    class AnimationTriggerMessage : MessageBase
    {
        public uint netId;
        public int hash;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            hash = (int)reader.ReadPackedUInt32();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WritePackedUInt32((uint)hash);
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

    class LocalChildTransformMessage : MessageBase
    {
        public uint netId;
        public uint childIndex;
        public byte[] payload;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            childIndex = reader.ReadPackedUInt32();
            payload = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WritePackedUInt32(childIndex);
            writer.WriteBytesAndSize(payload);
        }
    }

    class LocalPlayerTransformMessage : MessageBase
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
}
