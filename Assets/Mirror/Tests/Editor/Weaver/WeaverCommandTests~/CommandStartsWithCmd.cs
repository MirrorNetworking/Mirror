using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.CommandStartsWithCmd
{
    class CommandStartsWithCmd : NetworkBehaviour
    {
        [Command]
        void DoesntStartWithCmd() {}
    }
}
