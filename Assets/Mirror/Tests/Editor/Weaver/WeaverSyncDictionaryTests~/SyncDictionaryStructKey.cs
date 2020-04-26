using UnityEngine;
using Mirror;

namespace MirrorTest
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
