using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourClientRpcParamComponent : NetworkBehaviour
    {
        public class ComponentClass : UnityEngine.Component
        {
            int monkeys = 12;
        }

        [ClientRpc]
        public void RpcCantHaveParamComponent(ComponentClass monkeyComp) {}
    }
}
