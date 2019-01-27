using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Components.NetworkLobby
{
    [AddComponentMenu("Network/NetworkLobbyManager")]
    public class NetworkLobbyManager : NetworkManager
    {

        struct PendingPlayer
        {
            public NetworkConnection conn;
            public GameObject lobbyPlayer;
        }

        // configuration
        [SerializeField] internal bool m_ShowLobbyGUI = true;
        [SerializeField] int m_MaxPlayers = 4;
        [SerializeField] int m_MinPlayers;
        [SerializeField] NetworkLobbyPlayer m_LobbyPlayerPrefab;

		[SerializeField, Scene]
		internal string LobbyScene;

		[SerializeField, Scene]
		internal string GameplayScene;

		// runtime data
		List<PendingPlayer> m_PendingPlayers = new List<PendingPlayer>();
        public List<NetworkLobbyPlayer> lobbySlots = new List<NetworkLobbyPlayer>();

        // static message objects to avoid runtime-allocations
        static IntegerMessage s_SceneLoadedMessage = new IntegerMessage();

        public override void OnValidate()
        {
            if (m_MaxPlayers <= 0)
            {
                m_MaxPlayers = 1;
            }

            if (m_MinPlayers < 0)
            {
                m_MinPlayers = 0;
            }

            if (m_MinPlayers > m_MaxPlayers)
            {
                m_MinPlayers = m_MaxPlayers;
            }

            if (m_LobbyPlayerPrefab != null)
            {
                var uv = m_LobbyPlayerPrefab.GetComponent<NetworkIdentity>();
                if (uv == null)
                {
                    m_LobbyPlayerPrefab = null;
                    Debug.LogWarning("LobbyPlayer prefab must have a NetworkIdentity component.");
                }
            }

            if (playerPrefab != null)
            {
                var uv = playerPrefab.GetComponent<NetworkIdentity>();
                if (uv == null)
                {
                    playerPrefab = null;
                    Debug.LogWarning("GamePlayer prefab must have a NetworkIdentity component.");
                }
            }

            base.OnValidate();
        }

		internal void ReadyStatusChanged()
		{
			int CurrentPlayers = 0;
			int ReadyPlayers = 0;
			foreach (var item in lobbySlots)
			{
				if (item != null)
				{
					CurrentPlayers++;
					if (item.ReadyToBegin)
					{
						ReadyPlayers++;
					}
				}
			}
			if (CurrentPlayers == ReadyPlayers)
			{
				CheckReadyToBegin();
			}
		}


        void SceneLoadedForPlayer(NetworkConnection conn, GameObject lobbyPlayerGameObject)
        {
            var lobbyPlayer = lobbyPlayerGameObject.GetComponent<NetworkLobbyPlayer>();
            if (lobbyPlayer == null)
            {
                // not a lobby player.. dont replace it
                return;
            }

            string loadedSceneName = SceneManager.GetSceneAt(0).name;
            if (LogFilter.Debug) { Debug.Log("NetworkLobby SceneLoadedForPlayer scene:" + loadedSceneName + " " + conn); }

            if (loadedSceneName == LobbyScene)
            {
                // cant be ready in lobby, add to ready list
                PendingPlayer pending;
                pending.conn = conn;
                pending.lobbyPlayer = lobbyPlayerGameObject;
                m_PendingPlayers.Add(pending);
                return;
            }

            var gamePlayer = OnLobbyServerCreateGamePlayer(conn);
            if (gamePlayer == null)
            {
                // get start position from base class
                Transform startPos = GetStartPosition();
                if (startPos != null)
                {
                    gamePlayer = (GameObject)Instantiate(playerPrefab, startPos.position, startPos.rotation);
                }
                else
                {
                    gamePlayer = (GameObject)Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
                }
            }

            if (!OnLobbyServerSceneLoadedForPlayer(lobbyPlayerGameObject, gamePlayer))
            {
                return;
            }

            // replace lobby player with game player
            NetworkServer.ReplacePlayerForConnection(conn, gamePlayer);
        }

        static int CheckConnectionIsReadyToBegin(NetworkConnection conn)
        {
            int countPlayers = 0;
            var player = conn.playerController;
            var lobbyPlayer = player.gameObject.GetComponent<NetworkLobbyPlayer>();
            if (lobbyPlayer.ReadyToBegin)
            {
                countPlayers += 1;
            }
            return countPlayers;
        }

        public void CheckReadyToBegin()
        {
            string loadedSceneName = SceneManager.GetSceneAt(0).name;
            if (loadedSceneName != LobbyScene)
            {
                return;
            }

            int readyCount = 0;

            foreach (var conn in NetworkServer.connections)
            {
                if (conn.Value == null)
                    continue;

                readyCount += CheckConnectionIsReadyToBegin(conn.Value);
            }
            if (m_MinPlayers > 0 && readyCount < m_MinPlayers)
            {
                // not enough players ready yet.
                return;
            }

            m_PendingPlayers.Clear();
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

        void CallOnClientEnterLobby()
        {
            OnLobbyClientEnter();
            foreach (var player in lobbySlots)
            {
                if (player == null)
                    continue;

                player.OnClientEnterLobby();
            }
        }

        void CallOnClientExitLobby()
        {
            OnLobbyClientExit();
            foreach (var player in lobbySlots)
            {
                if (player == null)
                    continue;

                player.OnClientExitLobby();
            }
        }

        public bool SendReturnToLobby()
        {
            if (client == null || !client.isConnected)
            {
                return false;
            }

            var msg = new EmptyMessage();
            client.Send((short)MsgType.LobbyReturnToLobby, msg);
            return true;
        }

        // ------------------------ server handlers ------------------------

        public override void OnServerConnect(NetworkConnection conn)
        {
            if (numPlayers >= m_MaxPlayers)
            {
                conn.Disconnect();
                return;
            }

            // cannot join game in progress
            string loadedSceneName = SceneManager.GetSceneAt(0).name;
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
            base.OnServerDisconnect(conn);

            // if lobbyplayer for this connection has not been destroyed by now, then destroy it here
            for (int i = 0; i < lobbySlots.Count; i++)
            {
                var player = lobbySlots[i];
                if (player == null)
                    continue;

                if (player.connectionToClient == conn)
                {
                    lobbySlots[i] = null;
                    NetworkServer.Destroy(player.gameObject);
                }
            }

            OnLobbyServerDisconnect(conn);
        }

        public override void OnServerAddPlayer(NetworkConnection conn)
        {
            string loadedSceneName = SceneManager.GetSceneAt(0).name;
            if (loadedSceneName != LobbyScene)
            {
                return;
            }

            var newLobbyGameObject = OnLobbyServerCreateLobbyPlayer(conn);
            if (newLobbyGameObject == null)
            {
                newLobbyGameObject = (GameObject)Instantiate(m_LobbyPlayerPrefab.gameObject, Vector3.zero, Quaternion.identity);
            }

            var newLobbyPlayer = newLobbyGameObject.GetComponent<NetworkLobbyPlayer>();

            lobbySlots.Add(newLobbyPlayer);

			RecalculateLobbyPlayerIndices();

            NetworkServer.AddPlayerForConnection(conn, newLobbyGameObject);
        }

		private void RecalculateLobbyPlayerIndices()
		{
			for (int i = 0; i < lobbySlots.Count; i++)
			{
				lobbySlots[i].Index = i;
			}
		}

		public override void OnServerRemovePlayer(NetworkConnection conn, NetworkIdentity player)
        {
			var netLobbyPlayer = player.gameObject.GetComponent<NetworkLobbyPlayer>();
			lobbySlots.Remove(netLobbyPlayer);
            base.OnServerRemovePlayer(conn, player);

            foreach (var p in lobbySlots)
            {
                if (p != null)
                {
                    p.GetComponent<NetworkLobbyPlayer>().ReadyToBegin = false;
                }
            }

            OnLobbyServerPlayerRemoved(conn);
        }

        public override void ServerChangeScene(string sceneName)
        {
            if (sceneName == LobbyScene)
            {
                foreach (var lobbyPlayer in lobbySlots)
                {
                    if (lobbyPlayer == null)
                        continue;

                    // find the game-player object for this connection, and destroy it
                    var uv = lobbyPlayer.GetComponent<NetworkIdentity>();

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
            base.ServerChangeScene(sceneName);
        }

        public override void OnServerSceneChanged(string sceneName)
        {
            if (sceneName != LobbyScene)
            {
                // call SceneLoadedForPlayer on any players that become ready while we were loading the scene.
                foreach (var pending in m_PendingPlayers)
                {
                    SceneLoadedForPlayer(pending.conn, pending.lobbyPlayer);
                }
                m_PendingPlayers.Clear();
            }

            OnLobbyServerSceneChanged(sceneName);
        }

        void OnServerSceneLoadedMessage(NetworkMessage netMsg)
        {
            if (LogFilter.Debug) { Debug.Log("NetworkLobbyManager OnSceneLoadedMessage"); }

            netMsg.ReadMessage(s_SceneLoadedMessage);

            NetworkIdentity lobbyController = netMsg.conn.playerController;

            SceneLoadedForPlayer(netMsg.conn, lobbyController.gameObject);
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

            NetworkServer.RegisterHandler((short)MsgType.LobbySceneLoaded, OnServerSceneLoadedMessage);
            NetworkServer.RegisterHandler((short)MsgType.LobbyReturnToLobby, OnServerReturnToLobbyMessage);

            OnLobbyStartServer();
        }

        public override void OnStartHost()
        {
            OnLobbyStartHost();
        }

        public override void OnStopHost()
        {
            OnLobbyStopHost();
        }

        // ------------------------ client handlers ------------------------

        public override void OnStartClient(NetworkClient lobbyClient)
        {

            if (m_LobbyPlayerPrefab == null || m_LobbyPlayerPrefab.gameObject == null)
            {
                if (LogFilter.Debug) { Debug.LogError("NetworkLobbyManager no LobbyPlayer prefab is registered. Please add a LobbyPlayer prefab."); }
            }
            else
            {
                ClientScene.RegisterPrefab(m_LobbyPlayerPrefab.gameObject);
            }

            if (playerPrefab == null)
            {
                if (LogFilter.Debug) { Debug.LogError("NetworkLobbyManager no GamePlayer prefab is registered. Please add a GamePlayer prefab."); }
            }
            else
            {
                ClientScene.RegisterPrefab(playerPrefab);
            }

            lobbyClient.RegisterHandler((short)MsgType.LobbyAddPlayerFailed, OnClientAddPlayerFailedMessage);

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
        }

        public override void OnClientSceneChanged(NetworkConnection conn)
        {
            string loadedSceneName = SceneManager.GetSceneAt(0).name;
            if (loadedSceneName == LobbyScene)
            {
                if (client.isConnected)
                {
                    CallOnClientEnterLobby();
                }
            }
            else
            {
                CallOnClientExitLobby();
            }

            base.OnClientSceneChanged(conn);
            OnLobbyClientSceneChanged(conn);
        }

        void OnClientAddPlayerFailedMessage(NetworkMessage netMsg)
        {
            if (LogFilter.Debug) { Debug.Log("NetworkLobbyManager Add Player failed."); }
            OnLobbyClientAddPlayerFailed();
        }

        // ------------------------ lobby server virtuals ------------------------

        public virtual void OnLobbyStartHost()
        {
        }

        public virtual void OnLobbyStopHost()
        {
        }

        public virtual void OnLobbyStartServer()
        {
        }

        public virtual void OnLobbyServerConnect(NetworkConnection conn)
        {
        }

        public virtual void OnLobbyServerDisconnect(NetworkConnection conn)
        {
        }

        public virtual void OnLobbyServerSceneChanged(string sceneName)
        {
        }

        public virtual GameObject OnLobbyServerCreateLobbyPlayer(NetworkConnection conn)
        {
            return null;
        }

        public virtual GameObject OnLobbyServerCreateGamePlayer(NetworkConnection conn)
        {
            return null;
        }

        public virtual void OnLobbyServerPlayerRemoved(NetworkConnection conn)
        {
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

        public virtual void OnLobbyClientEnter()
        {
        }

        public virtual void OnLobbyClientExit()
        {
        }

        public virtual void OnLobbyClientConnect(NetworkConnection conn)
        {
        }

        public virtual void OnLobbyClientDisconnect(NetworkConnection conn)
        {
        }

        public virtual void OnLobbyStartClient(NetworkClient lobbyClient)
        {
        }

        public virtual void OnLobbyStopClient()
        {
        }

        public virtual void OnLobbyClientSceneChanged(NetworkConnection conn)
        {
        }

        // for users to handle adding a player failed on the server
        public virtual void OnLobbyClientAddPlayerFailed()
        {
        }

        // ------------------------ optional UI ------------------------

        void OnGUI()
        {
            if (!m_ShowLobbyGUI)
                return;

            string loadedSceneName = SceneManager.GetSceneAt(0).name;
            if (loadedSceneName != LobbyScene)
                return;

            Rect backgroundRec = new Rect(90, 180, 500, 150);
            GUI.Box(backgroundRec, "Players:");

            if (NetworkClient.active)
            {
                Rect addRec = new Rect(100, 300, 120, 20);
                if (GUI.Button(addRec, "Add Player"))
                {
                    TryToAddPlayer();
                }
            }
        }

        public void TryToAddPlayer()
        {
            if (NetworkClient.active)
            {
                short controllerId = -1;
                var controller = NetworkClient.allClients[0].connection.playerController;

                if (controller != null)
                {
                    controllerId = 0;
                }
                if (LogFilter.Debug) { Debug.Log("NetworkLobbyManager TryToAddPlayer controllerId " + controllerId + " ready:" + ClientScene.ready); }

                if (controllerId == -1)
                {
                    if (LogFilter.Debug) { Debug.Log("NetworkLobbyManager No Space!"); }
                    return;
                }

                if (ClientScene.ready)
                {
                    ClientScene.AddPlayer();
                }
                else
                {
                    ClientScene.AddPlayer(NetworkClient.allClients[0].connection);
                }
            }
            else
            {
                if (LogFilter.Debug) { Debug.Log("NetworkLobbyManager NetworkClient not active!"); }
            }
        }
    }

}