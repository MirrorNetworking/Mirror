using UnityEngine;

namespace Mirror.Examples.Shooter
{
    public class ShooterNetworkManager : NetworkManager
    {
        public static new ShooterNetworkManager singleton => (ShooterNetworkManager)NetworkManager.singleton;
        private ShooterCharacterData characterData;

        public override void Awake()
        {
            base.Awake();
        }

        public struct CreateCharacterMessage : NetworkMessage
        {
            public string playerName;
            public int playerCharacter;
            public Color playerColour;
        }

        public struct ReplaceCharacterMessage : NetworkMessage
        {
            public CreateCharacterMessage createCharacterMessage;
        }

        public override void OnStartServer()
        {
            FindReferences();
            base.OnStartServer();
            NetworkServer.RegisterHandler<CreateCharacterMessage>(OnCreateCharacter);
        }

        public override void OnClientConnect()
        {
            FindReferences();
            base.OnClientConnect();

            //you can send the message here, or wherever else you want
            CreateCharacterMessage characterMessage = new CreateCharacterMessage
            {
                playerName = ShooterCharacterData.playerName,
                playerCharacter = ShooterCharacterData.playerCharacter,
                playerColour = ShooterCharacterData.playerColour
            };

            NetworkClient.Send(characterMessage);
        }

        void OnCreateCharacter(NetworkConnectionToClient conn, CreateCharacterMessage message)
        {
            Transform startPos = GetStartPosition();

            // check if the save data has been pre-set
            if (message.playerName == "")
            {
                //Debug.Log("OnCreateCharacter name invalid or not set, use random.");
                message.playerName = "Player: " + UnityEngine.Random.Range(100, 1000);
            }

            // check that prefab is set, or exists for saved character number data
            // could be a cheater, or coding error, or different version conflict
            if (message.playerCharacter <= 0 || message.playerCharacter >= characterData.characterPrefabs.Length)
            {
                //Debug.Log("OnCreateCharacter prefab Invalid or not set, use random.");
                message.playerCharacter = UnityEngine.Random.Range(1, characterData.characterPrefabs.Length);
            }

            // check if the save data has been pre-set
            if (message.playerColour == new Color(0, 0, 0, 0))
            {
                //Debug.Log("OnCreateCharacter colour invalid or not set, use random.");
                message.playerColour = Random.ColorHSV(0f, 1f, 1f, 1f, 0f, 1f);
            }

            GameObject playerObject = startPos != null
                ? Instantiate(characterData.characterPrefabs[message.playerCharacter], startPos.position, startPos.rotation)
                : Instantiate(characterData.characterPrefabs[message.playerCharacter]);


            // Apply data from the message however appropriate for your game
            // Typically Player would be a component you write with syncvars or properties
            Player characterSelection = playerObject.GetComponent<Player>();
            characterSelection.playerName = message.playerName;
            characterSelection.playerCharacter = message.playerCharacter;
            characterSelection.playerColour = message.playerColour;

            // set sync var stats according to which character is chosen
            characterSelection.playerHealth = characterData.characterHealths[message.playerCharacter];

            // call this to use this gameobject as the primary controller
            NetworkServer.AddPlayerForConnection(conn, playerObject);
        }

        void FindReferences()
        {
            if (characterData == null)
            {
                characterData = (ShooterCharacterData)FindObjectOfType(typeof(ShooterCharacterData));
            }
        }
    }
}