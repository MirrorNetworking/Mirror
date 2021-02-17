using Mirror;
using UnityEngine;

namespace WeaverClientServerAttributeTests.MonoBehaviourServer
{
    class MonoBehaviourServer : MonoBehaviour
    {
        [Server]
        void ServerOnlyMethod() { }
    }
}
