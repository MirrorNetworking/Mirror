using Mirror;

namespace GeneratedReaderWriter.GivesErrorForJaggedArray
{
    public class GivesErrorForJaggedArray : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(int[][] data)
        {
            // empty
        }
    }
}
