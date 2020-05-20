using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverTargetRpcTests.TargetRpcValid
{
    class TargetRpcValid : NetworkBehaviour
    {
        [TargetRpc]
        void TargetThatIsTotallyValid(NetworkConnection nc) {}
    }
}
