using Mirror;

namespace GeneratedReaderWriter.CreatesForClassWithValidConstructor
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
