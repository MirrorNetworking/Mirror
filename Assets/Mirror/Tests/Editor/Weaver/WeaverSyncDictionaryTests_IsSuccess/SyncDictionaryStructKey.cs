using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryStructKey
{
    class SyncDictionaryStructKey : NetworkBehaviour
    {
        readonly MyStructDictionary Foo;

        struct MyStruct
        {
            public int potato;
            public float floatingpotato;
            public double givemetwopotatoes;
        }
        class MyStructDictionary : SyncDictionary<MyStruct, string> { }
    }
}
