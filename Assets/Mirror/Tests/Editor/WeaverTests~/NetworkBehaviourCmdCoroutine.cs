using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [Command]
        public IEnumerator CmdCantHaveCoroutine()
        {
            yield return null;
        }
    }
}
