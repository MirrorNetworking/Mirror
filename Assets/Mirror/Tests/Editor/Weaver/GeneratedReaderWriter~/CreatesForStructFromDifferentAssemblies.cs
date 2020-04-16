using Mirror;
using Mirror.Weaver.Tests.Extra;

namespace MirrorTest
{
    public class CreatesForStructFromDifferentAssemblies : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(SomeData data)
        {
            // empty
        }
    }
}