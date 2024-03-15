using Edgegap;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.EdgegapLobby
{
    public class UILobbyCreate : MonoBehaviour
    {
        public UILobbyList List;
        public Button CancelButton;
        public InputField LobbyName;
        public Text SlotCount;
        public Slider SlotSlider;
        public Button HostButton;
        public Button ServerButton;
        private EdgegapLobbyKcpTransport _transport => (EdgegapLobbyKcpTransport)NetworkManager.singleton.transport;

        private void Awake()
        {
            ValidateName();
            LobbyName.onValueChanged.AddListener(_ =>
            {
                ValidateName();
            });
            CancelButton.onClick.AddListener(() =>
            {
                List.gameObject.SetActive(true);
                gameObject.SetActive(false);
            });
            SlotSlider.onValueChanged.AddListener(arg0 =>
            {
                SlotCount.text = ((int)arg0).ToString();
            });
            HostButton.onClick.AddListener(() =>
            {
                gameObject.SetActive(false);
                _transport.SetServerLobbyParams(LobbyName.text, (int)SlotSlider.value);
                NetworkManager.singleton.StartHost();
            });
            ServerButton.onClick.AddListener(() =>
            {
                gameObject.SetActive(false);
                _transport.SetServerLobbyParams(LobbyName.text, (int)SlotSlider.value);
                NetworkManager.singleton.StartServer();
            });
        }
        void ValidateName()
        {
            bool valid = !string.IsNullOrWhiteSpace(LobbyName.text);
            HostButton.interactable = valid;
            ServerButton.interactable = valid;
        }
    }
}
