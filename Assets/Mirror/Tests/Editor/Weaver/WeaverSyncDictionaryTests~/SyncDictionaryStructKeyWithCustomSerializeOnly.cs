using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryStructKeyWithCustomSerializeOnly
{
    class SyncDictionaryStructKeyWithCustomSerializeOnly : NetworkBehaviour
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
        }
    }
}
