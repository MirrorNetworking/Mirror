using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.SyncListByteValid
{
    class SyncListByteValid : NetworkBehaviour
    {
        class MyByteClass : SyncList<byte> {};

        MyByteClass Foo;
    }
}
