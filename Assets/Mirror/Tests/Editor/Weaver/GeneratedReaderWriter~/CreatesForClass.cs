using Mirror;
using Mirror.Weaver.Tests.Extra;

namespace Mirror.Weaver.Tests.CreatesForClass
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
