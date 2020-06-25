using Mirror.Cloud.ListServerService;
using UnityEngine;

namespace Mirror.Cloud
{
    /// <summary>
    /// Used to requests and responses from the mirror api
    /// </summary>
    public interface IApiConnector
    {
        ListServer ListServer { get; }
    }

    /// <summary>
    /// Used to requests and responses from the mirror api
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/CloudServices/ApiConnector")]
    [HelpURL("https://mirror-networking.com/docs/CloudServices/ApiConnector.html")]
    public class ApiConnector : MonoBehaviour, IApiConnector, ICoroutineRunner
    {
        #region Inspector
        [Header("Settings")]

        [Tooltip("Base URL of api, including https")]
        [SerializeField] string ApiAddress = "";

        [Tooltip("Api key required to access api")]
        [SerializeField] string ApiKey = "";

        [Header("Events")]

        [Tooltip("Triggered when server list updates")]
        [SerializeField] ServerListEvent _onServerListUpdated = new ServerListEvent();
        #endregion

        IRequestCreator requestCreator;

        public ListServer ListServer { get; private set; }

        void Awake()
        {
            requestCreator = new RequestCreator(ApiAddress, ApiKey, this);

            InitListServer();
        }

        void InitListServer()
        {
            IListServerServerApi serverApi = new ListServerServerApi(this, requestCreator);
            IListServerClientApi clientApi = new ListServerClientApi(this, requestCreator, _onServerListUpdated);
            ListServer = new ListServer(serverApi, clientApi);
        }

        public void OnDestroy()
        {
            ListServer.ServerApi.Shutdown();
            ListServer.ClientApi.Shutdown();
        }
    }
}
