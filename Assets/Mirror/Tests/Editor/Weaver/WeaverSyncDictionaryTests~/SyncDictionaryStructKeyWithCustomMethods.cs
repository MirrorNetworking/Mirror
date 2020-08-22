using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryStructKeyWithCustomMethods
{
    class SyncDictionaryKeyStructWithCustomMethods : NetworkBehaviour
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

            protected override MyStruct DeserializeKey(NetworkReader reader)
            {
                return new MyStruct() { /* read some stuff here */ };
            }
        }
    }
}
