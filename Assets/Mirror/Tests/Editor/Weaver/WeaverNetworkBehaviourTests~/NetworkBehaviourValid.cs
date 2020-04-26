using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourValid
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [SyncVar]
        public int durpatron9000 = 12;
    }
}
