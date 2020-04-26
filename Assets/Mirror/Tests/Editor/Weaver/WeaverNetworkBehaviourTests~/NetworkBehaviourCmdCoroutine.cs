using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourCmdCoroutine
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
