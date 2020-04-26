using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.SyncListNestedStruct
{
    class SyncListNestedStruct : NetworkBehaviour
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
