using Mirror;
using Mirror.Weaver.Tests.Extra;

namespace Mirror.Weaver.Tests.CreatesForClassFromDifferentAssemblies
{
    public class CreatesForClassFromDifferentAssemblies : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(SomeDataClass data)
        {
            // empty
        }
    }
}