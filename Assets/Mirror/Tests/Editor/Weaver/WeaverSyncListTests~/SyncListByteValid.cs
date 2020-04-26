using UnityEngine;
using Mirror;

namespace SyncListByteValid
{
    class MyBehaviour : NetworkBehaviour
    {
        class MyByteClass : SyncList<byte> {};

        MyByteClass Foo;
    }
}
