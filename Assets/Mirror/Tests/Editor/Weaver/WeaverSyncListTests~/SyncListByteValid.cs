using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class SyncListByteValid : NetworkBehaviour
    {
        class MyByteClass : SyncList<byte> {};

        MyByteClass Foo;
    }
}
