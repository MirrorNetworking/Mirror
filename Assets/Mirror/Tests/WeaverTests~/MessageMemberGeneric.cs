using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class HasGeneric<T> {}

    class PrefabClone : MessageBase
    {
        public uint netId;
        public Guid assetId;
        public Vector3 position;
        public Quaternion rotation;
        public HasGeneric<int> invalidField;
        public byte[] payload;
    }
}
