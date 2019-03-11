using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [TargetRpc]
        public IEnumerator TargetRpcCantHaveCoroutine()
        {
            yield return null;
        }
    }
}
