using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Mirror
{
    /// <summary>
    /// This is a specialized NetworkManager that includes a networked room.
    /// </summary>
    /// <remarks>
    /// <para>The room has slots that track the joined players, and a maximum player count that is enforced. It requires that the NetworkRoomPlayer component be on the room player objects.</para>
    /// <para>NetworkRoomManager is derived from NetworkManager, and so it implements many of the virtual functions provided by the NetworkManager class. To avoid accidentally replacing functionality of the NetworkRoomManager, there are new virtual functions on the NetworkRoomManager that begin with "OnRoom". These should be used on classes derived from NetworkRoomManager instead of the virtual functions on NetworkManager.</para>
    /// <para>The OnRoom*() functions have empty implementations on the NetworkRoomManager base class, so the base class functions do not have to be called.</para>
    /// </remarks>
    [AddComponentMenu("Network/NetworkRoomManager")]
    [HelpURL("https://mirror-networking.com/docs/Components/NetworkRoomManager.html")]
    public class NetworkRoomManager : NetworkManager
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkRoomManager));

        public struct PendingPlayer
        {
            public NetworkConnection conn;
            public GameObject roomPlayer;
        }

        [Header("Room Settings")]

        [FormerlySerializedAs("m_ShowRoomGUI")]
        [SerializeField]
        [Tooltip("This flag controls whether the default UI is shown for the room")]
        internal bool showRoomGUI = true;

        [FormerlySerializedAs("m_MinPlayers")]
        [SerializeField]
        [Tooltip("Minimum number of players to auto-start the game")]
        protected int minPlayers = 1;

        [FormerlySerializedAs("m_RoomPlayerPrefab")]
        [SerializeField]
        [Tooltip("Prefab to use for the Room Player")]
        protected NetworkRoomPlayer roomPlayerPrefab;

        /// <summary>
        /// The scene to use for the room. This is similar to the offlineScene of the NetworkManager.
        /// </summary>
        [Scene]
        public string RoomScene;

        /// <summary>
        /// The scene to use for the playing the game from the room. This is similar to the onlineScene of the NetworkManager.
        /// </summary>
        [Scene]
        public string GameplayScene;

        /// <summary>
        /// List of players that are in the Room
        /// </summary>
        [FormerlySerializedAs("m_PendingPlayers")]
        public List<PendingPlayer> pendingPlayers = new List<PendingPlayer>();

        [Header("Diagnostics")]

        /// <summary>
        /// True when all players have submitted a Ready message
        /// </summary>
        [Tooltip("Diagnostic flag indicating all players are ready to play")]
        [FormerlySerializedAs("allPlayersReady")]
        [SerializeField] bool _allPlayersReady;

        /// <summary>
        /// These slots track players that enter the room.
        /// <para>The slotId on players is global to the game - across all players.</para>
        /// </summary>
        [Tooltip("List of Room Player objects")]
        public List<NetworkRoomPlayer> roomSlots = new List<NetworkRoomPlayer>();

        public bool allPlayersReady
        {
            get => _allPlayersReady;
            set
            {
                bool wasReady = _allPlayersReady;
                bool nowReady = value;

                if (wasReady != nowReady)
                {
                    _allPlayersReady = value;

                    if (nowReady)
                    {
                        OnRoomServerPlayersReady();
                    }
                    else
                    {
                        OnRoomServerPlayersNotReady();
                    }
                }
            }
        }

        public override void OnValidate()
        {
            // always >= 0
            maxConnections = Mathf.Max(maxConnections, 0);

            // always <= maxConnections
            minPlayers = Mathf.Min(minPlayers, maxConnections);

            // always >= 0
            minPlayers = Mathf.Max(minPlayers, 0);

            if (roomPlayerPrefab != null)
            {
                NetworkIdentity identity = roomPlayerPrefab.GetComponent<NetworkIdentity>();
                if (identity == null)
                {
                    roomPlayerPrefab = null;
                    logger.LogError("RoomPlayer prefab must have a NetworkIdentity component.");
                }
            }

            base.OnValidate();
        }

        internal void ReadyStatusChanged()
        {
            int CurrentPlayers = 0;
            int ReadyPlayers = 0;

            foreach (NetworkRoomPlayer item in roomSlots)
            {
                if (item != null)
                {
                    CurrentPlayers++;
                    if (item.readyToBegin)
                        ReadyPlayers++;
                }
            }

            if (CurrentPlayers == ReadyPlayers)
                CheckReadyToBegin();
            else
                allPlayersReady = false;
        }

        /// <summary>
        /// Called on the server when a client is ready.
        /// <para>The default implementation of this function calls NetworkServer.SetClientReady() to continue the network setup process.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerReady(NetworkConnection conn)
        {
            logger.Log("NetworkRoomManager OnServerReady");
            base.OnServerReady(conn);

            if (conn != null && conn.identity != null)
            {
                GameObject roomPlayer = conn.identity.gameObject;

                // if null or not a room player, dont replace it
                if (roomPlayer != null && roomPlayer.GetComponent<NetworkRoomPlayer>() != null)
                    SceneLoadedForPlayer(conn, roomPlayer);
            }
        }

        void SceneLoadedForPlayer(NetworkConnection conn, GameObject roomPlayer)
        {
            if (logger.LogEnabled()) logger.LogFormat(LogType.Log, "NetworkRoom SceneLoadedForPlayer scene: {0} {1}", SceneManager.GetActiveScene().path, conn);

            if (IsSceneActive(RoomScene))
            {
                // cant be ready in room, add to ready list
                PendingPlayer pending;
                pending.conn = conn;
                pending.roomPlayer = roomPlayer;
                pendingPlayers.Add(pending);
                return;
            }

            GameObject gamePlayer = OnRoomServerCreateGamePlayer(conn, roomPlayer);
            if (gamePlayer == null)
            {
                // get start position from base class
                Transform startPos = GetStartPosition();
                gamePlayer = startPos != null
                    ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                    : Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            }

            if (!OnRoomServerSceneLoadedForPlayer(conn, roomPlayer, gamePlayer))
                return;

            // replace room player with game player
            NetworkServer.ReplacePlayerForConnection(conn, gamePlayer, true);
        }

        /// <summary>
        /// CheckReadyToBegin checks all of the players in the room to see if their readyToBegin flag is set.
        /// <para>If all of the players are ready, then the server switches from the RoomScene to the PlayScene, essentially starting the game. This is called automatically in response to NetworkRoomPlayer.CmdChangeReadyState.</para>
        /// </summary>
        public void CheckReadyToBegin()
        {
            if (!IsSceneActive(RoomScene))
                return;

            int numberOfReadyPlayers = NetworkServer.connections.Count(conn => conn.Value != null && conn.Value.identity.gameObject.GetComponent<NetworkRoomPlayer>().readyToBegin);
            bool enoughReadyPlayers = minPlayers <= 0 || numberOfReadyPlayers >= minPlayers;
            if (enoughReadyPlayers)
            {
                pendingPlayers.Clear();
                allPlayersReady = true;
            }
            else
            {
                allPlayersReady = false;
            }
        }

        internal void CallOnClientEnterRoom()
        {
            OnRoomClientEnter();
            foreach (NetworkRoomPlayer player in roomSlots)
                if (player != null)
                {
                    player.OnClientEnterRoom();
                }
        }

        void CallOnClientExitRoom()
        {
            OnRoomClientExit();
            foreach (NetworkRoomPlayer player in roomSlots)
                if (player != null)
                {
                    player.OnClientExitRoom();
                }
        }

        #region server handlers

        /// <summary>
        /// Called on the server when a new client connects.
        /// <para>Unity calls this on the Server when a Client connects to the Server. Use an override to tell the NetworkManager what to do when a client connects to the server.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerConnect(NetworkConnection conn)
        {
            if (numPlayers >= maxConnections)
            {
                conn.Disconnect();
                return;
            }

            // cannot join game in progress
            if (!IsSceneActive(RoomScene))
            {
                conn.Disconnect();
                return;
            }

            base.OnServerConnect(conn);
            OnRoomServerConnect(conn);
        }

        /// <summary>
        /// Called on the server when a client disconnects.
        /// <para>This is called on the Server when a Client disconnects from the Server. Use an override to decide what should happen when a disconnection is detected.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerDisconnect(NetworkConnection conn)
        {
            if (conn.identity != null)
            {
                NetworkRoomPlayer roomPlayer = conn.identity.GetComponent<NetworkRoomPlayer>();

                if (roomPlayer != null)
                    roomSlots.Remove(roomPlayer);

                foreach (NetworkIdentity clientOwnedObject in conn.clientOwnedObjects)
                {
                    roomPlayer = clientOwnedObject.GetComponent<NetworkRoomPlayer>();
                    if (roomPlayer != null)
                        roomSlots.Remove(roomPlayer);
                }
            }

            allPlayersReady = false;

            foreach (NetworkRoomPlayer player in roomSlots)
            {
                if (player != null)
                    player.GetComponent<NetworkRoomPlayer>().readyToBegin = false;
            }

            if (IsSceneActive(RoomScene))
                RecalculateRoomPlayerIndices();

            OnRoomServerDisconnect(conn);
            base.OnServerDisconnect(conn);
        }

        /// <summary>
        /// Called on the server when a client adds a new player with ClientScene.AddPlayer.
        /// <para>The default implementation for this function creates a new player object from the playerPrefab.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerAddPlayer(NetworkConnection conn)
        {
            if (IsSceneActive(RoomScene))
            {
                if (roomSlots.Count == maxConnections)
                    return;

                allPlayersReady = false;

                if (logger.LogEnabled()) logger.LogFormat(LogType.Log, "NetworkRoomManager.OnServerAddPlayer playerPrefab:{0}", roomPlayerPrefab.name);

                GameObject newRoomGameObject = OnRoomServerCreateRoomPlayer(conn);
                if (newRoomGameObject == null)
                    newRoomGameObject = Instantiate(roomPlayerPrefab.gameObject, Vector3.zero, Quaternion.identity);

                NetworkServer.AddPlayerForConnection(conn, newRoomGameObject);
            }
            else
                OnRoomServerAddPlayer(conn);
        }

        public void RecalculateRoomPlayerIndices()
        {
            if (roomSlots.Count > 0)
            {
                for (int i = 0; i < roomSlots.Count; i++)
                {
                    roomSlots[i].index = i;
                }
            }
        }

        /// <summary>
        /// This causes the server to switch scenes and sets the networkSceneName.
        /// <para>Clients that connect to this server will automatically switch to this scene. This is called autmatically if onlineScene or offlineScene are set, but it can be called from user code to switch scenes again while the game is in progress. This automatically sets clients to be not-ready. The clients must call NetworkClient.Ready() again to participate in the new scene.</para>
        /// </summary>
        /// <param name="newSceneName"></param>
        public override void ServerChangeScene(string newSceneName)
        {
            if (newSceneName == RoomScene)
            {
                foreach (NetworkRoomPlayer roomPlayer in roomSlots)
                {
                    if (roomPlayer == null)
                        continue;

                    // find the game-player object for this connection, and destroy it
                    NetworkIdentity identity = roomPlayer.GetComponent<NetworkIdentity>();

                    if (NetworkServer.active)
                    {
                        // re-add the room object
                        roomPlayer.GetComponent<NetworkRoomPlayer>().readyToBegin = false;
                        NetworkServer.ReplacePlayerForConnection(identity.connectionToClient, roomPlayer.gameObject);
                    }
                }

                allPlayersReady = false;
            }

            base.ServerChangeScene(newSceneName);
        }

        /// <summary>
        /// Called on the server when a scene is completed loaded, when the scene load was initiated by the server with ServerChangeScene().
        /// </summary>
        /// <param name="sceneName">The name of the new scene.</param>
        public override void OnServerSceneChanged(string sceneName)
        {
            if (sceneName != RoomScene)
            {
                // call SceneLoadedForPlayer on any players that become ready while we were loading the scene.
                foreach (PendingPlayer pending in pendingPlayers)
                    SceneLoadedForPlayer(pending.conn, pending.roomPlayer);

                pendingPlayers.Clear();
            }

            OnRoomServerSceneChanged(sceneName);
        }

        /// <summary>
        /// This is invoked when a server is started - including when a host is started.
        /// <para>StartServer has multiple signatures, but they all cause this hook to be called.</para>
        /// </summary>
        public override void OnStartServer()
        {
            if (string.IsNullOrEmpty(RoomScene))
            {
                logger.LogError("NetworkRoomManager RoomScene is empty. Set the RoomScene in the inspector for the NetworkRoomMangaer");
                return;
            }

            if (string.IsNullOrEmpty(GameplayScene))
            {
                logger.LogError("NetworkRoomManager PlayScene is empty. Set the PlayScene in the inspector for the NetworkRoomMangaer");
                return;
            }

            OnRoomStartServer();
        }

        /// <summary>
        /// This is invoked when a host is started.
        /// <para>StartHost has multiple signatures, but they all cause this hook to be called.</para>
        /// </summary>
        public override void OnStartHost()
        {
            OnRoomStartHost();
        }

        /// <summary>
        /// This is called when a server is stopped - including when a host is stopped.
        /// </summary>
        public override void OnStopServer()
        {
            roomSlots.Clear();
            OnRoomStopServer();
        }

        /// <summary>
        /// This is called when a host is stopped.
        /// </summary>
        public override void OnStopHost()
        {
            OnRoomStopHost();
        }

        #endregion

        #region client handlers

        /// <summary>
        /// This is invoked when the client is started.
        /// </summary>
        public override void OnStartClient()
        {
            if (roomPlayerPrefab == null || roomPlayerPrefab.gameObject == null)
                logger.LogError("NetworkRoomManager no RoomPlayer prefab is registered. Please add a RoomPlayer prefab.");
            else
                ClientScene.RegisterPrefab(roomPlayerPrefab.gameObject);

            if (playerPrefab == null)
                logger.LogError("NetworkRoomManager no GamePlayer prefab is registered. Please add a GamePlayer prefab.");

            OnRoomStartClient();
        }

        /// <summary>
        /// Called on the client when connected to a server.
        /// <para>The default implementation of this function sets the client as ready and adds a player. Override the function to dictate what happens when the client connects.</para>
        /// </summary>
        /// <param name="conn">Connection to the server.</param>
        public override void OnClientConnect(NetworkConnection conn)
        {
            OnRoomClientConnect(conn);
            base.OnClientConnect(conn);
        }

        /// <summary>
        /// Called on clients when disconnected from a server.
        /// <para>This is called on the client when it disconnects from the server. Override this function to decide what happens when the client disconnects.</para>
        /// </summary>
        /// <param name="conn">Connection to the server.</param>
        public override void OnClientDisconnect(NetworkConnection conn)
        {
            OnRoomClientDisconnect(conn);
            base.OnClientDisconnect(conn);
        }

        /// <summary>
        /// This is called when a client is stopped.
        /// </summary>
        public override void OnStopClient()
        {
            OnRoomStopClient();
            CallOnClientExitRoom();
            roomSlots.Clear();
        }

        /// <summary>
        /// Called on clients when a scene has completed loaded, when the scene load was initiated by the server.
        /// <para>Scene changes can cause player objects to be destroyed. The default implementation of OnClientSceneChanged in the NetworkManager is to add a player object for the connection if no player object exists.</para>
        /// </summary>
        /// <param name="conn">Connection of the client</param>
        public override void OnClientSceneChanged(NetworkConnection conn)
        {
            if (IsSceneActive(RoomScene))
            {
                if (NetworkClient.isConnected)
                    CallOnClientEnterRoom();
            }
            else
                CallOnClientExitRoom();

            base.OnClientSceneChanged(conn);
            OnRoomClientSceneChanged(conn);
        }

        #endregion

        #region room server virtuals

        /// <summary>
        /// This is called on the host when a host is started.
        /// </summary>
        public virtual void OnRoomStartHost() { }

        /// <summary>
        /// This is called on the host when the host is stopped.
        /// </summary>
        public virtual void OnRoomStopHost() { }

        /// <summary>
        /// This is called on the server when the server is started - including when a host is started.
        /// </summary>
        public virtual void OnRoomStartServer() { }

        /// <summary>
        /// This is called on the server when the server is started - including when a host is stopped.
        /// </summary>
        public virtual void OnRoomStopServer() { }

        /// <summary>
        /// This is called on the server when a new client connects to the server.
        /// </summary>
        /// <param name="conn">The new connection.</param>
        public virtual void OnRoomServerConnect(NetworkConnection conn) { }

        /// <summary>
        /// This is called on the server when a client disconnects.
        /// </summary>
        /// <param name="conn">The connection that disconnected.</param>
        public virtual void OnRoomServerDisconnect(NetworkConnection conn) { }

        /// <summary>
        /// This is called on the server when a networked scene finishes loading.
        /// </summary>
        /// <param name="sceneName">Name of the new scene.</param>
        public virtual void OnRoomServerSceneChanged(string sceneName) { }

        /// <summary>
        /// This allows customization of the creation of the room-player object on the server.
        /// <para>By default the roomPlayerPrefab is used to create the room-player, but this function allows that behaviour to be customized.</para>
        /// </summary>
        /// <param name="conn">The connection the player object is for.</param>
        /// <returns>The new room-player object.</returns>
        public virtual GameObject OnRoomServerCreateRoomPlayer(NetworkConnection conn)
        {
            return null;
        }

        // Deprecated 12/17/2019
        /// <summary>
        /// Obsolete: Use <see cref="OnRoomServerCreateGamePlayer(NetworkConnection, GameObject)">OnRoomServerCreateGamePlayer(NetworkConnection, GameObject)</see> instead.
        /// </summary>
        /// <param name="conn">The connection the player object is for.</param>
        /// <returns>A new GamePlayer object.</returns>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use OnRoomServerCreateGamePlayer(NetworkConnection conn, GameObject roomPlayer) instead", true)]
        public virtual GameObject OnRoomServerCreateGamePlayer(NetworkConnection conn)
        {
            return null;
        }

        /// <summary>
        /// This allows customization of the creation of the GamePlayer object on the server.
        /// <para>By default the gamePlayerPrefab is used to create the game-player, but this function allows that behaviour to be customized. The object returned from the function will be used to replace the room-player on the connection.</para>
        /// </summary>
        /// <param name="conn">The connection the player object is for.</param>
        /// <param name="roomPlayer">The room player object for this connection.</param>
        /// <returns>A new GamePlayer object.</returns>
        public virtual GameObject OnRoomServerCreateGamePlayer(NetworkConnection conn, GameObject roomPlayer)
        {
            return null;
        }

        /// <summary>
        /// This allows customization of the creation of the GamePlayer object on the server.
        /// <para>This is only called for subsequent GamePlay scenes after the first one.</para>
        /// <para>See <see cref="OnRoomServerCreateGamePlayer(NetworkConnection, GameObject)">OnRoomServerCreateGamePlayer(NetworkConnection, GameObject)</see> to customize the player object for the initial GamePlay scene.</para>
        /// </summary>
        /// <param name="conn">The connection the player object is for.</param>
        public virtual void OnRoomServerAddPlayer(NetworkConnection conn)
        {
            base.OnServerAddPlayer(conn);
        }

        // Deprecated 02/22/2020
        /// <summary>
        /// Obsolete: Use <see cref="OnRoomServerSceneLoadedForPlayer(NetworkConnection, GameObject, GameObject)">OnRoomServerSceneLoadedForPlayer(NetworkConnection, GameObject, GameObject)</see> instead.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use OnRoomServerSceneLoadedForPlayer(NetworkConnection conn, GameObject roomPlayer, GameObject gamePlayer) instead")]
        public virtual bool OnRoomServerSceneLoadedForPlayer(GameObject roomPlayer, GameObject gamePlayer)
        {
            return true;
        }

        // for users to apply settings from their room player object to their in-game player object
        /// <summary>
        /// This is called on the server when it is told that a client has finished switching from the room scene to a game player scene.
        /// <para>When switching from the room, the room-player is replaced with a game-player object. This callback function gives an opportunity to apply state from the room-player to the game-player object.</para>
        /// </summary>
        /// <param name="conn">The connection of the player</param>
        /// <param name="roomPlayer">The room player object.</param>
        /// <param name="gamePlayer">The game player object.</param>
        /// <returns>False to not allow this player to replace the room player.</returns>
        public virtual bool OnRoomServerSceneLoadedForPlayer(NetworkConnection conn, GameObject roomPlayer, GameObject gamePlayer)
        {
            return true;
        }

        /// <summary>
        /// This is called on the server when all the players in the room are ready.
        /// <para>The default implementation of this function uses ServerChangeScene() to switch to the game player scene. By implementing this callback you can customize what happens when all the players in the room are ready, such as adding a countdown or a confirmation for a group leader.</para>
        /// </summary>
        public virtual void OnRoomServerPlayersReady()
        {
            // all players are readyToBegin, start the game
            ServerChangeScene(GameplayScene);
        }

        /// <summary>
        /// This is called on the server when CheckReadyToBegin finds that players are not ready
        /// <para>May be called multiple times while not ready players are joining</para>
        /// </summary>
        public virtual void OnRoomServerPlayersNotReady() { }

        #endregion

        #region room client virtuals

        /// <summary>
        /// This is a hook to allow custom behaviour when the game client enters the room.
        /// </summary>
        public virtual void OnRoomClientEnter() { }

        /// <summary>
        /// This is a hook to allow custom behaviour when the game client exits the room.
        /// </summary>
        public virtual void OnRoomClientExit() { }

        /// <summary>
        /// This is called on the client when it connects to server.
        /// </summary>
        /// <param name="conn">The connection that connected.</param>
        public virtual void OnRoomClientConnect(NetworkConnection conn) { }

        /// <summary>
        /// This is called on the client when disconnected from a server.
        /// </summary>
        /// <param name="conn">The connection that disconnected.</param>
        public virtual void OnRoomClientDisconnect(NetworkConnection conn) { }

        /// <summary>
        /// This is called on the client when a client is started.
        /// </summary>
        /// <param name="roomClient">The connection for the room.</param>
        public virtual void OnRoomStartClient() { }

        /// <summary>
        /// This is called on the client when the client stops.
        /// </summary>
        public virtual void OnRoomStopClient() { }

        /// <summary>
        /// This is called on the client when the client is finished loading a new networked scene.
        /// </summary>
        /// <param name="conn">The connection that finished loading a new networked scene.</param>
        public virtual void OnRoomClientSceneChanged(NetworkConnection conn) { }

        /// <summary>
        /// Called on the client when adding a player to the room fails.
        /// <para>This could be because the room is full, or the connection is not allowed to have more players.</para>
        /// </summary>
        public virtual void OnRoomClientAddPlayerFailed() { }

        #endregion

        #region optional UI

        /// <summary>
        /// virtual so inheriting classes can roll their own
        /// </summary>
        public virtual void OnGUI()
        {
            if (!showRoomGUI)
                return;

            if (NetworkServer.active && IsSceneActive(GameplayScene))
            {
                GUILayout.BeginArea(new Rect(Screen.width - 150f, 10f, 140f, 30f));
                if (GUILayout.Button("Return to Room"))
                    ServerChangeScene(RoomScene);
                GUILayout.EndArea();
            }

            if (IsSceneActive(RoomScene))
                GUI.Box(new Rect(10f, 180f, 520f, 150f), "PLAYERS");
        }

        #endregion
    }
}
