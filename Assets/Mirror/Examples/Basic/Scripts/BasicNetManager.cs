using System.Collections.Generic;
using UnityEngine;

/*
	Documentation: https://mirror-networking.com/docs/Articles/Components/NetworkManager.html
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkManager.html
*/

namespace Mirror.Examples.Basic
{
    [AddComponentMenu("")]
    public class BasicNetManager : NetworkManager
    {
        [Header("Canvas UI")]

        [Tooltip("Assign Main Panel so it can be turned on from Player:OnStartClient")]
        public RectTransform mainPanel;

        [Tooltip("Assign Players Panel for instantiating PlayerUI as child")]
        public RectTransform playersPanel;

        /// <summary>
        /// Called on the server when a client adds a new player with NetworkClient.AddPlayer.
        /// <para>The default implementation for this function creates a new player object from the playerPrefab.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerAddPlayer(NetworkConnection conn)
        {
            base.OnServerAddPlayer(conn);
            Player.ResetPlayerNumbers();
        }

        /// <summary>
        /// Called on the server when a client disconnects.
        /// <para>This is called on the Server when a Client disconnects from the Server. Use an override to decide what should happen when a disconnection is detected.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerDisconnect(NetworkConnection conn)
        {
            base.OnServerDisconnect(conn);
            Player.ResetPlayerNumbers();
        }

    }
}
