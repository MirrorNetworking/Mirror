using Mirror;

namespace SyncListNestedStruct
{
    class MyBehaviour : NetworkBehaviour
    {
        MyNestedStructList Foo;

        struct MyNestedStruct
        {
            int potato;
            float floatingpotato;
            double givemetwopotatoes;
        }

        class MyNestedStructList : SyncList<MyNestedStruct> { }
    }
}
