using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryStructKeyWithCustomDeserializeOnly
{
    class SyncDictionaryStructKeyWithCustomDeserializeOnly : NetworkBehaviour
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
            protected override MyStruct DeserializeKey(NetworkReader reader)
            {
                return new MyStruct() { /* read some stuff here */ };
            }
        }
    }
}
