using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourCmdGenericParam
{
    class NetworkBehaviourCmdGenericParam : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveGeneric<T>() {}
    }
}
