using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System;

namespace Mirror.Examples.Additive
{
    public class ChatNetworkManager : NetworkManager
    {
        public ChatWindow chatWindow;


        public class CreatePlayerMessage: MessageBase
        {
            public string name;
        }

        public string PlayerName { get; set; }

        // Start is called before the first frame update
        public void SetHostname(string hostname)
        {
            this.networkAddress = hostname;
        }

        public override void OnClientConnect(NetworkConnection conn)
        {
            base.OnClientConnect(conn);

            // tell the server to create a player with this name
            conn.Send(new CreatePlayerMessage
            {
                name = PlayerName
            });
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            NetworkServer.RegisterHandler<CreatePlayerMessage>(OnCreatePlayer);
        }

        private void OnCreatePlayer(NetworkConnection connection, CreatePlayerMessage createPlayerMessage)
        {
            // create a gameobject using the name supplied by client
            GameObject playergo = Instantiate(playerPrefab);
            playergo.GetComponent<Player>().Name = createPlayerMessage.name;

            // set it as the player
            NetworkServer.AddPlayerForConnection(connection, playergo);
        }
    }
}