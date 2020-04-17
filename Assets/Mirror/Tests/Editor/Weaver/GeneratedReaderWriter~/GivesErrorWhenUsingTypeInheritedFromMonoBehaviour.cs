using Mirror;
using UnityEngine;

namespace MirrorTest
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
