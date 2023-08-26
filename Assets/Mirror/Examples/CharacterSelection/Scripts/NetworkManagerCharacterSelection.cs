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
            Transform startPos = GetStartPosition();

            // check if the static save data has been pre-set
            //if (StaticVariables.playerName != "")
            //{
            //    inputFieldPlayerName.text = StaticVariables.playerName;
            //}

            // check that prefab is set, or exists for saved character number data
            // could be a cheater, or coding error, or different version conflict
            if (message.characterNumber <= 0 || message.characterNumber >= characterData.characterPrefabs.Length)
            {
                Debug.Log("OnCreateCharacter Invalid, set defaults.");
                message.characterNumber = UnityEngine.Random.Range(1, characterData.characterPrefabs.Length);
            }

            GameObject playerObject = startPos != null
                ? Instantiate(characterData.characterPrefabs[message.characterNumber], startPos.position, startPos.rotation)
                : Instantiate(characterData.characterPrefabs[message.characterNumber]);


            // Apply data from the message however appropriate for your game
            // Typically Player would be a component you write with syncvars or properties
            //Player player = gameobject.GetComponent();
            //player.hairColor = message.hairColor;
            //player.eyeColor = message.eyeColor;
            //player.name = message.name;
            //player.race = message.race;

            // call this to use this gameobject as the primary controller
            NetworkServer.AddPlayerForConnection(conn, playerObject);
        }
    }
}