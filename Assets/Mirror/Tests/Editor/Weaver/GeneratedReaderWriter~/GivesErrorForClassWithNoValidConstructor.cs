using Mirror;

namespace GeneratedReaderWriter.GivesErrorForClassWithNoValidConstructor
{
    public class GivesErrorForClassWithNoValidConstructor : NetworkBehaviour
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

        public SomeOtherData(int usefulNumber)
        {
            this.usefulNumber = usefulNumber;
        }
    }
}
