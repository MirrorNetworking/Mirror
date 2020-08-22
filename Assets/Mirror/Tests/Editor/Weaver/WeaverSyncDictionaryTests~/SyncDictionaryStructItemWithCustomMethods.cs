using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryStructItemWithCustomMethods
{
    class SyncDictionaryItemStructWithCustomMethods : NetworkBehaviour
    {
        MyStructDictionary Foo;

        struct MyStruct
        {
            public int potato;
            public float floatingpotato;
            public double givemetwopotatoes;
        }
        class MyStructDictionary : SyncDictionary<int, MyStruct>
        {
            protected override void SerializeItem(NetworkWriter writer, MyStruct item)
            {
                // write some stuff here
            }

            protected override MyStruct DeserializeItem(NetworkReader reader)
            {
                return new MyStruct() { /* read some stuff here */ };
            }
        }
    }
}
