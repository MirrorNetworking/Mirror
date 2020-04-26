using Mirror;
using UnityEngine;

namespace Mirror.Weaver.Tests.GivesErrorWhenUsingUnityAsset
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
