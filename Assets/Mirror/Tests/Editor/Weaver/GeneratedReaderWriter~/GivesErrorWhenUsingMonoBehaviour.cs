using Mirror;
using UnityEngine;

namespace GeneratedReaderWriter.GivesErrorWhenUsingMonoBehaviour
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
