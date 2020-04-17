using Mirror;
using Mirror.Weaver.Tests.Extra;

namespace MirrorTest
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