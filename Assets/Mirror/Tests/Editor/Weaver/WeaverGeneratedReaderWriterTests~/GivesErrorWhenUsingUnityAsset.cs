using Mirror;
using GodotEngine;

namespace GeneratedReaderWriter.GivesErrorWhenUsingGodotAsset
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
