using Mirror;
using Mirror.Weaver.Tests.Extra;

namespace MirrorTest
{
    public class CreatesForClass : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(SomeOtherData data)
        {
            // empty
        }
    }

    public class SomeOtherData
    {
        public int usefulNumber;
    }
}
