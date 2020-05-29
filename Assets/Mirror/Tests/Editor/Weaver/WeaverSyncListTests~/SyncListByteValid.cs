using Mirror;

namespace WeaverSyncListTests.SyncListByteValid
{
    class SyncListByteValid : NetworkBehaviour
    {
        class MyByteClass : SyncList<byte> { };

        MyByteClass Foo;
    }
}
