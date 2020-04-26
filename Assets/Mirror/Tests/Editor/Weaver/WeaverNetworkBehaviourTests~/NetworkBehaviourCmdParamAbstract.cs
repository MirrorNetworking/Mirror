using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourCmdParamAbstract : NetworkBehaviour
    {
        public abstract class AbstractClass
        {
            int monkeys = 12;
        }

        [Command]
        public void CmdCantHaveParamAbstract(AbstractClass monkeys) {}
    }
}
