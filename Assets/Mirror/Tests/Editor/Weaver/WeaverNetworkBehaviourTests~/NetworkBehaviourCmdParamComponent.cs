using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourCmdParamComponent : NetworkBehaviour
    {
        public class ComponentClass : UnityEngine.Component
        {
            int monkeys = 12;
        }

        [Command]
        public void CmdCantHaveParamComponent(ComponentClass monkeyComp) {}
    }
}
