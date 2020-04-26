using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourCmdCoroutine : NetworkBehaviour
    {
        [Command]
        public IEnumerator CmdCantHaveCoroutine()
        {
            yield return null;
        }
    }
}
