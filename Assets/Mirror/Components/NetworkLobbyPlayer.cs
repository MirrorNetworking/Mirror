using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror
{
    /// <summary>
    /// This component works in conjunction with the NetworkLobbyManager to make up the multiplayer lobby system.
    /// <para>The LobbyPrefab object of the NetworkLobbyManager must have this component on it. This component holds basic lobby player data required for the lobby to function. Game specific data for lobby players can be put in other components on the LobbyPrefab or in scripts derived from NetworkLobbyPlayer.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkLobbyPlayer")]
    [HelpURL("https://vis2k.github.io/Mirror/Components/NetworkLobbyPlayer")]
    public class NetworkLobbyPlayer : NetworkBehaviour
    {
        /// <summary>
        /// This flag controls whether the default UI is shown for the lobby player.
        /// <para>As this UI is rendered using the old GUI system, it is only recommended for testing purposes.</para>
        /// </summary>
        public bool showLobbyGUI = true;

        /// <summary>
        /// This is a flag that control whether this player is ready for the game to begin.
        /// <para>When all players are ready to begin, the game will start. This should not be set directly, the SendReadyToBeginMessage function should be called on the client to set it on the server.</para>
        /// </summary>
        [SyncVar(hook = nameof(ReadyStateChanged))]
        public bool readyToBegin;

        /// <summary>
        /// Current index of the player, e.g. Player1, Player2, etc.
        /// </summary>
        [SyncVar]
        public int index;

        #region Unity Callbacks

        /// <summary>
        /// Do not use Start - Override OnStartrHost / OnStartClient instead!
        /// </summary>
        public void Start()
        {
            if (NetworkManager.singleton as NetworkLobbyManager)
                OnClientEnterLobby();
            else
                Debug.LogError("LobbyPlayer could not find a NetworkLobbyManager. The LobbyPlayer requires a NetworkLobbyManager object to function. Make sure that there is one in the scene.");
        }

        #endregion

        #region Commands

        [Command]
        public void CmdChangeReadyState(bool readyState)
        {
            readyToBegin = readyState;
            NetworkLobbyManager lobby = NetworkManager.singleton as NetworkLobbyManager;
            if (lobby != null)
            {
                lobby.ReadyStatusChanged();
            }
        }

        #endregion

        #region SyncVar Hooks

        void ReadyStateChanged(bool newReadyState)
        {
            OnClientReady(readyToBegin);
        }

        #endregion

        #region Lobby Client Virtuals

        /// <summary>
        /// This is a hook that is invoked on all player objects when entering the lobby.
        /// <para>Note: isLocalPlayer is not guaranteed to be set until OnStartLocalPlayer is called.</para>
        /// </summary>
        public virtual void OnClientEnterLobby() { }

        /// <summary>
        /// This is a hook that is invoked on all player objects when exiting the lobby.
        /// </summary>
        public virtual void OnClientExitLobby() { }

        /// <summary>
        /// This is a hook that is invoked on clients when a LobbyPlayer switches between ready or not ready.
        /// <para>This function is called when the a client player calls SendReadyToBeginMessage() or SendNotReadyToBeginMessage().</para>
        /// </summary>
        /// <param name="readyState">Whether the player is ready or not.</param>
        public virtual void OnClientReady(bool readyState) { }

        #endregion

        #region Optional UI

        /// <summary>
        /// Render a UI for the lobby.   Override to provide your on UI
        /// </summary>
        public virtual void OnGUI()
        {
            if (!showLobbyGUI)
                return;

            NetworkLobbyManager lobby = NetworkManager.singleton as NetworkLobbyManager;
            if (lobby)
            {
                if (!lobby.showLobbyGUI)
                    return;

                if (SceneManager.GetActiveScene().name != lobby.LobbyScene)
                    return;

                GUILayout.BeginArea(new Rect(20f + (index * 100), 200f, 90f, 130f));

                GUILayout.Label($"Player [{index + 1}]");

                if (readyToBegin)
                    GUILayout.Label("Ready");
                else
                    GUILayout.Label("Not Ready");

                if (((isServer && index > 0) || isServerOnly) && GUILayout.Button("REMOVE"))
                {
                    // This button only shows on the Host for all players other than the Host
                    // Host and Players can't remove themselves (stop the client instead)
                    // Host can kick a Player this way.
                    GetComponent<NetworkIdentity>().connectionToClient.Disconnect();
                }

                GUILayout.EndArea();

                if (NetworkClient.active && isLocalPlayer)
                {
                    GUILayout.BeginArea(new Rect(20f, 300f, 120f, 20f));

                    if (readyToBegin)
                    {
                        if (GUILayout.Button("Cancel"))
                            CmdChangeReadyState(false);
                    }
                    else
                    {
                        if (GUILayout.Button("Ready"))
                            CmdChangeReadyState(true);
                    }

                    GUILayout.EndArea();
                }
            }
        }

        #endregion
    }
}
