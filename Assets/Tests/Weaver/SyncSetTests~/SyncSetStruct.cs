using Mirror;

namespace SyncSetTests.SyncSetStruct
{
    class SyncSetStruct : NetworkBehaviour
    {
        MyStructSet Foo;

        struct MyStruct
        {
            public int potato;
            public float floatingpotato;
            public double givemetwopotatoes;
        }
        class MyStructSet : SyncHashSet<MyStruct> { }
    }
}
