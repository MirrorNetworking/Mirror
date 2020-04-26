using Mirror;
using Mirror.Weaver.Tests.Extra;

namespace Mirror.Weaver.Tests.CreatesForClassFromDifferentAssembliesWithValidConstructor
{
    public class CreatesForClassFromDifferentAssembliesWithValidConstructor : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(SomeDataClassWithConstructor data)
        {
            // empty
        }
    }
}