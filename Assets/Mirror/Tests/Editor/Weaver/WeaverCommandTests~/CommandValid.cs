using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.CommandValid
{
    class CommandValid : NetworkBehaviour
    {
        [Command]
        void CmdThatIsTotallyValid() {}
    }
}
