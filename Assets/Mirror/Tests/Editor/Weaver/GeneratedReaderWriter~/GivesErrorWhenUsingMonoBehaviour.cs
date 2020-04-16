using Mirror;
using UnityEngine;

namespace MirrorTest
{
    public class GivesErrorWhenUsingMonoBehaviour : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(MonoBehaviour behaviour)
        {
            // empty
        }
    }
}
