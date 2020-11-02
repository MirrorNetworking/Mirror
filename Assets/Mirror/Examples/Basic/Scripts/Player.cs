using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.Basic
{
    public class Player : NetworkBehaviour
    {
        [Header("Player Components")]
        public RectTransform rectTransform;
        public Image image;

        [Header("Child Text Objects")]
        public Text playerNameText;
        public Text playerDataText;

        // These are set in BasicNetManager::OnServerAddPlayer
        [Header("SyncVars")]
        [SyncVar]
        public int playerNumber;
        [SyncVar]
        public Color playerColor;

        // This is updated by UpdateData which is called from OnStartServer via InvokeRepeating
        [SyncVar(hook = nameof(OnPlayerDataChanged))]
        public int playerData;

        // This is called by the hook of playerData SyncVar above
        void OnPlayerDataChanged(int oldPlayerData, int newPlayerData)
        {
            // Show the data in the UI
            playerDataText.text = string.Format("Data: {0:000}", newPlayerData);
        }

        // This fires on server when this player object is network-ready
        public override void OnStartServer()
        {
            base.OnStartServer();

            // Start generating updates
            InvokeRepeating(nameof(UpdateData), 1, 1);
        }

        // This only runs on the server, called from OnStartServer via InvokeRepeating
        [ServerCallback]
        void UpdateData()
        {
            playerData = Random.Range(100, 1000);
        }

        // This fires on all clients when this player object is network-ready
        public override void OnStartClient()
        {
            base.OnStartClient();

            // Make this a child of the layout panel in the Canvas
            transform.SetParent(GameObject.Find("PlayersPanel").transform);

            // Calculate position in the layout panel
            int x = 100 + ((playerNumber % 4) * 150);
            int y = -170 - ((playerNumber / 4) * 80);
            rectTransform.anchoredPosition = new Vector2(x, y);

            // Apply SyncVar values
            playerNameText.color = playerColor;
            playerNameText.text = string.Format("Player {0:00}", playerNumber);
        }

        // This only fires on the local client when this player object is network-ready
        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            // apply a shaded background to our player
            image.color = new Color(1f, 1f, 1f, 0.1f);
        }
    }
}
