using UnityEngine;

namespace Mirror.Examples.CharacterSelection
{
    public class NetworkManagerCharacterSelection : NetworkManager
    {
        // See the scene 'SceneMapSpawnWithNoCharacter', to spawn as empty player.
        // 'SceneMap' will auto spawn as random player character.
        // Compare Network Manager inspector setups to see the difference between the two.
        // Either of these allow selecting character after spawning in too.
        public bool SpawnAsCharacter = true;

        public static new NetworkManagerCharacterSelection singleton => (NetworkManagerCharacterSelection)NetworkManager.singleton;
        private CharacterData characterData;

        public override void Awake()
        {
            characterData = CharacterData.characterDataSingleton;
            if (characterData == null)
            {
                Debug.Log("Add CharacterData prefab singleton into the scene.");
                return;
            }
            base.Awake();
        }

        public struct CreateCharacterMessage : NetworkMessage
        {
            public string playerName;
            public int characterNumber;
            public Color characterColour;
        }

        public struct ReplaceCharacterMessage : NetworkMessage
        {
            public CreateCharacterMessage createCharacterMessage;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            NetworkServer.RegisterHandler<CreateCharacterMessage>(OnCreateCharacter);
            NetworkServer.RegisterHandler<ReplaceCharacterMessage>(OnReplaceCharacterMessage);
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();

            if (SpawnAsCharacter)
            {
                // you can send the message here, or wherever else you want
                CreateCharacterMessage characterMessage = new CreateCharacterMessage
                {
                    playerName = StaticVariables.playerName,
                    characterNumber = StaticVariables.characterNumber,
                    characterColour = StaticVariables.characterColour
                };

                NetworkClient.Send(characterMessage);
            }
        }

        void OnCreateCharacter(NetworkConnectionToClient conn, CreateCharacterMessage message)
        {
            Transform startPos = GetStartPosition();

            // check if the save data has been pre-set
            if (message.playerName == "")
            {
                Debug.Log("OnCreateCharacter name invalid or not set, use random.");
                message.playerName = "Player: " + UnityEngine.Random.Range(100, 1000);
            }

            // check that prefab is set, or exists for saved character number data
            // could be a cheater, or coding error, or different version conflict
            if (message.characterNumber <= 0 || message.characterNumber >= characterData.characterPrefabs.Length)
            {
                Debug.Log("OnCreateCharacter prefab Invalid or not set, use random.");
                message.characterNumber = UnityEngine.Random.Range(1, characterData.characterPrefabs.Length);
            }

            // check if the save data has been pre-set
            if (message.characterColour == new Color(0, 0, 0, 0))
            {
                Debug.Log("OnCreateCharacter colour invalid or not set, use random.");
                message.characterColour = Random.ColorHSV(0f, 1f, 1f, 1f, 0f, 1f);
            }

            GameObject playerObject = startPos != null
                ? Instantiate(characterData.characterPrefabs[message.characterNumber], startPos.position, startPos.rotation)
                : Instantiate(characterData.characterPrefabs[message.characterNumber]);


            // Apply data from the message however appropriate for your game
            // Typically Player would be a component you write with syncvars or properties
            CharacterSelection characterSelection = playerObject.GetComponent<CharacterSelection>();
            characterSelection.playerName = message.playerName;
            characterSelection.characterNumber = message.characterNumber;
            characterSelection.characterColour = message.characterColour;

            // call this to use this gameobject as the primary controller
            NetworkServer.AddPlayerForConnection(conn, playerObject);
        }

        void OnReplaceCharacterMessage(NetworkConnectionToClient conn, ReplaceCharacterMessage message)
        {
            // Cache a reference to the current player object
            GameObject oldPlayer = conn.identity.gameObject;

            GameObject playerObject = Instantiate(characterData.characterPrefabs[message.createCharacterMessage.characterNumber], oldPlayer.transform.position, oldPlayer.transform.rotation);

            // Instantiate the new player object and broadcast to clients
            // Include true for keepAuthority paramater to prevent ownership change
            NetworkServer.ReplacePlayerForConnection(conn, playerObject, true);

            // Apply data from the message however appropriate for your game
            // Typically Player would be a component you write with syncvars or properties
            CharacterSelection characterSelection = playerObject.GetComponent<CharacterSelection>();
            characterSelection.playerName = message.createCharacterMessage.playerName;
            characterSelection.characterNumber = message.createCharacterMessage.characterNumber;
            characterSelection.characterColour = message.createCharacterMessage.characterColour;

            // Remove the previous player object that's now been replaced
            // Delay is required to allow replacement to complete.
            Destroy(oldPlayer, 0.1f);
        }

        public void ReplaceCharacter(ReplaceCharacterMessage message)
        {
            NetworkClient.Send(message);
        }
    }
}