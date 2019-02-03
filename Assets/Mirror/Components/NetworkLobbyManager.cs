using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Mirror.Components.NetworkLobby
{
    [AddComponentMenu("Network/NetworkLobbyManager")]
    [HelpURL("https://vis2k.github.io/Mirror/Components/NetworkLobbyManager")]
    public class NetworkLobbyManager : NetworkManager
    {
        struct PendingPlayer
        {
            public NetworkConnection conn;
            public GameObject lobbyPlayer;
        }

        // configuration
        [Header("Lobby Settings")]
        [FormerlySerializedAs("m_ShowLobbyGUI")] [SerializeField] internal bool showLobbyGUI = true;
        [FormerlySerializedAs("m_MinPlayers")] [SerializeField] int minPlayers = 1;
        [FormerlySerializedAs("m_LobbyPlayerPrefab")] [SerializeField] NetworkLobbyPlayer lobbyPlayerPrefab;

        [SerializeField, Scene]
        internal string LobbyScene;

        [SerializeField, Scene]
        internal string GameplayScene;

        // runtime data
        [FormerlySerializedAs("m_PendingPlayers")] List<PendingPlayer> pendingPlayers = new List<PendingPlayer>();
        List<NetworkLobbyPlayer> lobbySlots = new List<NetworkLobbyPlayer>();

        public override void OnValidate()
        {
            maxConnections = Mathf.Max(maxConnections, 0); // always >= 0

            minPlayers = Mathf.Min(minPlayers, maxConnections); // always <= maxConnections
            minPlayers = Mathf.Max(minPlayers, 0); // always >= 0

            if (lobbyPlayerPrefab != null)
            {
                NetworkIdentity uv = lobbyPlayerPrefab.GetComponent<NetworkIdentity>();
                if (uv == null)
                {
                    lobbyPlayerPrefab = null;
                    Debug.LogWarning("LobbyPlayer prefab must have a NetworkIdentity component.");
                }
            }

            base.OnValidate();
        }

        internal void PlayerLoadedScene(NetworkConnection conn)
        {
            if (LogFilter.Debug) { Debug.Log("NetworkLobbyManager OnSceneLoadedMessage"); }

            NetworkIdentity lobbyController = conn.playerController;

            SceneLoadedForPlayer(conn, lobbyController.gameObject);
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
                    if (item.ReadyToBegin)
                        ReadyPlayers++;
                }
            }
            if (CurrentPlayers == ReadyPlayers)
                CheckReadyToBegin();
        }


        void SceneLoadedForPlayer(NetworkConnection conn, GameObject lobbyPlayerGameObject)
        {
            NetworkLobbyPlayer lobbyPlayer = lobbyPlayerGameObject.GetComponent<NetworkLobbyPlayer>();
            if (lobbyPlayer == null)
            {
                // not a lobby player.. dont replace it
                return;
            }

            string loadedSceneName = SceneManager.GetActiveScene().name;
            if (LogFilter.Debug) { Debug.Log("NetworkLobby SceneLoadedForPlayer scene:" + loadedSceneName + " " + conn); }

            if (loadedSceneName == LobbyScene)
            {
                // cant be ready in lobby, add to ready list
                PendingPlayer pending;
                pending.conn = conn;
                pending.lobbyPlayer = lobbyPlayerGameObject;
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
            }

            if (!OnLobbyServerSceneLoadedForPlayer(lobbyPlayerGameObject, gamePlayer))
                return;

            // replace lobby player with game player
            NetworkServer.ReplacePlayerForConnection(conn, gamePlayer);
        }

        static int CheckConnectionIsReadyToBegin(NetworkConnection conn)
        {
            int countPlayers = 0;
            NetworkIdentity player = conn.playerController;
            NetworkLobbyPlayer lobbyPlayer = player.gameObject.GetComponent<NetworkLobbyPlayer>();
            if (lobbyPlayer.ReadyToBegin)
                countPlayers += 1;

            return countPlayers;
        }

        public void CheckReadyToBegin()
        {
            string loadedSceneName = SceneManager.GetActiveScene().name;
            if (loadedSceneName != LobbyScene)
                return;

            int readyCount = 0;

            foreach (KeyValuePair<int, NetworkConnection> conn in NetworkServer.connections)
            {
                if (conn.Value == null)
                    continue;

                readyCount += CheckConnectionIsReadyToBegin(conn.Value);
            }

            if (minPlayers > 0 && readyCount < minPlayers)
                return;

            pendingPlayers.Clear();
            OnLobbyServerPlayersReady();
        }

        public void ServerReturnToLobby()
        {
            if (!NetworkServer.active)
            {
                Debug.Log("ServerReturnToLobby called on client");
                return;
            }
            ServerChangeScene(LobbyScene);
        }

        public void CallOnClientEnterLobby()
        {
            OnLobbyClientEnter();
            foreach (NetworkLobbyPlayer player in lobbySlots)
            {
                if (player == null)
                    continue;

                player.OnClientEnterLobby();
            }
        }

        void CallOnClientExitLobby()
        {
            OnLobbyClientExit();
            foreach (NetworkLobbyPlayer player in lobbySlots)
            {
                if (player == null)
                    continue;

                player.OnClientExitLobby();
            }
        }

        // ------------------------ server handlers ------------------------

        public override void OnServerConnect(NetworkConnection conn)
        {
            if (numPlayers >= maxConnections)
            {
                conn.Disconnect();
                return;
            }

            // cannot join game in progress
            string loadedSceneName = SceneManager.GetActiveScene().name;
            if (loadedSceneName != LobbyScene)
            {
                conn.Disconnect();
                return;
            }

            base.OnServerConnect(conn);
            OnLobbyServerConnect(conn);
        }

        public override void OnServerDisconnect(NetworkConnection conn)
        {
            if (conn.playerController != null)
            {
                NetworkLobbyPlayer player = conn.playerController.GetComponent<NetworkLobbyPlayer>();

                if (player != null)
                    lobbySlots.Remove(player);
            }

            foreach (NetworkLobbyPlayer p in lobbySlots)
            {
                if (p != null)
                    p.GetComponent<NetworkLobbyPlayer>().ReadyToBegin = false;
            }

            base.OnServerDisconnect(conn);

            if (SceneManager.GetActiveScene().name != LobbyScene)
                RecalculateLobbyPlayerIndices();

            OnLobbyServerDisconnect(conn);
        }

        public override void OnServerAddPlayer(NetworkConnection conn)
        {
            string loadedSceneName = SceneManager.GetActiveScene().name;
            if (loadedSceneName != LobbyScene)
                return;

            if (lobbySlots.Count == maxConnections)
                return;

            GameObject newLobbyGameObject = OnLobbyServerCreateLobbyPlayer(conn);
            if (newLobbyGameObject == null)
                newLobbyGameObject = (GameObject)Instantiate(lobbyPlayerPrefab.gameObject, Vector3.zero, Quaternion.identity);

            NetworkLobbyPlayer newLobbyPlayer = newLobbyGameObject.GetComponent<NetworkLobbyPlayer>();

            lobbySlots.Add(newLobbyPlayer);

            RecalculateLobbyPlayerIndices();

            NetworkServer.AddPlayerForConnection(conn, newLobbyGameObject);
        }

        private void RecalculateLobbyPlayerIndices()
        {
            if (lobbySlots.Count > 0)
            {
                for (int i = 0; i < lobbySlots.Count; i++)
                {
                    lobbySlots[i].Index = i;
                }
            }
        }

        public override void ServerChangeScene(string sceneName)
        {
            if (sceneName == LobbyScene)
            {
                Application.targetFrameRate = 10;

                foreach (NetworkLobbyPlayer lobbyPlayer in lobbySlots)
                {
                    if (lobbyPlayer == null)
                        continue;

                    // find the game-player object for this connection, and destroy it
                    NetworkIdentity uv = lobbyPlayer.GetComponent<NetworkIdentity>();

                    NetworkIdentity playerController = uv.connectionToClient.playerController;
                    NetworkServer.Destroy(playerController.gameObject);

                    if (NetworkServer.active)
                    {
                        // re-add the lobby object
                        lobbyPlayer.GetComponent<NetworkLobbyPlayer>().ReadyToBegin = false;
                        NetworkServer.ReplacePlayerForConnection(uv.connectionToClient, lobbyPlayer.gameObject);
                    }
                }
            }
            else
                Application.targetFrameRate = 60;

            base.ServerChangeScene(sceneName);
        }

        public override void OnServerSceneChanged(string sceneName)
        {
            if (sceneName != LobbyScene)
            {
                // call SceneLoadedForPlayer on any players that become ready while we were loading the scene.
                foreach (PendingPlayer pending in pendingPlayers)
                {
                    SceneLoadedForPlayer(pending.conn, pending.lobbyPlayer);
                }

                pendingPlayers.Clear();
            }

            OnLobbyServerSceneChanged(sceneName);
        }

        void OnServerReturnToLobbyMessage(NetworkMessage netMsg)
        {
            if (LogFilter.Debug) { Debug.Log("NetworkLobbyManager OnServerReturnToLobbyMessage"); }

            ServerReturnToLobby();
        }

        public override void OnStartServer()
        {
            if (string.IsNullOrEmpty(LobbyScene))
            {
                if (LogFilter.Debug) { Debug.LogError("NetworkLobbyManager LobbyScene is empty. Set the LobbyScene in the inspector for the NetworkLobbyMangaer"); }
                return;
            }

            if (string.IsNullOrEmpty(GameplayScene))
            {
                if (LogFilter.Debug) { Debug.LogError("NetworkLobbyManager PlayScene is empty. Set the PlayScene in the inspector for the NetworkLobbyMangaer"); }
                return;
            }

            OnLobbyStartServer();
        }

        public override void OnStartHost()
        {
            OnLobbyStartHost();
        }

        public override void OnStopServer()
        {
            lobbySlots.Clear();
            base.OnStopServer();
        }

        public override void OnStopHost()
        {
            OnLobbyStopHost();
        }

        // ------------------------ client handlers ------------------------

        public override void OnStartClient(NetworkClient lobbyClient)
        {

            if (lobbyPlayerPrefab == null || lobbyPlayerPrefab.gameObject == null)
            {
                if (LogFilter.Debug) { Debug.LogError("NetworkLobbyManager no LobbyPlayer prefab is registered. Please add a LobbyPlayer prefab."); }
            }
            else
            {
                ClientScene.RegisterPrefab(lobbyPlayerPrefab.gameObject);
            }

            if (playerPrefab == null)
            {
                if (LogFilter.Debug) { Debug.LogError("NetworkLobbyManager no GamePlayer prefab is registered. Please add a GamePlayer prefab."); }
            }
            else
            {
                ClientScene.RegisterPrefab(playerPrefab);
            }

            OnLobbyStartClient(lobbyClient);
        }

        public override void OnClientConnect(NetworkConnection conn)
        {
            OnLobbyClientConnect(conn);
            CallOnClientEnterLobby();
            base.OnClientConnect(conn);
        }

        public override void OnClientDisconnect(NetworkConnection conn)
        {
            OnLobbyClientDisconnect(conn);
            base.OnClientDisconnect(conn);
        }

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

        public override void OnClientSceneChanged(NetworkConnection conn)
        {
            string loadedSceneName = SceneManager.GetActiveScene().name;
            if (loadedSceneName == LobbyScene)
            {
                if (client.isConnected)
                    CallOnClientEnterLobby();
            }
            else
                CallOnClientExitLobby();

            base.OnClientSceneChanged(conn);
            OnLobbyClientSceneChanged(conn);
        }

        // ------------------------ lobby server virtuals ------------------------

        public virtual void OnLobbyStartHost() { }

        public virtual void OnLobbyStopHost() { }

        public virtual void OnLobbyStartServer() { }

        public virtual void OnLobbyServerConnect(NetworkConnection conn) { }

        public virtual void OnLobbyServerDisconnect(NetworkConnection conn) { }

        public virtual void OnLobbyServerSceneChanged(string sceneName) { }

        public virtual GameObject OnLobbyServerCreateLobbyPlayer(NetworkConnection conn)
        {
            return null;
        }

        public virtual GameObject OnLobbyServerCreateGamePlayer(NetworkConnection conn)
        {
            return null;
        }

        // for users to apply settings from their lobby player object to their in-game player object
        public virtual bool OnLobbyServerSceneLoadedForPlayer(GameObject lobbyPlayer, GameObject gamePlayer)
        {
            return true;
        }

        public virtual void OnLobbyServerPlayersReady()
        {
            // all players are readyToBegin, start the game
            ServerChangeScene(GameplayScene);
        }

        // ------------------------ lobby client virtuals ------------------------

        public virtual void OnLobbyClientEnter() { }

        public virtual void OnLobbyClientExit() { }

        public virtual void OnLobbyClientConnect(NetworkConnection conn) { }

        public virtual void OnLobbyClientDisconnect(NetworkConnection conn) { }

        public virtual void OnLobbyStartClient(NetworkClient lobbyClient) { }

        public virtual void OnLobbyStopClient() { }

        public virtual void OnLobbyClientSceneChanged(NetworkConnection conn) { }

        // for users to handle adding a player failed on the server
        public virtual void OnLobbyClientAddPlayerFailed() { }

        // ------------------------ optional UI ------------------------

        void OnGUI()
        {
            if (!showLobbyGUI)
                return;

            string loadedSceneName = SceneManager.GetActiveScene().name;
            if (loadedSceneName != LobbyScene)
                return;

            GUI.Box(new Rect(10, 180, 520, 150), "PLAYERS");
        }
    }
}