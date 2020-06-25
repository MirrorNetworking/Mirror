using System;
using Mirror.CloudServices.ListServerService;
using UnityEngine;

namespace Mirror.CloudServices.Example
{
    /// <summary>
    /// This component should be put on the NetworkManager object
    /// </summary>
    public class ApiUpdater : MonoBehaviour
    {
        NetworkManagerListServer manager;
        ApiConnector connector;
        public string gameName = "Game";

        void Start()
        {
            manager = NetworkManager.singleton as NetworkManagerListServer;
            connector = manager.GetComponent<ApiConnector>();

            manager.onPlayerListChanged += onPlayerListChanged;
            manager.onServerStarted += ServerStartedHandler;
            manager.onServerStopped += ServerStoppedHandler;
        }


        void OnDestroy()
        {
            manager.onPlayerListChanged -= onPlayerListChanged;
            manager.onServerStarted -= ServerStartedHandler;
            manager.onServerStopped -= ServerStoppedHandler;
        }

        void onPlayerListChanged(int playerCount)
        {
            if (connector.ListServer.ServerApi.ServerInList)
            {
                // update player count so that other players can see
                if (playerCount < 2)
                {
                    connector.ListServer.ServerApi.UpdateServer(playerCount);
                }
                // remove server when there is more thasn 2 players
                else
                {
                    connector.ListServer.ServerApi.RemoveServer();
                }
            }
            else
            {
                // if not in list, and player counts drops below 2, add server to list
                if (playerCount < 2)
                {
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
