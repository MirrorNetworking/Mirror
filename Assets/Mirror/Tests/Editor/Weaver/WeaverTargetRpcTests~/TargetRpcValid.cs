using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class TargetRpcValid : NetworkBehaviour
    {
        [TargetRpc]
        void TargetThatIsTotallyValid(NetworkConnection nc) {}
    }
}
