using Mirror;
using UnityEngine;

namespace Mirror.Weaver.Tests.GivesErrorWhenUsingMonoBehaviour
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
