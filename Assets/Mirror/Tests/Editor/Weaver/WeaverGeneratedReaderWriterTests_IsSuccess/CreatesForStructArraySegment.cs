using System;
using Mirror;
using UnityEngine;

namespace GeneratedReaderWriter.CreatesForStructArraySegment
{
    public class CreatesForStructArraySegment : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(ArraySegment<MyStruct> data)
        {
            // empty
        }
    }

    public struct MyStruct
    {
        public int someValue;
        public Vector3 anotherValue;
    }
}
