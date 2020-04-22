using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class SyncSetByteValid : NetworkBehaviour
    {
        class MyByteClass : SyncHashSet<byte> {};

        MyByteClass Foo;
    }
}
