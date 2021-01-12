using Mirror;
using Mirror.Weaver.Extra;

namespace GeneratedReaderWriter.CanUseCustomReadWriteForTypesFromDifferentAssemblies
{
    public class CanUseCustomReadWriteForTypesFromDifferentAssemblies : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(SomeDataWithWriter data)
        {
            // empty
        }
    }
}
