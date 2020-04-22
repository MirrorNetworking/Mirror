using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MonoBehaviourClientRpc : MonoBehaviour
    {
        [ClientRpc]
        void RpcThisCantBeOutsideNetworkBehaviour() {}
    }
}
