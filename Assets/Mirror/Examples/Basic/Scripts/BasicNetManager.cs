using UnityEngine;

/*
	Documentation: https://mirror-networking.com/docs/Components/NetworkManager.html
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkManager.html
*/

namespace Mirror.Examples.Basic
{
    public class BasicNetManager : NetworkManager
    {
        // Sequential index used in round-robin deployment of players into instances and score positioning
        int clientIndex;

        /// <summary>
        /// Called on the server when a client adds a new player with ClientScene.AddPlayer.
        /// <para>The default implementation for this function creates a new player object from the playerPrefab.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerAddPlayer(NetworkConnection conn)
        {
            GameObject go = Instantiate(playerPrefab);
            Player player = go.GetComponent<Player>();
            player.playerColor = Random.ColorHSV(0f, 1f, 0.9f, 0.9f, 1f, 1f);
            player.playerNumber = clientIndex;

            // increment the index after setting on player, so first player starts at 0
            clientIndex++;

            NetworkServer.AddPlayerForConnection(conn, go);
        }
    }
}
