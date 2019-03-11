using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveSameName(int abc) {}

        [Command]
        public void CmdCantHaveSameName(int abc, int def) {}
    }
}
