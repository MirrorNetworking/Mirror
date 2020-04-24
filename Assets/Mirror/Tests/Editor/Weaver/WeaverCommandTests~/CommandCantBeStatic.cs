using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class CommandCantBeStatic : NetworkBehaviour
    {
        [Command]
        static void CmdCantBeStatic() {}
    }
}
