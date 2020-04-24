using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class CommandStartsWithCmd : NetworkBehaviour
    {
        [Command]
        void DoesntStartWithCmd() {}
    }
}
