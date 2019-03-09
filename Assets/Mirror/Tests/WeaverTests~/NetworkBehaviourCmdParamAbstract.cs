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

        [Command]
        public void CmdCantHaveParamAbstract(AbstractClass monkeys) {}
    }
}
