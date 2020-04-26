using Mirror;
using UnityEngine;

namespace Mirror.Weaver.Tests.GivesErrorWhenUsingObject
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
