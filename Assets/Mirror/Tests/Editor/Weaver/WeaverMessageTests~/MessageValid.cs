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
    }
}
