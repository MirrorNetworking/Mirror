using Mirror;

namespace SyncListTests.SyncListStruct
{
    class SyncListStruct : NetworkBehaviour
    {
        MyStructList Foo;

        struct MyStruct
        {
            public int potato;
            public float floatingpotato;
            public double givemetwopotatoes;
        }
        class MyStructList : SyncList<MyStruct> { }
    }
}
