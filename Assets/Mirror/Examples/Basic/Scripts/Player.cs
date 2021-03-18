using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.Basic
{
    public class Player : NetworkBehaviour
    {
        // Events that the UI will subscribe to
        public event System.Action<int> OnPlayerNumberChanged;
        public event System.Action<Color32> OnPlayerColorChanged;
        public event System.Action<int> OnPlayerDataChanged;

        // Players List to manage playerNumber
        internal static readonly List<Player> playersList = new List<Player>();

        internal static void ResetPlayerNumbers()
        {
            int playerNumber = 0;
            foreach (Player player in playersList)
            {
                player.playerNumber = playerNumber++;
            }
        }

        [Header("Player UI")]
        public GameObject playerUIPrefab;
        GameObject playerUI;

        [Header("SyncVars")]

        /// <summary>
        /// This is appended to the player name text, e.g. "Player 01"
        /// </summary>
        [SyncVar(hook = nameof(PlayerNumberChanged))]
        public int playerNumber = 0;

        /// <summary>
        /// This is updated by UpdateData which is called from OnStartServer via InvokeRepeating
        /// </summary>
        [SyncVar(hook = nameof(PlayerDataChanged))]
        public int playerData = 0;

        /// <summary>
        /// Random color for the playerData text, assigned in OnStartServer
        /// </summary>
        [SyncVar(hook = nameof(PlayerColorChanged))]
        public Color32 playerColor = Color.white;

        // This is called by the hook of playerNumber SyncVar above
        void PlayerNumberChanged(int _, int newPlayerNumber)
        {
            OnPlayerNumberChanged?.Invoke(newPlayerNumber);
        }

        // This is called by the hook of playerData SyncVar above
        void PlayerDataChanged(int _, int newPlayerData)
        {
            OnPlayerDataChanged?.Invoke(newPlayerData);
        }

        // This is called by the hook of playerColor SyncVar above
        void PlayerColorChanged(Color32 _, Color32 newPlayerColor)
        {
            OnPlayerColorChanged?.Invoke(newPlayerColor);
        }

        /// <summary>
        /// This is invoked for NetworkBehaviour objects when they become active on the server.
        /// <para>This could be triggered by NetworkServer.Listen() for objects in the scene, or by NetworkServer.Spawn() for objects that are dynamically created.</para>
        /// <para>This will be called for objects on a "host" as well as for object on a dedicated server.</para>
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();

            // Add this to the static Players List
            playersList.Add(this);

            // set the Player Color SyncVar
            playerColor = Random.ColorHSV(0f, 1f, 0.9f, 0.9f, 1f, 1f);

            // Start generating updates
            InvokeRepeating(nameof(UpdateData), 1, 1);
        }

        /// <summary>
        /// Invoked on the server when the object is unspawned
        /// <para>Useful for saving object data in persistent storage</para>
        /// </summary>
        public override void OnStopServer()
        {
            CancelInvoke();
            playersList.Remove(this);
        }

        // This only runs on the server, called from OnStartServer via InvokeRepeating
        [ServerCallback]
        void UpdateData()
        {
            playerData = Random.Range(100, 1000);
        }

        /// <summary>
        /// Called on every NetworkBehaviour when it is activated on a client.
        /// <para>Objects on the host have this function called, as there is a local client on the host. The values of SyncVars on object are guaranteed to be initialized correctly with the latest state from the server when this function is called on the client.</para>
        /// </summary>
        public override void OnStartClient()
        {
            // Activate the main panel
            ((BasicNetManager)NetworkManager.singleton).mainPanel.gameObject.SetActive(true);

            // Instantiate the player UI as child of the Players Panel
            playerUI = Instantiate(playerUIPrefab, ((BasicNetManager)NetworkManager.singleton).playersPanel);

            // Set this player object in PlayerUI to wire up event handlers
            playerUI.GetComponent<PlayerUI>().SetPlayer(this, isLocalPlayer);

            // Invoke all event handlers with the current data
            OnPlayerNumberChanged.Invoke(playerNumber);
            OnPlayerColorChanged.Invoke(playerColor);
            OnPlayerDataChanged.Invoke(playerData);
        }

        /// <summary>
        /// This is invoked on clients when the server has caused this object to be destroyed.
        /// <para>This can be used as a hook to invoke effects or do client specific cleanup.</para>
        /// </summary>
        public override void OnStopClient()
        {
            // Remove this player's UI object
            Destroy(playerUI);

            // Disable the main panel for local player
            if (isLocalPlayer)
                ((BasicNetManager)NetworkManager.singleton).mainPanel.gameObject.SetActive(false);
        }
    }
}
