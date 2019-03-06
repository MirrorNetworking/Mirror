using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [TargetRpc]
        public int TargetRpcCantHaveNonVoidReturn()
        {
            return 1;
        }
    }
}
