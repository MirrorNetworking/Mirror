using Mirror;
using UnityEngine;

namespace WeaverClientServerAttributeTests.MonoBehaviourClient
{
    class MonoBehaviourClient : MonoBehaviour
    {
        [Client]
        void ClientOnlyMethod() { }
    }
}
