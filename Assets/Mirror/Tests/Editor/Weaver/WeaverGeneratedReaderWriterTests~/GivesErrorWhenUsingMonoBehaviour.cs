using Mirror;
using GodotEngine;

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
