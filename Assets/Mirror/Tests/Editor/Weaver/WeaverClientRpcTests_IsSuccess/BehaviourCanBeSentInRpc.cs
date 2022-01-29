using Mirror;

namespace WeaverClientRpcTests.BehaviourCanBeSentInRpc
{
    class BehaviourCanBeSentInRpc : NetworkBehaviour
    {
        [ClientRpc]
        void RpcDoSomething(BehaviourCanBeSentInRpc value)
        {
            // empty
        }
    }
}
