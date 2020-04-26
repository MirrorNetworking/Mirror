using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.SyncListStruct
{
    class SyncListStruct : NetworkBehaviour
    {
        MyStructList Foo;
        
        struct MyStruct
        {
            int potato;
            float floatingpotato;
            double givemetwopotatoes;
        }
        class MyStructList : SyncList<MyStruct> { }
    }
}
