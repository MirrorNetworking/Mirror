using Mirror;
using UnityEngine;

namespace MirrorTest
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
