using UnityEngine;

/*
	Documentation: https://mirror-networking.gitbook.io/docs/components/network-manager
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkManager.html
*/

namespace Mirror.Examples.Chat
{
    [AddComponentMenu("")]
    public class ChatNetworkManager : NetworkManager
    {
        [Header("Chat GUI")]
        public ChatWindow chatWindow;

        public string PlayerName;

        #region Messages

        public struct CreatePlayerMessage : NetworkMessage
        {
            public string name;
        }

        #endregion

        #region Server

        public override void OnStartServer()
        {
            base.OnStartServer();
            NetworkServer.RegisterHandler<CreatePlayerMessage>(OnCreatePlayer);
        }

        void OnCreatePlayer(NetworkConnection connection, CreatePlayerMessage createPlayerMessage)
        {
            // create a gameobject using the name supplied by client
            GameObject playergo = Instantiate(playerPrefab);
            playergo.GetComponent<Player>().playerName = createPlayerMessage.name;

            // set it as the player
            NetworkServer.AddPlayerForConnection(connection, playergo);

            chatWindow.gameObject.SetActive(true);
        }

        #endregion

        #region Client

        // Called by UI element UsernameInput.OnValueChanged
        public void SetPlayername(string playerName)
        {
            PlayerName = playerName;
        }

        // Called by UI element NetworkAddressInput.OnValueChanged
        public void SetHostname(string hostname)
        {
            networkAddress = hostname;
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();

            // tell the server to create a player with this name
            NetworkClient.connection.Send(new CreatePlayerMessage { name = PlayerName });
        }

        #endregion
    }
}
