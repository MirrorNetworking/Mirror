using Mirror;

namespace SyncListStruct
{
    class MyBehaviour : NetworkBehaviour
    {
        MyStructList Foo;
    }

    struct MyStruct
    {
        int potato;
        float floatingpotato;
        double givemetwopotatoes;
    }

    class MyStructList : SyncList<MyStruct> { }
}
