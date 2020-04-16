using Mirror;
using UnityEngine;

namespace MirrorTest
{
    public class GivesErrorForClassWithNoValidConstructor : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(Material material)
        {
            // empty
        }
    }
}
