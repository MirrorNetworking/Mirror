using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class CommandValid : NetworkBehaviour
    {
        [Command]
        void CmdThatIsTotallyValid() {}
    }
}
