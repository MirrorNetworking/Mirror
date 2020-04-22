using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class ErrorWhenClientRpcDoesntStartWithRpc : NetworkBehaviour
    {
        [ClientRpc]
        void DoesntStartWithRpc() {}
    }
}
