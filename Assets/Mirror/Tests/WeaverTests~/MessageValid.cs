using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class PrefabClone : MessageBase
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
}
