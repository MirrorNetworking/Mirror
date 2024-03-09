using System;
using Edgegap;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;
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
        private LobbyCreateRequest Request => new LobbyCreateRequest
        {
            player = new LobbyCreateRequest.Player
            {
                id = $"{Random.Range(0, int.MaxValue)}",
            },
            annotations = new LobbyCreateRequest.Annotation[]
            {
            },
            capacity = (int)SlotSlider.value,
            is_joinable = true,
            name = LobbyName.text,
            tags = new string[]
            {
                "test"
            }
        };

        private void Awake()
        {
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
                _transport.CreateLobbyAndStartServer(Request, true);
            });
            ServerButton.onClick.AddListener(() =>
            {
                gameObject.SetActive(false);
                _transport.CreateLobbyAndStartServer(Request, false);
            });
        }
    }
}
