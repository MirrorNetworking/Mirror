using Mirror;
using Mirror.Weaver.Tests.Extra;

namespace Mirror.Weaver.Tests.CanUseCustomReadWriteForTypesFromDifferentAssemblies
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