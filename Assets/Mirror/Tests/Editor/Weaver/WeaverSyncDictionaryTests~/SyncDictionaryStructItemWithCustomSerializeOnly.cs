using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryStructItemWithCustomSerializeOnly
{
    class SyncDictionaryStructItemWithCustomSerializeOnly : NetworkBehaviour
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
            protected override void SerializeItem(NetworkWriter writer, MyStruct item)
            {
                // write some stuff here
            }
        }
    }
}
