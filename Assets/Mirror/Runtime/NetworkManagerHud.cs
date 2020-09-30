using UnityEngine;
using UnityEngine.UI;

namespace Mirror
{
    public class NetworkManagerHud : MonoBehaviour
    {
        public NetworkManager NetworkManager;
        public string NetworkAddress = "localhost";

        [Header("Prefab Canvas Elements")]
        public InputField NetworkAddressInput;
        public GameObject OfflineGO;
        public GameObject OnlineGO;
        public Text StatusLabel;

        internal void OnlineSetActive()
        {
            OfflineGO.SetActive(false);
            OnlineGO.SetActive(true);
        }

        internal void OfflineSetActive()
        {
            OfflineGO.SetActive(true);
            OnlineGO.SetActive(false);
        }

        public void StartHostButtonHandler()
        {
            _ = NetworkManager.StartHost();
            OnlineSetActive();
        }

        public void StartServerOnlyButtonHandler()
        {
            _ = NetworkManager.server.ListenAsync();
            OnlineSetActive();
        }

        public void StartClientButtonHandler()
        {
            NetworkManager.client.ConnectAsync(NetworkAddress);
            OnlineSetActive();
        }

        public void StopButtonHandler()
        {
            NetworkManager.StopHost();
            OfflineSetActive();
        }

        public void OnNetworkAddressInputUpdate()
        {
            NetworkAddress = NetworkAddressInput.text;
        }
    }
}
