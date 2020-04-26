using System;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourCmdParamOut
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
