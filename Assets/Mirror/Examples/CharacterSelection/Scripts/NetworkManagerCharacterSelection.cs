using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace Mirror.Examples.CharacterSelection
{
    public class NetworkManagerCharacterSelection : NetworkManager
    {
        private CharacterData characterData;

        public override void Awake()
        {
            characterData = CharacterData.characterDataSingleton;
            if (characterData == null)
            {
                Debug.Log("Add CharacterData prefab singleton into the scene.");
                return;
            }
        }

        public struct CreateCharacterMessage : NetworkMessage
        {
            public string playerName;
            public int characterNumber;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            NetworkServer.RegisterHandler<CreateCharacterMessage>(OnCreateCharacter);
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();

            // you can send the message here, or wherever else you want
            CreateCharacterMessage characterMessage = new CreateCharacterMessage
            {
                playerName = StaticVariables.playerName,
                characterNumber = StaticVariables.characterNumber
            };

            NetworkClient.Send(characterMessage);
        }

        void OnCreateCharacter(NetworkConnectionToClient conn, CreateCharacterMessage message)
        {
            // playerPrefab is the one assigned in the inspector in Network
            GameObject gameobject = Instantiate(characterData.characterPrefabs[message.characterNumber]);

            // Apply data from the message however appropriate for your game
            // Typically Player would be a component you write with syncvars or properties
            //Player player = gameobject.GetComponent();
            //player.hairColor = message.hairColor;
            //player.eyeColor = message.eyeColor;
            //player.name = message.name;
            //player.race = message.race;

            // call this to use this gameobject as the primary controller
            NetworkServer.AddPlayerForConnection(conn, gameobject);
        }
    }
}