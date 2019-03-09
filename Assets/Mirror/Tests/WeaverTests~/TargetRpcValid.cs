using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [TargetRpc]
        private void TargetThatIsTotallyValid(NetworkConnection nc) {}
    }
}
