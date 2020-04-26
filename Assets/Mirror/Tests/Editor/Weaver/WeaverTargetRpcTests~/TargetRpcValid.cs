using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.TargetRpcValid
{
    class TargetRpcValid : NetworkBehaviour
    {
        [TargetRpc]
        void TargetThatIsTotallyValid(NetworkConnection nc) {}
    }
}
