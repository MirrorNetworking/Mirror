using Mirror;
using Mirror.Weaver.Tests.Extra;

namespace GeneratedReaderWriter.CreatesForComplexTypeFromDifferentAssemblies
{
    public class CreatesForComplexTypeFromDifferentAssemblies : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(ComplexData data)
        {
            // empty
        }
    }
}
