using Mirror;
using UnityEngine;

namespace Mirror.Weaver.Tests.GivesErrorWhenUsingTypeInheritedFromMonoBehaviour
{
    public class GivesErrorWhenUsingTypeInheritedFromMonoBehaviour : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(MyBehaviour behaviour)
        {
            // empty
        }
    }

    public class MyBehaviour : MonoBehaviour 
    {
        public int usefulNumber;
    }
}
