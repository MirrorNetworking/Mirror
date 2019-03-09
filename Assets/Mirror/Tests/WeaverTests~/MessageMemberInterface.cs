using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    interface SuperCoolInterface {}

    class PrefabClone : MessageBase
    {
        public uint netId;
        public Guid assetId;
        public Vector3 position;
        public Quaternion rotation;
        public SuperCoolInterface invalidField;
        public byte[] payload;
    }
}
