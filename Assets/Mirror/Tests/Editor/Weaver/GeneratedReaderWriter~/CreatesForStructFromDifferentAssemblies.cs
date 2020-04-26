using Mirror;
using Mirror.Weaver.Tests.Extra;

namespace Mirror.Weaver.Tests.CreatesForStructFromDifferentAssemblies
{
    public class CreatesForStructFromDifferentAssemblies : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(SomeData data)
        {
            // empty
        }
    }
}