using Mirror;

namespace WeaverSyncSetTests.SyncSetStruct
{
    class SyncSetStruct : NetworkBehaviour
    {
        readonly MyStructSet Foo;

        struct MyStruct
        {
            public int potato;
            public float floatingpotato;
            public double givemetwopotatoes;
        }
        class MyStructSet : SyncHashSet<MyStruct> { }
    }
}
