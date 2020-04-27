using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverMessageTests.MessageSelfReferencing
{
    class MessageSelfReferencing : MessageBase
    {
        public uint netId;
        public Guid assetId;
        public Vector3 position;
        public Quaternion rotation;
        public MessageSelfReferencing selfReference = new MessageSelfReferencing();
        public byte[] payload;
    }
}
