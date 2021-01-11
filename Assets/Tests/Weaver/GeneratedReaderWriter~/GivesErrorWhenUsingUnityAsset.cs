using Mirror;
using UnityEngine;

namespace GeneratedReaderWriter.GivesErrorWhenUsingUnityAsset
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
