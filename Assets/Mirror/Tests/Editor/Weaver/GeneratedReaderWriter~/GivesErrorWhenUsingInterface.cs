using Mirror;
using UnityEngine;

namespace Mirror.Weaver.Tests.GivesErrorWhenUsingInterface
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
