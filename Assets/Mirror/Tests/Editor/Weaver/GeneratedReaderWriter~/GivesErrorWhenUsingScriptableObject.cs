using Mirror;
using UnityEngine;

namespace GeneratedReaderWriter.GivesErrorWhenUsingScriptableObject
{
    public class GivesErrorWhenUsingScriptableObject : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(ScriptableObject obj)
        {
            // empty
        }
    }
}
