using Mirror;
using UnityEngine;

namespace Mirror.Weaver.Tests.GivesErrorWhenUsingScriptableObject
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
