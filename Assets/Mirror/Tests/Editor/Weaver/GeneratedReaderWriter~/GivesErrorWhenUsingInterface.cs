using Mirror;
using UnityEngine;

namespace GeneratedReaderWriter.GivesErrorWhenUsingInterface
{
    public class GivesErrorWhenUsingInterface : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(IData data)
        {
            // empty
        }
    }

    public interface IData { }
}
