using System;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourCmdParamOptional
{
    class NetworkBehaviourCmdParamOptional : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveParamOptional(int monkeys = 12) {}
    }
}
