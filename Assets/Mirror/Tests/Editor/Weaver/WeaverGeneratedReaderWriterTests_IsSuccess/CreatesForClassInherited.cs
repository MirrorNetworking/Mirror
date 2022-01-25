using Mirror;


namespace GeneratedReaderWriter.CreatesForClassInherited
{
    public class CreatesForClassInherited : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(SomeOtherData data)
        {
            // empty
        }
    }

    public class BaseData
    {
        public bool yes;
    }
    public class SomeOtherData : BaseData
    {
        public int usefulNumber;
    }
}
