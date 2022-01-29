using Mirror;
using Mirror.Weaver.Tests.Extra;

namespace GeneratedReaderWriter.CreatesForTypeThatUsesDifferentAssemblies
{
    public class CreatesForTypeThatUsesDifferentAssemblies : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(DataHolder data)
        {
            // empty
        }
    }
    public struct DataHolder
    {
        public AnotherData another;
        public float q;
    }
}
