using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourCmdParamOptional : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveParamOptional(int monkeys = 12) {}
    }
}
