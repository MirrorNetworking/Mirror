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

        // this will guarantee only serialize will be generated (even though weaver does serialize first)
        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            assetId = reader.ReadGuid();
            position = reader.ReadVector3();
            rotation = reader.ReadQuaternion();
            payload = reader.ReadBytesAndSize();
        }
    }
}
