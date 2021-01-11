using Mirror;
using Mirror.Weaver.Tests.Extra;

namespace GeneratedReaderWriter.CreatesForClassFromDifferentAssembliesWithValidConstructor
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