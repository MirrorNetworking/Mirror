using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class SyncDictionaryKeyStructWithCustomMethods : NetworkBehaviour
    {
        MyStructDictionary Foo;

        struct MyStruct
        {
            int potato;
            float floatingpotato;
            double givemetwopotatoes;
        }
        class MyStructDictionary : SyncDictionary<MyStruct, int>
        {
            protected override void SerializeKey(NetworkWriter writer, MyStruct item)
            {
                // write some stuff here
            }

            protected override MyStruct DeserializeKey(NetworkReader reader)
            {
                return new MyStruct() { /* read some stuff here */ };
            }
        }
    }
}
