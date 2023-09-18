using Mirror;
using GodotEngine;

namespace WeaverMonoBehaviourTests.MonoBehaviourCommand
{
    class MonoBehaviourCommand : MonoBehaviour
    {
        [Command]
        void CmdThisCantBeOutsideNetworkBehaviour() { }
    }
}
