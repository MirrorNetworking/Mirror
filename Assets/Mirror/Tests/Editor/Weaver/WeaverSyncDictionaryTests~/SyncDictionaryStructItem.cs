using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryStructItem
{
    class SyncDictionaryStructItem : NetworkBehaviour
    {
        MyStructDictionary Foo;

        struct MyStruct
        {
            public int potato;
            public float floatingpotato;
            public double givemetwopotatoes;
        }
        class MyStructDictionary : SyncDictionary<int, MyStruct> { }
    }
}
