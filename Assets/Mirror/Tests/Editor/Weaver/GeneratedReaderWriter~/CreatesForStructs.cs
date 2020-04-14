using Mirror;
using Mirror.Weaver.Tests.Extra;

namespace MirrorTest
{
    public class CreatesForStructs : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(SomeOtherData data)
        {
            // empty
        }
    }

    public struct SomeOtherData
    {
        public int usefulNumber;
    }
}
