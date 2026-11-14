using System;
using Edgegap;
using UnityEngine;
using UnityEngine.UI;
namespace Mirror.Examples.EdgegapLobby
{
    public class UILobbyStatus : MonoBehaviour
    {
        public GameObject[] ShowDisconnected;
        public GameObject[] ShowServer;
        public GameObject[] ShowHost;
        public GameObject[] ShowClient;
        public Button StopServer;
        public Button StopHost;
        public Button StopClient;
        public Text StatusText;
        private Status _status;
        private EdgegapLobbyKcpTransport _transport;
        enum Status
        {
            Offline,
            Server,
            Host,
            Client
        }
        void Awake()
        {
            Refresh();
            StopServer.onClick.AddListener(() =>
            {
                NetworkManager.singleton.StopServer();
            });
            StopHost.onClick.AddListener(() =>
            {
                NetworkManager.singleton.StopHost();
            });
            StopClient.onClick.AddListener(() =>
            {
                NetworkManager.singleton.StopClient();
            });
        }
        private void Start()
        {
            _transport = (EdgegapLobbyKcpTransport)NetworkManager.singleton.transport;
        }
        private void Update()
        {
            var status = GetStatus();
            if (_status != status)
            {
                _status = status;
                Refresh();
            }
            if (_transport)
            {
                StatusText.text = _transport.Status.ToString();
            }
            else
            {
                StatusText.text = "";
            }
        }
        private void Refresh()
        {
            switch (_status)
            {

                case Status.Offline:
                    SetUI(ShowServer, false);
                    SetUI(ShowHost, false);
                    SetUI(ShowClient, false);
                    SetUI(ShowDisconnected, true);
                    break;
                case Status.Server:
                    SetUI(ShowDisconnected, false);
                    SetUI(ShowHost, false);
                    SetUI(ShowClient, false);
                    SetUI(ShowServer, true);
                    break;
                case Status.Host:
                    SetUI(ShowDisconnected, false);
                    SetUI(ShowServer, false);
                    SetUI(ShowClient, false);
                    SetUI(ShowHost, true);
                    break;
                case Status.Client:
                    SetUI(ShowDisconnected, false);
                    SetUI(ShowServer, false);
                    SetUI(ShowHost, false);
                    SetUI(ShowClient, true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void SetUI(GameObject[] gos, bool active)
        {
            foreach (GameObject go in gos)
            {
                go.SetActive(active);
            }
        }
        private Status GetStatus()
        {
            if (NetworkServer.active && NetworkClient.active)
            {
                return Status.Host;
            }
            if (NetworkServer.active)
            {
                return Status.Server;
            }
            if (NetworkClient.active)
            {
                return Status.Client;
            }
            return Status.Offline;
        }
    }
}
