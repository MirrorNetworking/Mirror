using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkLobbyPlayer")]
    [HelpURL("https://vis2k.github.io/Mirror/Components/NetworkLobbyPlayer")]
    public class NetworkLobbyPlayer : NetworkBehaviour
    {
        public bool ShowLobbyGUI = true;

        [SyncVar]
        public bool ReadyToBegin;

        [SyncVar]
        public int Index;

        /// <summary>
        /// Do not use Start - Override OnStartrHost / OnStartClient instead!
        /// </summary>
        public void Start()
        {
            if (isClient) SceneManager.sceneLoaded += ClientLoadedScene;

            if (NetworkManager.singleton as NetworkLobbyManager)
                OnClientEnterLobby();
            else
                Debug.LogError("LobbyPlayer could not find a NetworkLobbyManager. The LobbyPlayer requires a NetworkLobbyManager object to function. Make sure that there is one in the scene.");
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= ClientLoadedScene;
        }

        public virtual void ClientLoadedScene(Scene arg0, LoadSceneMode arg1)
        {
            NetworkLobbyManager lobby = NetworkManager.singleton as NetworkLobbyManager;
            if (lobby != null && SceneManager.GetActiveScene().name == lobby.LobbyScene)
                return;

            if (this != null && isLocalPlayer)
                CmdSendLevelLoaded();
        }

        [Command]
        public void CmdChangeReadyState(bool ReadyState)
        {
            ReadyToBegin = ReadyState;
            NetworkLobbyManager lobby = NetworkManager.singleton as NetworkLobbyManager;
            lobby?.ReadyStatusChanged();
        }

        [Command]
        public void CmdSendLevelLoaded()
        {
            NetworkLobbyManager lobby = NetworkManager.singleton as NetworkLobbyManager;
            lobby?.PlayerLoadedScene(GetComponent<NetworkIdentity>().connectionToClient);
        }

        #region lobby client virtuals

        public virtual void OnClientEnterLobby() {}

        public virtual void OnClientExitLobby() {}

        public virtual void OnClientReady(bool readyState) {}

        #endregion

        #region optional UI

        public virtual void OnGUI()
        {
            if (!ShowLobbyGUI)
                return;

            NetworkLobbyManager lobby = NetworkManager.singleton as NetworkLobbyManager;
            if (lobby)
            {
                if (!lobby.showLobbyGUI)
                    return;

                if (SceneManager.GetActiveScene().name != lobby.LobbyScene)
                    return;

                GUILayout.BeginArea(new Rect(20f + (Index * 100), 200f, 90f, 130f));

                GUILayout.Label($"Player [{Index + 1}]");

                if (ReadyToBegin)
                    GUILayout.Label("Ready");
                else
                    GUILayout.Label("Not Ready");

                if (isServer && Index > 0 && GUILayout.Button("REMOVE"))
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

                    if (ReadyToBegin)
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
