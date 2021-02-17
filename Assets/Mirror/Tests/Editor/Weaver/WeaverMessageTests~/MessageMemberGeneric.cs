using System;
using Mirror;
using UnityEngine;

namespace WeaverMessageTests.MessageMemberGeneric
{
    class HasGeneric<T> { }

    class MessageMemberGeneric : NetworkMessage
    {
        public uint netId;
        public Guid assetId;
        public Vector3 position;
        public Quaternion rotation;
        public HasGeneric<int> invalidField;
        public byte[] payload;
    }
}
