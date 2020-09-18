using System;
using Mirror.Cloud.ListServerService;
using UnityEngine;

namespace Mirror.Cloud.Example
{
    /// <summary>
    /// This component should be put on the NetworkManager object
    /// </summary>
    public class ApiUpdater : MonoBehaviour
    {
        static readonly ILogger logger = LogFactory.GetLogger<ApiUpdater>();

        [SerializeField] NetworkManagerListServer manager;
        [SerializeField] ApiConnector connector;
        public string gameName = "Game";

        void Awake()
        {
            if (manager == null)
            {
                manager = FindObjectOfType<NetworkManagerListServer>();
            }
            if (connector == null)
            {
                connector = manager.GetComponent<ApiConnector>();
            }

            Debug.Assert(manager != null, "ApiUpdater could not find NetworkManagerListServer");
            Debug.Assert(connector != null, "ApiUpdater could not find ApiConnector");

            manager.onPlayerListChanged += onPlayerListChanged;
            manager.onServerStarted += ServerStartedHandler;
            manager.onServerStopped += ServerStoppedHandler;
        }

        void OnDestroy()
        {
            if (manager != null)
            {
                manager.onPlayerListChanged -= onPlayerListChanged;
                manager.onServerStarted -= ServerStartedHandler;
                manager.onServerStopped -= ServerStoppedHandler;
            }
        }

        void onPlayerListChanged(int playerCount)
        {
            if (connector.ListServer.ServerApi.ServerInList)
            {
                // update player count so that other players can see
                if (playerCount < manager.maxConnections)
                {
                    if (logger.LogEnabled()) logger.Log($"Updating Server, player count: {playerCount} ");
                    connector.ListServer.ServerApi.UpdateServer(playerCount);
                }
                // remove server when there is max players
                else
                {
                    if (logger.LogEnabled()) logger.Log($"Removing Server, player count: {playerCount}");
                    connector.ListServer.ServerApi.RemoveServer();
                }
            }
            else
            {
                // if not in list, and player counts drops below 2, add server to list
                if (playerCount < 2)
                {
                    if (logger.LogEnabled()) logger.Log($"Adding Server, player count: {playerCount}");
                    AddServer(playerCount);
                }
            }
        }

        void ServerStartedHandler()
        {
            AddServer(0);
        }

        void AddServer(int playerCount)
        {
            Transport transport = Transport.activeTransport;

            Uri uri = transport.ServerUri();
            int port = uri.Port;
            string protocol = uri.Scheme;

            connector.ListServer.ServerApi.AddServer(new ServerJson
            {
                displayName = $"{gameName} {(UnityEngine.Random.value * 1000).ToString("0")}",
                protocol = protocol,
                port = port,
                maxPlayerCount = NetworkManager.singleton.maxConnections,
                playerCount = playerCount
            });
        }

        void ServerStoppedHandler()
        {
            connector.ListServer.ServerApi.RemoveServer();
        }
    }
}
