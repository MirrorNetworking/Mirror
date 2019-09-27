using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror
{
    /// <summary>
    /// This component works in conjunction with the NetworkRoomManager to make up the multiplayer room system.
    /// <para>The RoomPrefab object of the NetworkRoomManager must have this component on it. This component holds basic room player data required for the room to function. Game specific data for room players can be put in other components on the RoomPrefab or in scripts derived from NetworkRoomPlayer.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkRoomPlayer")]
    [HelpURL("https://mirror-networking.com/docs/Components/NetworkRoomPlayer.html")]
    public class NetworkRoomPlayer : NetworkBehaviour
    {
        /// <summary>
        /// This flag controls whether the default UI is shown for the room player.
        /// <para>As this UI is rendered using the old GUI system, it is only recommended for testing purposes.</para>
        /// </summary>
        public bool showRoomGUI = true;

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
            if (NetworkManager.singleton is NetworkRoomManager room)
            {
                // NetworkRoomPlayer object must be set to DontDestroyOnLoad along with NetworkRoomManager
                // in server and all clients, otherwise it will be respawned in the game scene which would 
                // have undesireable effects.
                if (room.dontDestroyOnLoad)
                    DontDestroyOnLoad(gameObject);

                OnClientEnterRoom();
            }
            else
                Debug.LogError("RoomPlayer could not find a NetworkRoomManager. The RoomPlayer requires a NetworkRoomManager object to function. Make sure that there is one in the scene.");
        }

        #endregion

        #region Commands

        [Command]
        public void CmdChangeReadyState(bool readyState)
        {
            readyToBegin = readyState;
            NetworkRoomManager room = NetworkManager.singleton as NetworkRoomManager;
            if (room != null)
            {
                room.ReadyStatusChanged();
            }
        }

        #endregion

        #region SyncVar Hooks

        void ReadyStateChanged(bool newReadyState)
        {
            OnClientReady(newReadyState);
        }

        #endregion

        #region Room Client Virtuals

        /// <summary>
        /// This is a hook that is invoked on all player objects when entering the room.
        /// <para>Note: isLocalPlayer is not guaranteed to be set until OnStartLocalPlayer is called.</para>
        /// </summary>
        public virtual void OnClientEnterRoom() { }

        /// <summary>
        /// This is a hook that is invoked on all player objects when exiting the room.
        /// </summary>
        public virtual void OnClientExitRoom() { }

        /// <summary>
        /// This is a hook that is invoked on clients when a RoomPlayer switches between ready or not ready.
        /// <para>This function is called when the a client player calls SendReadyToBeginMessage() or SendNotReadyToBeginMessage().</para>
        /// </summary>
        /// <param name="readyState">Whether the player is ready or not.</param>
        public virtual void OnClientReady(bool readyState) { }

        #endregion

        #region Optional UI

        /// <summary>
        /// Render a UI for the room.   Override to provide your on UI
        /// </summary>
        public virtual void OnGUI()
        {
            if (!showRoomGUI)
                return;

            NetworkRoomManager room = NetworkManager.singleton as NetworkRoomManager;
            if (room)
            {
                if (!room.showRoomGUI)
                    return;

                if (SceneManager.GetActiveScene().name != room.RoomScene)
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
