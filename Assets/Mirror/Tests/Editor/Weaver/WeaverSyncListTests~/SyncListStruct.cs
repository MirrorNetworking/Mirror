using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class SyncListStruct : NetworkBehaviour
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
