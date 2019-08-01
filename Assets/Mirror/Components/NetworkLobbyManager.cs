using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Mirror
{
    /// <summary>
    /// This is a specialized NetworkManager that includes a networked lobby.
    /// </summary>
    /// <remarks>
    /// <para>The lobby has slots that track the joined players, and a maximum player count that is enforced. It requires that the NetworkLobbyPlayer component be on the lobby player objects.</para>
    /// <para>NetworkLobbyManager is derived from NetworkManager, and so it implements many of the virtual functions provided by the NetworkManager class. To avoid accidentally replacing functionality of the NetworkLobbyManager, there are new virtual functions on the NetworkLobbyManager that begin with "OnLobby". These should be used on classes derived from NetworkLobbyManager instead of the virtual functions on NetworkManager.</para>
    /// <para>The OnLobby*() functions have empty implementations on the NetworkLobbyManager base class, so the base class functions do not have to be called.</para>
    /// </remarks>
    [AddComponentMenu("Network/NetworkLobbyManager")]
    [HelpURL("https://vis2k.github.io/Mirror/Components/NetworkLobbyManager")]
    public class NetworkLobbyManager : NetworkManager
    {
        public struct PendingPlayer
        {
            public NetworkConnection conn;
            public GameObject lobbyPlayer;
        }

        [Header("Lobby Settings")]

        [FormerlySerializedAs("m_ShowLobbyGUI")]
        [SerializeField]
        internal bool showLobbyGUI = true;

        [FormerlySerializedAs("m_MinPlayers")]
        [SerializeField]
        int minPlayers = 1;

        [FormerlySerializedAs("m_LobbyPlayerPrefab")]
        [SerializeField]
        NetworkLobbyPlayer lobbyPlayerPrefab;

        /// <summary>
        /// The scene to use for the lobby. This is similar to the offlineScene of the NetworkManager.
        /// </summary>
        [Scene]
        public string LobbyScene;

        /// <summary>
        /// The scene to use for the playing the game from the lobby. This is similar to the onlineScene of the NetworkManager.
        /// </summary>
        [Scene]
        public string GameplayScene;

        /// <summary>
        /// List of players that are in the Lobby
        /// </summary>
        [FormerlySerializedAs("m_PendingPlayers")]
        public List<PendingPlayer> pendingPlayers = new List<PendingPlayer>();

        /// <summary>
        /// These slots track players that enter the lobby.
        /// <para>The slotId on players is global to the game - across all players.</para>
        /// </summary>
        public List<NetworkLobbyPlayer> lobbySlots = new List<NetworkLobbyPlayer>();

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

            if (lobbyPlayerPrefab != null)
            {
                NetworkIdentity identity = lobbyPlayerPrefab.GetComponent<NetworkIdentity>();
                if (identity == null)
                {
                    lobbyPlayerPrefab = null;
                    Debug.LogError("LobbyPlayer prefab must have a NetworkIdentity component.");
                }
            }

            base.OnValidate();
        }

        internal void ReadyStatusChanged()
        {
            int CurrentPlayers = 0;
            int ReadyPlayers = 0;

            foreach (NetworkLobbyPlayer item in lobbySlots)
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
            if (LogFilter.Debug) Debug.Log("NetworkLobbyManager OnServerReady");
            base.OnServerReady(conn);

            if (conn != null && conn.playerController != null)
            {
                GameObject lobbyPlayer = conn.playerController.gameObject;

                // if null or not a lobby player, dont replace it
                if (lobbyPlayer != null && lobbyPlayer.GetComponent<NetworkLobbyPlayer>() != null)
                    SceneLoadedForPlayer(conn, lobbyPlayer);
            }
        }

        void SceneLoadedForPlayer(NetworkConnection conn, GameObject lobbyPlayer)
        {
            if (LogFilter.Debug) Debug.LogFormat("NetworkLobby SceneLoadedForPlayer scene: {0} {1}", SceneManager.GetActiveScene().name, conn);

            if (SceneManager.GetActiveScene().name == LobbyScene)
            {
                // cant be ready in lobby, add to ready list
                PendingPlayer pending;
                pending.conn = conn;
                pending.lobbyPlayer = lobbyPlayer;
                pendingPlayers.Add(pending);
                return;
            }

            GameObject gamePlayer = OnLobbyServerCreateGamePlayer(conn);
            if (gamePlayer == null)
            {
                // get start position from base class
                Transform startPos = GetStartPosition();
                gamePlayer = startPos != null
                    ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                    : Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
                gamePlayer.name = playerPrefab.name;
            }

            if (!OnLobbyServerSceneLoadedForPlayer(lobbyPlayer, gamePlayer))
                return;

            // replace lobby player with game player
            NetworkServer.ReplacePlayerForConnection(conn, gamePlayer);
        }

        /// <summary>
        /// CheckReadyToBegin checks all of the players in the lobby to see if their readyToBegin flag is set.
        /// <para>If all of the players are ready, then the server switches from the LobbyScene to the PlayScene - essentially starting the game. This is called automatically in response to NetworkLobbyPlayer.SendReadyToBeginMessage().</para>
        /// </summary>
        public void CheckReadyToBegin()
        {
            if (SceneManager.GetActiveScene().name != LobbyScene) return;

            if (minPlayers > 0 && NetworkServer.connections.Count(conn => conn.Value != null && conn.Value.playerController.gameObject.GetComponent<NetworkLobbyPlayer>().readyToBegin) < minPlayers)
            {
                allPlayersReady = false;
                return;
            }

            pendingPlayers.Clear();
            allPlayersReady = true;
            OnLobbyServerPlayersReady();
        }

        void CallOnClientEnterLobby()
        {
            OnLobbyClientEnter();
            foreach (NetworkLobbyPlayer player in lobbySlots)
                if (player != null)
                {
                    player.OnClientEnterLobby();
                }
        }

        void CallOnClientExitLobby()
        {
            OnLobbyClientExit();
            foreach (NetworkLobbyPlayer player in lobbySlots)
                if (player != null)
                {
                    player.OnClientExitLobby();
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
            if (SceneManager.GetActiveScene().name != LobbyScene)
            {
                conn.Disconnect();
                return;
            }

            base.OnServerConnect(conn);
            OnLobbyServerConnect(conn);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn">Connection of the client</param>
        public override void OnServerDisconnect(NetworkConnection conn)
        {
            if (conn.playerController != null)
            {
                NetworkLobbyPlayer player = conn.playerController.GetComponent<NetworkLobbyPlayer>();

                if (player != null)
                    lobbySlots.Remove(player);
            }

            allPlayersReady = false;

            foreach (NetworkLobbyPlayer player in lobbySlots)
            {
                if (player != null)
                    player.GetComponent<NetworkLobbyPlayer>().readyToBegin = false;
            }

            if (SceneManager.GetActiveScene().name == LobbyScene)
                RecalculateLobbyPlayerIndices();

            base.OnServerDisconnect(conn);
            OnLobbyServerDisconnect(conn);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn">Connection of the client</param>
        /// <param name="extraMessage"></param>
        public override void OnServerAddPlayer(NetworkConnection conn, AddPlayerMessage extraMessage)
        {
            if (SceneManager.GetActiveScene().name != LobbyScene) return;

            if (lobbySlots.Count == maxConnections) return;

            allPlayersReady = false;

            if (LogFilter.Debug) Debug.LogFormat("NetworkLobbyManager.OnServerAddPlayer playerPrefab:{0}", lobbyPlayerPrefab.name);

            GameObject newLobbyGameObject = OnLobbyServerCreateLobbyPlayer(conn);
            if (newLobbyGameObject == null)
                newLobbyGameObject = (GameObject)Instantiate(lobbyPlayerPrefab.gameObject, Vector3.zero, Quaternion.identity);

            NetworkLobbyPlayer newLobbyPlayer = newLobbyGameObject.GetComponent<NetworkLobbyPlayer>();

            lobbySlots.Add(newLobbyPlayer);

            RecalculateLobbyPlayerIndices();

            NetworkServer.AddPlayerForConnection(conn, newLobbyGameObject);
        }

        void RecalculateLobbyPlayerIndices()
        {
            if (lobbySlots.Count > 0)
            {
                for (int i = 0; i < lobbySlots.Count; i++)
                {
                    lobbySlots[i].index = i;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sceneName"></param>
        public override void ServerChangeScene(string sceneName)
        {
            if (sceneName == LobbyScene)
            {
                foreach (NetworkLobbyPlayer lobbyPlayer in lobbySlots)
                {
                    if (lobbyPlayer == null) continue;

                    // find the game-player object for this connection, and destroy it
                    NetworkIdentity identity = lobbyPlayer.GetComponent<NetworkIdentity>();

                    NetworkIdentity playerController = identity.connectionToClient.playerController;
                    NetworkServer.Destroy(playerController.gameObject);

                    if (NetworkServer.active)
                    {
                        // re-add the lobby object
                        lobbyPlayer.GetComponent<NetworkLobbyPlayer>().readyToBegin = false;
                        NetworkServer.ReplacePlayerForConnection(identity.connectionToClient, lobbyPlayer.gameObject);
                    }
                }
            }
            else
            {
                if (dontDestroyOnLoad)
                {
                    foreach (NetworkLobbyPlayer lobbyPlayer in lobbySlots)
                    {
                        if (lobbyPlayer != null)
                        {
                            lobbyPlayer.transform.SetParent(null);
                            DontDestroyOnLoad(lobbyPlayer);
                        }
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
            if (sceneName != LobbyScene)
            {
                // call SceneLoadedForPlayer on any players that become ready while we were loading the scene.
                foreach (PendingPlayer pending in pendingPlayers)
                    SceneLoadedForPlayer(pending.conn, pending.lobbyPlayer);

                pendingPlayers.Clear();
            }

            OnLobbyServerSceneChanged(sceneName);
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnStartServer()
        {
            if (string.IsNullOrEmpty(LobbyScene))
            {
                Debug.LogError("NetworkLobbyManager LobbyScene is empty. Set the LobbyScene in the inspector for the NetworkLobbyMangaer");
                return;
            }

            if (string.IsNullOrEmpty(GameplayScene))
            {
                Debug.LogError("NetworkLobbyManager PlayScene is empty. Set the PlayScene in the inspector for the NetworkLobbyMangaer");
                return;
            }

            OnLobbyStartServer();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnStartHost()
        {
            OnLobbyStartHost();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnStopServer()
        {
            lobbySlots.Clear();
            base.OnStopServer();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnStopHost()
        {
            OnLobbyStopHost();
        }

        #endregion

        #region client handlers

        /// <summary>
        /// 
        /// </summary>
        public override void OnStartClient()
        {
            if (lobbyPlayerPrefab == null || lobbyPlayerPrefab.gameObject == null)
                Debug.LogError("NetworkLobbyManager no LobbyPlayer prefab is registered. Please add a LobbyPlayer prefab.");
            else
                ClientScene.RegisterPrefab(lobbyPlayerPrefab.gameObject);

            if (playerPrefab == null)
                Debug.LogError("NetworkLobbyManager no GamePlayer prefab is registered. Please add a GamePlayer prefab.");
            else
                ClientScene.RegisterPrefab(playerPrefab);

            OnLobbyStartClient();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn">Connection of the client</param>
        public override void OnClientConnect(NetworkConnection conn)
        {
            OnLobbyClientConnect(conn);
            CallOnClientEnterLobby();
            base.OnClientConnect(conn);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn">Connection of the client</param>
        public override void OnClientDisconnect(NetworkConnection conn)
        {
            OnLobbyClientDisconnect(conn);
            base.OnClientDisconnect(conn);
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnStopClient()
        {
            OnLobbyStopClient();
            CallOnClientExitLobby();

            if (!string.IsNullOrEmpty(offlineScene))
            {
                // Move the LobbyManager from the virtual DontDestroyOnLoad scene to the Game scene.
                // This let's it be destroyed when client changes to the Offline scene.
                SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newSceneName"></param>
        public override void OnClientChangeScene(string newSceneName)
        {
            if (LogFilter.Debug) Debug.LogFormat("OnClientChangeScene from {0} to {1}", SceneManager.GetActiveScene().name, newSceneName);

            if (SceneManager.GetActiveScene().name == LobbyScene && newSceneName == GameplayScene && dontDestroyOnLoad && NetworkClient.isConnected)
            {
                if (NetworkClient.connection != null && NetworkClient.connection.playerController != null)
                {
                    GameObject lobbyPlayer = NetworkClient.connection.playerController.gameObject;
                    if (lobbyPlayer != null)
                    {
                        lobbyPlayer.transform.SetParent(null);
                        DontDestroyOnLoad(lobbyPlayer);
                    }
                    else
                        Debug.LogWarningFormat("OnClientChangeScene: lobbyPlayer is null");
                }
            }
            else
               if (LogFilter.Debug) Debug.LogFormat("OnClientChangeScene {0} {1}", dontDestroyOnLoad, NetworkClient.isConnected);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn">Connection of the client</param>
        public override void OnClientSceneChanged(NetworkConnection conn)
        {
            if (SceneManager.GetActiveScene().name == LobbyScene)
            {
                if (NetworkClient.isConnected)
                    CallOnClientEnterLobby();
            }
            else
                CallOnClientExitLobby();

            base.OnClientSceneChanged(conn);
            OnLobbyClientSceneChanged(conn);
        }

        #endregion

        #region lobby server virtuals

        /// <summary>
        /// This is called on the host when a host is started.
        /// </summary>
        public virtual void OnLobbyStartHost() { }

        /// <summary>
        /// This is called on the host when the host is stopped.
        /// </summary>
        public virtual void OnLobbyStopHost() { }

        /// <summary>
        /// This is called on the server when the server is started - including when a host is started.
        /// </summary>
        public virtual void OnLobbyStartServer() { }

        /// <summary>
        /// This is called on the server when a new client connects to the server.
        /// </summary>
        /// <param name="conn">The new connection.</param>
        public virtual void OnLobbyServerConnect(NetworkConnection conn) { }

        /// <summary>
        /// This is called on the server when a client disconnects.
        /// </summary>
        /// <param name="conn">The connection that disconnected.</param>
        public virtual void OnLobbyServerDisconnect(NetworkConnection conn) { }

        /// <summary>
        /// This is called on the server when a networked scene finishes loading.
        /// </summary>
        /// <param name="sceneName">Name of the new scene.</param>
        public virtual void OnLobbyServerSceneChanged(string sceneName) { }

        /// <summary>
        /// This allows customization of the creation of the lobby-player object on the server.
        /// <para>By default the lobbyPlayerPrefab is used to create the lobby-player, but this function allows that behaviour to be customized.</para>
        /// </summary>
        /// <param name="conn">The connection the player object is for.</param>
        /// <returns>The new lobby-player object.</returns>
        public virtual GameObject OnLobbyServerCreateLobbyPlayer(NetworkConnection conn)
        {
            return null;
        }

        /// <summary>
        /// This allows customization of the creation of the GamePlayer object on the server.
        /// <para>By default the gamePlayerPrefab is used to create the game-player, but this function allows that behaviour to be customized. The object returned from the function will be used to replace the lobby-player on the connection.</para>
        /// </summary>
        /// <param name="conn">The connection the player object is for.</param>
        /// <returns>A new GamePlayer object.</returns>
        public virtual GameObject OnLobbyServerCreateGamePlayer(NetworkConnection conn)
        {
            return null;
        }

        // for users to apply settings from their lobby player object to their in-game player object
        /// <summary>
        /// This is called on the server when it is told that a client has finished switching from the lobby scene to a game player scene.
        /// <para>When switching from the lobby, the lobby-player is replaced with a game-player object. This callback function gives an opportunity to apply state from the lobby-player to the game-player object.</para>
        /// </summary>
        /// <param name="lobbyPlayer">The lobby player object.</param>
        /// <param name="gamePlayer">The game player object.</param>
        /// <returns>False to not allow this player to replace the lobby player.</returns>
        public virtual bool OnLobbyServerSceneLoadedForPlayer(GameObject lobbyPlayer, GameObject gamePlayer)
        {
            return true;
        }

        /// <summary>
        /// This is called on the server when all the players in the lobby are ready.
        /// <para>The default implementation of this function uses ServerChangeScene() to switch to the game player scene. By implementing this callback you can customize what happens when all the players in the lobby are ready, such as adding a countdown or a confirmation for a group leader.</para>
        /// </summary>
        public virtual void OnLobbyServerPlayersReady()
        {
            // all players are readyToBegin, start the game
            ServerChangeScene(GameplayScene);
        }

        #endregion

        #region lobby client virtuals

        /// <summary>
        /// This is a hook to allow custom behaviour when the game client enters the lobby.
        /// </summary>
        public virtual void OnLobbyClientEnter() { }

        /// <summary>
        /// This is a hook to allow custom behaviour when the game client exits the lobby.
        /// </summary>
        public virtual void OnLobbyClientExit() { }

        /// <summary>
        /// This is called on the client when it connects to server.
        /// </summary>
        /// <param name="conn">The connection that connected.</param>
        public virtual void OnLobbyClientConnect(NetworkConnection conn) { }

        /// <summary>
        /// This is called on the client when disconnected from a server.
        /// </summary>
        /// <param name="conn">The connection that disconnected.</param>
        public virtual void OnLobbyClientDisconnect(NetworkConnection conn) { }

        /// <summary>
        /// This is called on the client when a client is started.
        /// </summary>
        /// <param name="lobbyClient">The connection for the lobby.</param>
        public virtual void OnLobbyStartClient() { }

        /// <summary>
        /// This is called on the client when the client stops.
        /// </summary>
        public virtual void OnLobbyStopClient() { }

        /// <summary>
        /// This is called on the client when the client is finished loading a new networked scene.
        /// </summary>
        /// <param name="conn">The connection that finished loading a new networked scene.</param>
        public virtual void OnLobbyClientSceneChanged(NetworkConnection conn) { }

        /// <summary>
        /// Called on the client when adding a player to the lobby fails.
        /// <para>This could be because the lobby is full, or the connection is not allowed to have more players.</para>
        /// </summary>
        public virtual void OnLobbyClientAddPlayerFailed() { }

        #endregion

        #region optional UI

        /// <summary>
        /// virtual so inheriting classes can roll their own
        /// </summary>
        public virtual void OnGUI()
        {
            if (!showLobbyGUI)
                return;

            if (SceneManager.GetActiveScene().name != LobbyScene)
                return;

            GUI.Box(new Rect(10f, 180f, 520f, 150f), "PLAYERS");
        }

        #endregion
    }
}
