using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdGenericParam
{
    class NetworkBehaviourCmdGenericParam : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveGeneric<T>() {}
    }
}
