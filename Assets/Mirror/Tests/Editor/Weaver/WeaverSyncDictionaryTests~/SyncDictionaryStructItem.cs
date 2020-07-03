using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryStructItem
{
    class SyncDictionaryStructItem : NetworkBehaviour
    {
        MyStructDictionary Foo;

        struct MyStruct
        {
            int potato;
            float floatingpotato;
            double givemetwopotatoes;
        }
        class MyStructDictionary : SyncDictionary<int, MyStruct> { }
    }
}
