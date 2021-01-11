using Mirror;

namespace WeaverSyncSetTests.SyncSetByteValid
{
    class SyncSetByteValid : NetworkBehaviour
    {
        class MyByteClass : SyncHashSet<byte> { };

        MyByteClass Foo;
    }
}
