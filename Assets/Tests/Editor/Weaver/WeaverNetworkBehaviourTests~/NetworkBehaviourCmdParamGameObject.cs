using Mirror;
using UnityEngine;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamGameObject
{
    class NetworkBehaviourCmdParamGameObject : NetworkBehaviour
    {
        [ServerRpc]
        public void CmdCantHaveGameObjectComponent(GameObject monkeyComp) { }
    }
}
