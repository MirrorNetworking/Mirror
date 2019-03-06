using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : MonoBehaviour
    {
        [ClientRpc]
        void RpcThisCantBeOutsideNetworkBehaviour() {}
    }
}
