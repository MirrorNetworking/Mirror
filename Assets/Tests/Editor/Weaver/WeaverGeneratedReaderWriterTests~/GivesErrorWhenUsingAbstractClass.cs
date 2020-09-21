using Mirror;

namespace GeneratedReaderWriter.GivesErrorWhenUsingAbstractClass
{
    public class GivesErrorWhenUsingAbstractClass : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(DataBase data)
        {
            // empty
        }
    }

    public abstract class DataBase
    {
        public int someField;
        public abstract int id { get; }
    }
}
