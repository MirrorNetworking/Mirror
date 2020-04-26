using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourClientRpcVoidReturn
{
    class NetworkBehaviourClientRpcVoidReturn : NetworkBehaviour
    {
        [ClientRpc]
        public int RpcCantHaveNonVoidReturn()
        {
            return 1;
        }
    }
}
