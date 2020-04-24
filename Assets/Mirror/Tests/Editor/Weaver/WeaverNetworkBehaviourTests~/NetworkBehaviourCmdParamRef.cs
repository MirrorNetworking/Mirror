using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourCmdParamRef : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveParamRef(ref int monkeys) {}
    }
}
