using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [Command]
        public int CmdCantHaveNonVoidReturn()
        {
            return 1;
        }
    }
}
