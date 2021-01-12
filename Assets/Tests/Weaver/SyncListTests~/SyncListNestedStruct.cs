using Mirror;

namespace SyncListTests.SyncListNestedStruct
{
    class SyncListNestedStruct : NetworkBehaviour
    {
        MyNestedStructList Foo;

        struct MyNestedStruct
        {
            public int potato;
            public float floatingpotato;
            public double givemetwopotatoes;
        }
        class MyNestedStructList : SyncList<MyNestedStruct> { }
    }
}
