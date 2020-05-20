using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverMessageTests.MessageMemberGeneric
{
    class HasGeneric<T> {}

    class MessageMemberGeneric : MessageBase
    {
        public uint netId;
        public Guid assetId;
        public Vector3 position;
        public Quaternion rotation;
        public HasGeneric<int> invalidField;
        public byte[] payload;
    }
}
