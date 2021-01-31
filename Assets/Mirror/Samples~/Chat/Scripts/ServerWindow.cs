using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mirror.Examples.Chat
{

    public class ServerWindow : MonoBehaviour
    {
        public string serverIp = "localhost";

        public NetworkManager NetworkManager;

        public void StartClient()
        {
            NetworkManager.Client.ConnectAsync(serverIp);
        }

        public void StartHost()
        {
            NetworkManager.Server.StartHost(NetworkManager.Client).Forget();
        }

        public void SetServerIp(string serverIp)
        {
            this.serverIp = serverIp;
        }
    }
}
