using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

namespace Mirror.Examples.Common
{
    // Note: EventSystem is needed in your scene for Unitys UI Canvas
    [AddComponentMenu("")]
    public class CanvasNetworkManagerHUD : MonoBehaviour
    {
        [SerializeField] private GameObject startButtonsGroup;
        [SerializeField] private GameObject statusLabelsGroup;

        [SerializeField] private Button startHostButton;
        [SerializeField] private Button startServerOnlyButton;
        [SerializeField] private Button startClientButton;

        [SerializeField] private Button mainStopButton;
        [SerializeField] private Text mainStopButtonText;
        [SerializeField] private Button secondaryStopButton;
        [SerializeField] private Text statusText;

        [SerializeField] private InputField inputNetworkAddress;
        [SerializeField] private InputField inputPort;

        private void Start()
        {
            // Init the input field with Network Manager's network address.
            inputNetworkAddress.text = NetworkManager.singleton.networkAddress;
            GetPort();

            RegisterListeners();

            //RegisterClientEvents();

            CheckWebGLPlayer();
        }

        private void RegisterListeners()
        {
            // Add button listeners. These buttons are already added in the inspector.
            startHostButton.onClick.AddListener(OnClickStartHostButton);
            startServerOnlyButton.onClick.AddListener(OnClickStartServerButton);
            startClientButton.onClick.AddListener(OnClickStartClientButton);
            mainStopButton.onClick.AddListener(OnClickMainStopButton);
            secondaryStopButton.onClick.AddListener(OnClickSecondaryStopButton);

            // Add input field listener to update NetworkManager's Network Address
            // when changed.
            inputNetworkAddress.onValueChanged.AddListener(delegate { OnNetworkAddressChange(); });
            inputPort.onValueChanged.AddListener(delegate { OnPortChange(); });
        }

        // Not working at the moment. Can't register events.
        /*private void RegisterClientEvents()
        {
            NetworkClient.OnConnectedEvent += OnClientConnect;
            NetworkClient.OnDisconnectedEvent += OnClientDisconnect;
        }*/

        private void CheckWebGLPlayer()
        {
            // WebGL can't be host or server.
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                startHostButton.interactable = false;
                startServerOnlyButton.interactable = false;
            }
        }

        private void RefreshHUD()
        {
            if (!NetworkServer.active && !NetworkClient.isConnected)
            {
                StartButtons();
            }
            else
            {
                StatusLabelsAndStopButtons();
            }
        }

        private void StartButtons()
        {
            if (!NetworkClient.active)
            {
                statusLabelsGroup.SetActive(false);
                startButtonsGroup.SetActive(true);
            }
            else
            {
                ShowConnectingStatus();
            }
        }

        private void StatusLabelsAndStopButtons()
        {
            startButtonsGroup.SetActive(false);
            statusLabelsGroup.SetActive(true);

            // Host
            if (NetworkServer.active && NetworkClient.active)
            {
                statusText.text = $"<b>Host</b>: running via {Transport.active}";

                mainStopButtonText.text = "Stop Client";
            }
            // Server only
            else if (NetworkServer.active)
            {
                statusText.text = $"<b>Server</b>: running via {Transport.active}";

                mainStopButtonText.text = "Stop Server";
            }
            // Client only
            else if (NetworkClient.isConnected)
            {
                statusText.text = $"<b>Client</b>: connected to {NetworkManager.singleton.networkAddress} via {Transport.active}";

                mainStopButtonText.text = "Stop Client";
            }

            // Note secondary button is only used to Stop Host, and is only needed in host mode.
            secondaryStopButton.gameObject.SetActive(NetworkServer.active && NetworkClient.active);
        }

        private void ShowConnectingStatus()
        {
            startButtonsGroup.SetActive(false);
            statusLabelsGroup.SetActive(true);

            secondaryStopButton.gameObject.SetActive(false);

            statusText.text = "Connecting to " + NetworkManager.singleton.networkAddress + "..";
            mainStopButtonText.text = "Cancel Connection Attempt";
        }

        private void OnClickStartHostButton()
        {
            NetworkManager.singleton.StartHost();
        }

        private void OnClickStartServerButton()
        {
            NetworkManager.singleton.StartServer();
        }

        private void OnClickStartClientButton()
        {
            NetworkManager.singleton.StartClient();
            //ShowConnectingStatus();
        }

        private void OnClickMainStopButton()
        {
            if (NetworkClient.active)
            {
                NetworkManager.singleton.StopClient();
            }
            else
            {
                NetworkManager.singleton.StopServer();
            }
        }

        private void OnClickSecondaryStopButton()
        {
            NetworkManager.singleton.StopHost();
        }

        private void OnNetworkAddressChange()
        {
            NetworkManager.singleton.networkAddress = inputNetworkAddress.text;
        }

        private void OnPortChange()
        {
            SetPort(inputPort.text);
        }

        private void SetPort(string _port)
        {
            // only show a port field if we have a port transport
            // we can't have "IP:PORT" in the address field since this only
            // works for IPV4:PORT.
            // for IPV6:PORT it would be misleading since IPV6 contains ":":
            // 2001:0db8:0000:0000:0000:ff00:0042:8329
            if (Transport.active is PortTransport portTransport)
            {
                // use TryParse in case someone tries to enter non-numeric characters
                if (ushort.TryParse(_port, out ushort port))
                    portTransport.Port = port;
            }
        }

        private void GetPort()
        {
            if (Transport.active is PortTransport portTransport)
            {
                inputPort.text = portTransport.Port.ToString();
            }
        }

        private void Update()
        {
            RefreshHUD();
        }

        /* This does not work because we can't register the handler.
        void OnClientConnect() {}

        private void OnClientDisconnect()
        {
            RefreshHUD();
        }
        */

        // Do a check for the presence of a Network Manager component when
        // you first add this script to a gameobject.
        private void Reset()
        {
#if UNITY_2022_2_OR_NEWER
            if (!FindAnyObjectByType<NetworkManager>())
                Debug.LogError("This component requires a NetworkManager component to be present in the scene. Please add!");
#else
            // Deprecated in Unity 2023.1
            if (!FindObjectOfType<NetworkManager>())
                Debug.LogError("This component requires a NetworkManager component to be present in the scene. Please add!");
#endif
        }
    }
}
