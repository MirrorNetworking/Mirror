using Mirror;
using UnityEngine;

namespace WeaverMonoBehaviourTests.MonoBehaviourCommand
{
    class MonoBehaviourCommand : MonoBehaviour
    {
        [Command]
        void CmdThisCantBeOutsideNetworkBehaviour() { }
    }
}
