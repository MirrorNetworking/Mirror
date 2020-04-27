using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverMonoBehaviourTests.MonoBehaviourClientRpc
{
    class MonoBehaviourClientRpc : MonoBehaviour
    {
        [ClientRpc]
        void RpcThisCantBeOutsideNetworkBehaviour() {}
    }
}
