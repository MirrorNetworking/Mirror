using System;
using Mirror;
using UnityEngine;

namespace WeaverMessageTests.MessageWithBaseClass
{
    class MessageWithBaseClass : SomeBaseMessage
    {
        public uint netId;
        public Guid assetId;
        public Vector3 position;
        public Quaternion rotation;
        public byte[] payload;
    }

    class SomeBaseMessage : NetworkMessage
    {
        public int myExtraType;
    }
}
