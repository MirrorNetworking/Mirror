using Mirror;
using GodotEngine;

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
