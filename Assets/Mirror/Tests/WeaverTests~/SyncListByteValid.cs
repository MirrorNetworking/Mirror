using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        class MyByteClass : SyncList<byte> {};

        MyByteClass Foo;
    }
}
