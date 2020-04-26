using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.SyncListErrorForInterface
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
