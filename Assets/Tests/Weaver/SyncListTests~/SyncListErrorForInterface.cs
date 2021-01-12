using Mirror;

namespace SyncListTests.SyncListErrorForInterface
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
