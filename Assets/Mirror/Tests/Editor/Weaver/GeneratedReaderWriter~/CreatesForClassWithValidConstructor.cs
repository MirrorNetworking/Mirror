using Mirror;
using Mirror.Weaver.Tests.Extra;

namespace Mirror.Weaver.Tests.CreatesForClassWithValidConstructor
{
    public class CreatesForClassWithValidConstructor : NetworkBehaviour
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

        public SomeOtherData()
        {
            // empty
        } 
    }
}
