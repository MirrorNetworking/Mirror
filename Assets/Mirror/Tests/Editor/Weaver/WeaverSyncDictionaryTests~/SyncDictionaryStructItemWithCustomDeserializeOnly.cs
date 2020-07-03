using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryStructItemWithCustomDeserializeOnly
{
    class SyncDictionaryStructItemWithCustomDeserializeOnly : NetworkBehaviour
    {
        MyStructDictionary Foo;

        struct MyStruct
        {
            int potato;
            float floatingpotato;
            double givemetwopotatoes;
        }
        class MyStructDictionary : SyncDictionary<int, MyStruct>
        {
            protected override MyStruct DeserializeItem(NetworkReader reader)
            {
                return new MyStruct() { /* read some stuff here */ };
            }
        }
    }
}
