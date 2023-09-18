using System.Collections.Generic;
using Mirror;
using GodotEngine;

namespace GeneratedReaderWriter.CreatesForStructList
{
    public class CreatesForStructList : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(List<MyStruct> data)
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

