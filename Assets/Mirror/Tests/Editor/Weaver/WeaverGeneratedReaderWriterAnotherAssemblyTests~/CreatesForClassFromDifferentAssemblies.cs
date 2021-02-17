using Mirror;
using Mirror.Weaver.Tests.Extra;

namespace GeneratedReaderWriter.CreatesForClassFromDifferentAssemblies
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
