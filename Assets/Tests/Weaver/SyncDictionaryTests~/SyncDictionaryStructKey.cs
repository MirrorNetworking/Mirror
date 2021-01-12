using Mirror;

namespace SyncDictionaryTests.SyncDictionaryStructKey
{
    class SyncDictionaryStructKey : NetworkBehaviour
    {
        MyStructDictionary Foo;

        struct MyStruct
        {
            public int potato;
            public float floatingpotato;
            public double givemetwopotatoes;
        }
        class MyStructDictionary : SyncDictionary<MyStruct, string> { }
    }
}
