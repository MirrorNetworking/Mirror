using UnityEngine;

namespace Mirror.Examples.Chat
{

    public class ServerWindow : MonoBehaviour
    {
        public string serverIp = "localhost";

        public NetworkManager NetworkManager;

        public void StartClient()
        {
            NetworkManager.StartClient(serverIp);
        }

        public void StartHost()
        {
            _ = NetworkManager.StartHost();
        }

        public void SetServerIp(string serverIp)
        {
            this.serverIp = serverIp;
        }
    }
}
