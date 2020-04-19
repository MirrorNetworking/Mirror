using Mirror;
using UnityEngine;

namespace MirrorTest
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
