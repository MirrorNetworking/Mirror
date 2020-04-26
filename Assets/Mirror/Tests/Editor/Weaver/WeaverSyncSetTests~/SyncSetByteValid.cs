using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.SyncSetByteValid
{
    class SyncSetByteValid : NetworkBehaviour
    {
        class MyByteClass : SyncHashSet<byte> {};

        MyByteClass Foo;
    }
}
