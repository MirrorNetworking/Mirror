using Mirror;
using UnityEngine;

namespace MirrorTest
{
    public class GivesErrorWhenUsingObject : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(Object obj)
        {
            // empty
        }
    }
}
