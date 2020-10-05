using System;
using Mirror;
using UnityEngine;

namespace WeaverMessageTests.MessageSelfReferencing
{
    class MessageSelfReferencing : NetworkMessage
    {
        public uint netId;
        public Guid assetId;
        public Vector3 position;
        public Quaternion rotation;
        public MessageSelfReferencing selfReference = new MessageSelfReferencing();
        public byte[] payload;
    }
}
