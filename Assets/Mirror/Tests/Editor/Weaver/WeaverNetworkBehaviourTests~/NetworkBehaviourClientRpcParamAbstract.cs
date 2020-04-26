using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourClientRpcParamAbstract : NetworkBehaviour
    {
        public abstract class AbstractClass
        {
            int monkeys = 12;
        }

        [ClientRpc]
        public void RpcCantHaveParamAbstract(AbstractClass monkeys) {}
    }
}
