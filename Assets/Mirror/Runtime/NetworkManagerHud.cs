using Cysharp.Threading.Tasks;
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
        string labelText;

        private void Start()
        {
            DontDestroyOnLoad(transform.root.gameObject);
            Application.runInBackground = true;
        }

        private void Update()
        {
            if (StatusLabel) StatusLabel.text = labelText;
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
            labelText = "Host Mode";
            NetworkManager.Server.StartHost(NetworkManager.Client).Forget();
            OnlineSetActive();
        }

        public void StartServerOnlyButtonHandler()
        {
            labelText = "Server Mode";
            NetworkManager.Server.ListenAsync().Forget();
            OnlineSetActive();
        }

        public void StartClientButtonHandler()
        {
            labelText = "Client Mode";
            NetworkManager.Client.ConnectAsync(NetworkAddress).Forget();
            OnlineSetActive();
        }

        public void StopButtonHandler()
        {
            labelText = string.Empty;
            NetworkManager.Server.StopHost();
            NetworkManager.Client.Disconnect();
            OfflineSetActive();
        }

        public void OnNetworkAddressInputUpdate()
        {
            NetworkAddress = NetworkAddressInput.text;
        }
    }
}
