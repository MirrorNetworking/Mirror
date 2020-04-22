using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class ClientRpcStartsWithRpc : NetworkBehaviour
    {
        [ClientRpc]
        void DoesntStartWithRpc() {}
    }
}
