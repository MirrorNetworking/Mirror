using Mirror;

namespace WeaverSyncListTests.SyncListStruct
{
    class SyncListStruct : NetworkBehaviour
    {
        readonly MyStructList Foo;

        struct MyStruct
        {
            public int potato;
            public float floatingpotato;
            public double givemetwopotatoes;
        }
        class MyStructList : SyncList<MyStruct> { }
    }
}
