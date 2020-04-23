using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class SyncListErrorForInterface : NetworkBehaviour
    {
        MyInterfaceList Foo;
    }
    interface MyInterface
    {
        int someNumber { get; set; }
    }
    class MyInterfaceList : SyncList<MyInterface> { }
}
