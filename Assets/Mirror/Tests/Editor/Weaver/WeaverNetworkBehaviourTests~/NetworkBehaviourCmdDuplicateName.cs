using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourCmdDuplicateName
{
    class NetworkBehaviourCmdDuplicateName : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveSameName(int abc) {}

        [Command]
        public void CmdCantHaveSameName(int abc, int def) {}
    }
}
