using System;
using Mirror;
using UnityEngine;

namespace WeaverMessageTests.MessageValid
{
    struct MessageValid : NetworkMessage
    {
        public uint netId;
        public Guid assetId;
        public Vector3 position;
        public Quaternion rotation;
        public byte[] payload;

        public void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadUInt32();
            assetId = reader.ReadGuid();
            position = reader.ReadVector3();
            rotation = reader.ReadQuaternion();
            payload = reader.ReadBytesAndSize();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WriteUInt32(netId);
            writer.WriteGuid(assetId);
            writer.WriteVector3(position);
            writer.WriteQuaternion(rotation);
            writer.WriteBytesAndSize(payload);
        }
    }
}
