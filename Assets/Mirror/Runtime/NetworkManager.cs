using UnityEngine;

namespace Mirror
{

    [AddComponentMenu("Network/NetworkManager")]
    [HelpURL("https://mirror-networking.com/docs/Components/NetworkManager.html")]
    [RequireComponent(typeof(NetworkServer))]
    [RequireComponent(typeof(NetworkClient))]
    [DisallowMultipleComponent]
    public class NetworkManager : MonoBehaviour
    {
        public NetworkServer server;
        public NetworkClient client;

        /// <summary>
        /// True if the server or client is started and running
        /// <para>This is set True in StartServer / StartClient, and set False in StopServer / StopClient</para>
        /// </summary>
        public bool IsNetworkActive => server.Active || client.Active;
    }
}
