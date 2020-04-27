using Mirror;
using UnityEngine;

namespace GeneratedReaderWriter.GivesErrorWhenUsingObject
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
