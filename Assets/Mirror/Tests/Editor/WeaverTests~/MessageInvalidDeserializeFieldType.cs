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
        public AccessViolationException invalidField;
        public byte[] payload;

        // this will cause generate serialization to be skipped, testing generate deserialization
        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WriteGuid(assetId);
            writer.WriteVector3(position);
            writer.WriteQuaternion(rotation);
            writer.WriteBytesAndSize(payload);
        }
    }
}
