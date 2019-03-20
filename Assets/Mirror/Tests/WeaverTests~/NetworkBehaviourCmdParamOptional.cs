using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveParamOptional(int monkeys = 12) {}
    }
}
