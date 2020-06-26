using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Cloud.Example
{
    /// <summary>
    /// Uses the ApiConnector on NetworkManager to update the Server list
    /// </summary>
    public class ServerListManager : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] ServerListUI listUI = null;

        [Header("Buttons")]
        [SerializeField] Button refreshButton = null;
        [SerializeField] Button startServerButton = null;


        [Header("Auto Refresh")]
        [SerializeField] bool autoRefreshServerlist = false;
        [SerializeField] int refreshinterval = 20;

        ApiConnector connector;

        void Start()
        {
            NetworkManager manager = NetworkManager.singleton;
            connector = manager.GetComponent<ApiConnector>();

            connector.ListServer.ClientApi.onServerListUpdated += listUI.UpdateList;

            if (autoRefreshServerlist)
            {
                connector.ListServer.ClientApi.StartGetServerListRepeat(refreshinterval);
            }

            AddButtonHandlers();
        }

        void AddButtonHandlers()
        {
            refreshButton.onClick.AddListener(RefreshButtonHandler);
            startServerButton.onClick.AddListener(StartServerButtonHandler);
        }

        void OnDestroy()
        {
            if (connector == null)
                return;

            if (autoRefreshServerlist)
            {
                connector.ListServer.ClientApi.StopGetServerListRepeat();
            }

            connector.ListServer.ClientApi.onServerListUpdated -= listUI.UpdateList;
        }

        public void RefreshButtonHandler()
        {
            connector.ListServer.ClientApi.GetServerList();
        }

        public void StartServerButtonHandler()
        {
            NetworkManager.singleton.StartServer();
        }
    }
}
