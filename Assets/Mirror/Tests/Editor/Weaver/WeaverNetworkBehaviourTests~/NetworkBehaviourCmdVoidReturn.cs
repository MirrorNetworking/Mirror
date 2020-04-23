using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
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
