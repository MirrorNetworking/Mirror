using System;
using Mirror;
using GodotEngine;

namespace WeaverMessageTests.MessageMemberInterface
{
    interface SuperCoolInterface { }

    class MessageMemberInterface : NetworkMessage
    {
        public uint netId;
        public Guid assetId;
        public Vector3 position;
        public Quaternion rotation;
        public SuperCoolInterface invalidField;
        public byte[] payload;
    }
}
