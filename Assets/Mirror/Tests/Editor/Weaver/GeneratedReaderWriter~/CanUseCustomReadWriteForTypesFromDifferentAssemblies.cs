using Mirror;
using Mirror.Weaver.Tests.Extra;

namespace MirrorTest
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