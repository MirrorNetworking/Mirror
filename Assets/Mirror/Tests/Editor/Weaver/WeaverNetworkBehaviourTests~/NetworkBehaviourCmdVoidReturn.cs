using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourCmdVoidReturn
{
    class NetworkBehaviourCmdVoidReturn : NetworkBehaviour
    {
        [Command]
        public int CmdCantHaveNonVoidReturn()
        {
            return 1;
        }
    }
}
