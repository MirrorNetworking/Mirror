using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourCmdParamOut : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveParamOut(out int monkeys)
        {
            monkeys = 12;
        }
    }
}
