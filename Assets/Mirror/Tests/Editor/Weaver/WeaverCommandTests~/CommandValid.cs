using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverCommandTests.CommandValid
{
    class CommandValid : NetworkBehaviour
    {
        [Command]
        void CmdThatIsTotallyValid() {}
    }
}
