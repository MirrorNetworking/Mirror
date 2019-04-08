using System;
using UnityEngine;

namespace Mirror
{
    class SpawnPrefabMessage : MessageBase
    {
        public uint netId;
        public Guid assetId;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public byte[] payload;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            assetId = reader.ReadGuid();
            position = reader.ReadVector3();
            rotation = reader.ReadQuaternion();
            scale = reader.ReadVector3();
            payload = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.Write(assetId);
            writer.Write(position);
            writer.Write(rotation);
            writer.Write(scale);
            writer.WriteBytesAndSize(payload);
        }
    }

    class SpawnSceneObjectMessage : MessageBase
    {
        public uint netId;
        public ulong sceneId;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public byte[] payload;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            sceneId = reader.ReadUInt64();
            position = reader.ReadVector3();
            rotation = reader.ReadQuaternion();
            scale = reader.ReadVector3();
            payload = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.Write(sceneId);
            writer.Write(position);
            writer.Write(rotation);
            writer.Write(scale);
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

    class ObjectHideMessage : MessageBase
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
        public NetworkPingMessage() {}

        public NetworkPingMessage(double value) : base(value) {}
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
