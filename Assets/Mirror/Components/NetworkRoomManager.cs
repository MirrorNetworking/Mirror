using System.Collections.Generic;
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
        public struct PendingPlayer
        {
            public NetworkConnection conn;
            public GameObject roomPlayer;
        }

        [Header("Room Settings")]

        [FormerlySerializedAs("m_ShowRoomGUI")]
        [SerializeField]
        internal bool showRoomGUI = true;

        [FormerlySerializedAs("m_MinPlayers")]
        [SerializeField]
        int minPlayers = 1;

        [FormerlySerializedAs("m_RoomPlayerPrefab")]
        [SerializeField]
        NetworkRoomPlayer roomPlayerPrefab;

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

        /// <summary>
        /// These slots track players that enter the room.
        /// <para>The slotId on players is global to the game - across all players.</para>
        /// </summary>
        public List<NetworkRoomPlayer> roomSlots = new List<NetworkRoomPlayer>();

        /// <summary>
        /// True when all players have submitted a Ready message
        /// </summary>
        public bool allPlayersReady;

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
                    Debug.LogError("RoomPlayer prefab must have a NetworkIdentity component.");
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
        /// 
        /// </summary>
        /// <param name="conn">Connection of the client</param>
        public override void OnServerReady(NetworkConnection conn)
        {
            if (LogFilter.Debug) Debug.Log("NetworkRoomManager OnServerReady");
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
            if (LogFilter.Debug) Debug.LogFormat("NetworkRoom SceneLoadedForPlayer scene: {0} {1}", SceneManager.GetActiveScene().name, conn);

            if (SceneManager.GetActiveScene().name == RoomScene)
            {
                // cant be ready in room, add to ready list
                PendingPlayer pending;
                pending.conn = conn;
                pending.roomPlayer = roomPlayer;
                pendingPlayers.Add(pending);
                return;
            }

            GameObject gamePlayer = OnRoomServerCreateGamePlayer(conn);
            if (gamePlayer == null)
            {
                // get start position from base class
                Transform startPos = GetStartPosition();
                gamePlayer = startPos != null
                    ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                    : Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
                gamePlayer.name = playerPrefab.name;
            }

            if (!OnRoomServerSceneLoadedForPlayer(roomPlayer, gamePlayer))
                return;

            // replace room player with game player
            NetworkServer.ReplacePlayerForConnection(conn, gamePlayer);
        }

        /// <summary>
        /// CheckReadyToBegin checks all of the players in the room to see if their readyToBegin flag is set.
        /// <para>If all of the players are ready, then the server switches from the RoomScene to the PlayScene - essentially starting the game. This is called automatically in response to NetworkRoomPlayer.SendReadyToBeginMessage().</para>
        /// </summary>
        public void CheckReadyToBegin()
        {
            if (SceneManager.GetActiveScene().name != RoomScene) return;

            if (minPlayers > 0 && NetworkServer.connections.Count(conn => conn.Value != null && conn.Value.identity.gameObject.GetComponent<NetworkRoomPlayer>().readyToBegin) < minPlayers)
            {
                allPlayersReady = false;
                return;
            }

            pendingPlayers.Clear();
            allPlayersReady = true;
            OnRoomServerPlayersReady();
        }

        void CallOnClientEnterRoom()
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
        /// 
        /// </summary>
        /// <param name="conn">Connection of the client</param>
        public override void OnServerConnect(NetworkConnection conn)
        {
            if (numPlayers >= maxConnections)
            {
                conn.Disconnect();
                return;
            }

            // cannot join game in progress
            if (SceneManager.GetActiveScene().name != RoomScene)
            {
                conn.Disconnect();
                return;
            }

            base.OnServerConnect(conn);
            OnRoomServerConnect(conn);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn">Connection of the client</param>
        public override void OnServerDisconnect(NetworkConnection conn)
        {
            if (conn.identity != null)
            {
                NetworkRoomPlayer player = conn.identity.GetComponent<NetworkRoomPlayer>();

                if (player != null)
                    roomSlots.Remove(player);
            }

            allPlayersReady = false;

            foreach (NetworkRoomPlayer player in roomSlots)
            {
                if (player != null)
                    player.GetComponent<NetworkRoomPlayer>().readyToBegin = false;
            }

            if (SceneManager.GetActiveScene().name == RoomScene)
                RecalculateRoomPlayerIndices();

            base.OnServerDisconnect(conn);
            OnRoomServerDisconnect(conn);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn">Connection of the client</param>
        /// <param name="extraMessage"></param>
        public override void OnServerAddPlayer(NetworkConnection conn)
        {
            if (SceneManager.GetActiveScene().name != RoomScene) return;

            if (roomSlots.Count == maxConnections) return;

            allPlayersReady = false;

            if (LogFilter.Debug) Debug.LogFormat("NetworkRoomManager.OnServerAddPlayer playerPrefab:{0}", roomPlayerPrefab.name);

            GameObject newRoomGameObject = OnRoomServerCreateRoomPlayer(conn);
            if (newRoomGameObject == null)
                newRoomGameObject = (GameObject)Instantiate(roomPlayerPrefab.gameObject, Vector3.zero, Quaternion.identity);

            NetworkRoomPlayer newRoomPlayer = newRoomGameObject.GetComponent<NetworkRoomPlayer>();

            roomSlots.Add(newRoomPlayer);

            RecalculateRoomPlayerIndices();

            NetworkServer.AddPlayerForConnection(conn, newRoomGameObject);
        }

        void RecalculateRoomPlayerIndices()
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
        /// 
        /// </summary>
        /// <param name="sceneName"></param>
        public override void ServerChangeScene(string sceneName)
        {
            if (sceneName == RoomScene)
            {
                foreach (NetworkRoomPlayer roomPlayer in roomSlots)
                {
                    if (roomPlayer == null) continue;

                    // find the game-player object for this connection, and destroy it
                    NetworkIdentity identity = roomPlayer.GetComponent<NetworkIdentity>();

                    NetworkIdentity playerController = identity.connectionToClient.identity;
                    NetworkServer.Destroy(playerController.gameObject);

                    if (NetworkServer.active)
                    {
                        // re-add the room object
                        roomPlayer.GetComponent<NetworkRoomPlayer>().readyToBegin = false;
                        NetworkServer.ReplacePlayerForConnection(identity.connectionToClient, roomPlayer.gameObject);
                    }
                }
            }

            base.ServerChangeScene(sceneName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sceneName"></param>
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
        /// 
        /// </summary>
        public override void OnStartServer()
        {
            if (string.IsNullOrEmpty(RoomScene))
            {
                Debug.LogError("NetworkRoomManager RoomScene is empty. Set the RoomScene in the inspector for the NetworkRoomMangaer");
                return;
            }

            if (string.IsNullOrEmpty(GameplayScene))
            {
                Debug.LogError("NetworkRoomManager PlayScene is empty. Set the PlayScene in the inspector for the NetworkRoomMangaer");
                return;
            }

            OnRoomStartServer();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnStartHost()
        {
            OnRoomStartHost();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnStopServer()
        {
            roomSlots.Clear();
            base.OnStopServer();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnStopHost()
        {
            OnRoomStopHost();
        }

        #endregion

        #region client handlers

        /// <summary>
        /// 
        /// </summary>
        public override void OnStartClient()
        {
            if (roomPlayerPrefab == null || roomPlayerPrefab.gameObject == null)
                Debug.LogError("NetworkRoomManager no RoomPlayer prefab is registered. Please add a RoomPlayer prefab.");
            else
                ClientScene.RegisterPrefab(roomPlayerPrefab.gameObject);

            if (playerPrefab == null)
                Debug.LogError("NetworkRoomManager no GamePlayer prefab is registered. Please add a GamePlayer prefab.");
            else
                ClientScene.RegisterPrefab(playerPrefab);

            OnRoomStartClient();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn">Connection of the client</param>
        public override void OnClientConnect(NetworkConnection conn)
        {
            OnRoomClientConnect(conn);
            CallOnClientEnterRoom();
            base.OnClientConnect(conn);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn">Connection of the client</param>
        public override void OnClientDisconnect(NetworkConnection conn)
        {
            OnRoomClientDisconnect(conn);
            base.OnClientDisconnect(conn);
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnStopClient()
        {
            OnRoomStopClient();
            CallOnClientExitRoom();

            if (!string.IsNullOrEmpty(offlineScene))
            {
                // Move the RoomManager from the virtual DontDestroyOnLoad scene to the Game scene.
                // This let's it be destroyed when client changes to the Offline scene.
                SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn">Connection of the client</param>
        public override void OnClientSceneChanged(NetworkConnection conn)
        {
            if (SceneManager.GetActiveScene().name == RoomScene)
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

        /// <summary>
        /// This allows customization of the creation of the GamePlayer object on the server.
        /// <para>By default the gamePlayerPrefab is used to create the game-player, but this function allows that behaviour to be customized. The object returned from the function will be used to replace the room-player on the connection.</para>
        /// </summary>
        /// <param name="conn">The connection the player object is for.</param>
        /// <returns>A new GamePlayer object.</returns>
        public virtual GameObject OnRoomServerCreateGamePlayer(NetworkConnection conn)
        {
            return null;
        }

        // for users to apply settings from their room player object to their in-game player object
        /// <summary>
        /// This is called on the server when it is told that a client has finished switching from the room scene to a game player scene.
        /// <para>When switching from the room, the room-player is replaced with a game-player object. This callback function gives an opportunity to apply state from the room-player to the game-player object.</para>
        /// </summary>
        /// <param name="roomPlayer">The room player object.</param>
        /// <param name="gamePlayer">The game player object.</param>
        /// <returns>False to not allow this player to replace the room player.</returns>
        public virtual bool OnRoomServerSceneLoadedForPlayer(GameObject roomPlayer, GameObject gamePlayer)
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

            if (SceneManager.GetActiveScene().name != RoomScene)
                return;

            GUI.Box(new Rect(10f, 180f, 520f, 150f), "PLAYERS");
        }

        #endregion
    }
}
