using System;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourCmdParamRef
{
    class NetworkBehaviourCmdParamRef : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveParamRef(ref int monkeys) {}
    }
}
