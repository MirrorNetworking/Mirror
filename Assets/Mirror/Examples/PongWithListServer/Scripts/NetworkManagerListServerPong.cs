using System;
using UnityEngine;

namespace Mirror.CloudServices.Example
{
    public class NetworkManagerListServerPong : NetworkManager
    {
        /// <summary>
        /// Called when Server Starts
        /// </summary>
        public event Action onServerStarted;

        /// <summary>
        /// Called when Server Stops
        /// </summary>
        public event Action onServerStopped;

        /// <summary>
        /// Called when players leaves or joins the room
        /// </summary>
        public event OnPlayerListChanged onPlayerListChanged;

        public delegate void OnPlayerListChanged(int playerCount);


        int connectionCount => NetworkServer.connections.Count;

        public override void OnServerConnect(NetworkConnection conn)
        {
            int count = connectionCount;
            if (count > maxConnections)
            {
                conn.Disconnect();
                return;
            }

            onPlayerListChanged?.Invoke(count);
        }

        public override void OnServerDisconnect(NetworkConnection conn)
        {
            base.OnServerDisconnect(conn);
            onPlayerListChanged?.Invoke(connectionCount);
        }

        public override void OnServerAddPlayer(NetworkConnection conn)
        {
            Debug.Assert(startPositions.Count == 2, "Pong Scene should have 2 start Poitions");
            // add player at correct spawn position
            Transform startPos = numPlayers == 0 ? startPositions[0] : startPositions[1];

            GameObject player = startPos != null
                ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                : Instantiate(playerPrefab);

            NetworkServer.AddPlayerForConnection(conn, player);
        }

        public override void OnStartServer()
        {
            onServerStarted?.Invoke();
        }

        public override void OnStopServer()
        {
            onServerStopped?.Invoke();
        }
    }
}
