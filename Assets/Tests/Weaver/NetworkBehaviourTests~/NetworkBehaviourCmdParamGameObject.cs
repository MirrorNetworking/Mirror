using Mirror;
using UnityEngine;

namespace NetworkBehaviourTests.NetworkBehaviourCmdParamGameObject
{
    class NetworkBehaviourCmdParamGameObject : NetworkBehaviour
    {
        [ServerRpc]
        public void CmdCantHaveGameObjectComponent(GameObject monkeyComp) { }
    }
}
