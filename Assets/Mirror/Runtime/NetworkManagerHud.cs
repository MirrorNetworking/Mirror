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

        private void Start()
        {
            DontDestroyOnLoad(transform.root.gameObject);
            Application.runInBackground = true;
        }

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
            StatusLabel.text = "Host Mode";
            _ = NetworkManager.StartHost();
            OnlineSetActive();
        }

        public void StartServerOnlyButtonHandler()
        {
            StatusLabel.text = "Server Mode";
            _ = NetworkManager.server.ListenAsync();
            OnlineSetActive();
        }

        public void StartClientButtonHandler()
        {
            StatusLabel.text = "Client Mode";
            NetworkManager.client.ConnectAsync(NetworkAddress);
            OnlineSetActive();
        }

        public void StopButtonHandler()
        {
            StatusLabel.text = string.Empty;
            NetworkManager.StopHost();
            OfflineSetActive();
        }

        public void OnNetworkAddressInputUpdate()
        {
            NetworkAddress = NetworkAddressInput.text;
        }
    }
}
