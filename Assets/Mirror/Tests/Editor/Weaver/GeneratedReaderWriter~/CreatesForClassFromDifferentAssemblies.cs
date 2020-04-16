using Mirror;
using Mirror.Weaver.Tests.Extra;

namespace MirrorTest
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