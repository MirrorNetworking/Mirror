using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        public abstract class AbstractClass
        {
            int monkeys = 12;
        }

        [TargetRpc]
        public void TargetRpcCantHaveParamAbstract(INetworkConnection monkeyCon, AbstractClass monkeys) { }
    }
}
