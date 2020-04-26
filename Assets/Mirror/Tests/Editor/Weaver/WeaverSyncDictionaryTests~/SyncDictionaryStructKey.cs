using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.SyncDictionaryStructKey
{
    class SyncDictionaryStructKey : NetworkBehaviour
    {
        MyStructDictionary Foo;

        struct MyStruct
        {
            int potato;
            float floatingpotato;
            double givemetwopotatoes;
        }
        class MyStructDictionary : SyncDictionary<MyStruct, string> { }
    }
}
