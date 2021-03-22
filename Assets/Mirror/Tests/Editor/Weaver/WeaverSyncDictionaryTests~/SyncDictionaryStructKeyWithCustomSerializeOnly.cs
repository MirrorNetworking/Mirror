using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryStructKeyWithCustomSerializeOnly
{
    class SyncDictionaryStructKeyWithCustomSerializeOnly : NetworkBehaviour
    {
        MyStructDictionary Foo;

        struct MyStruct
        {
            public int potato;
            public float floatingpotato;
            public double givemetwopotatoes;
        }
        class MyStructDictionary : SyncDictionary<MyStruct, int>
        {
            protected override void SerializeKey(NetworkWriter writer, MyStruct item)
            {
                // write some stuff here
            }
        }
    }
}
