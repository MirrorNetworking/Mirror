using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourCmdGenericParam : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveGeneric<T>() {}
    }
}
