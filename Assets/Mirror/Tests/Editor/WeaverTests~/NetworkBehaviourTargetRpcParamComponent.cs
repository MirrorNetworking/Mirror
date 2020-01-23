using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        public class ComponentClass : UnityEngine.Component
        {
            int monkeys = 12;
        }

        [TargetRpc]
        public void TargetRpcCantHaveParamComponent(NetworkConnection monkeyCon, ComponentClass monkeyComp) {}
    }
}
