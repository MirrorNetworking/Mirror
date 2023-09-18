using Mirror;
using GodotEngine;

namespace GeneratedReaderWriter.GivesErrorForInvalidArrayType
{
    public class GivesErrorForInvalidArrayType : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(MonoBehaviour[] data)
        {
            // empty
        }
    }
}
