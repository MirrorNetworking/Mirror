using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.CommandCantBeStatic
{
    class CommandCantBeStatic : NetworkBehaviour
    {
        [Command]
        static void CmdCantBeStatic() {}
    }
}
