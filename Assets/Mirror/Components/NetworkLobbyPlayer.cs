using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkLobbyPlayer")]
    [HelpURL("https://vis2k.github.io/Mirror/Components/NetworkLobbyPlayer")]
    public class NetworkLobbyPlayer : NetworkBehaviour
    {
        [SerializeField] public bool ShowLobbyGUI = true;

        [SyncVar]
        public bool ReadyToBegin = false;

        [SyncVar]
        public int Index;

        /// <summary>
        /// Do not use Start - Override OnStartrHost / OnStartClient instead!
        /// </summary>
        public void Start()
        {
            if (isClient) SceneManager.sceneLoaded += ClientLoadedScene;

            if (NetworkManager.singleton as NetworkLobbyManager)
            {
                ReadyToBegin = false;
                OnClientEnterLobby();
            }
            else
                Debug.LogError("LobbyPlayer could not find a NetworkLobbyManager. The LobbyPlayer requires a NetworkLobbyManager object to function. Make sure that there is one in the scene.");
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= ClientLoadedScene;
        }

        void ClientLoadedScene(Scene arg0, LoadSceneMode arg1)
        {
            NetworkLobbyManager lobby = NetworkManager.singleton as NetworkLobbyManager;
            if (lobby)
            {
                // dont even try this in the startup scene
                if (SceneManager.GetActiveScene().name == lobby.LobbyScene)
                {
                    Application.targetFrameRate = 10;
                    return;
                }
                else
                    Application.targetFrameRate = 60;
            }

            if (this != null && isLocalPlayer)
                CmdSendLevelLoaded();
        }

        [Command]
        public void CmdChangeReadyState(bool ReadyState)
        {
            ReadyToBegin = ReadyState;
            NetworkLobbyManager lobby = NetworkManager.singleton as NetworkLobbyManager;
            if (lobby)
                lobby.ReadyStatusChanged();
        }

        [Command]
        public void CmdSendLevelLoaded()
        {
            NetworkLobbyManager lobby = NetworkManager.singleton as NetworkLobbyManager;
            if (lobby)
                lobby.PlayerLoadedScene(GetComponent<NetworkIdentity>().connectionToClient);
        }

        #region lobby client virtuals

        public virtual void OnClientEnterLobby() { }

        public virtual void OnClientExitLobby() { }

        public virtual void OnClientReady(bool readyState) { }

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

                Rect rec = new Rect(20 + Index * 100, 200, 90, 20);

                GUI.Label(rec, String.Format("Player [{0}]", Index + 1));

                rec.y += 25;
                if (ReadyToBegin)
                    GUI.Label(rec, "Ready");
                else
                    GUI.Label(rec, "Not Ready");

                rec.y += 25;
                if (isServer && Index > 0 && GUI.Button(rec, "REMOVE"))
                {
                    // This button only shows on the Host for all players other than the Host
                    // Host and Players can't remove themselves (stop the client instead)
                    // Host can kick a Player this way.
                    GetComponent<NetworkIdentity>().clientAuthorityOwner.Disconnect();
                }

                if (NetworkClient.active && isLocalPlayer)
                {
                    Rect readyCancelRect = new Rect(20, 300, 120, 20);

                    if (ReadyToBegin)
                    {
                        if (GUI.Button(readyCancelRect, "Cancel"))
                            CmdChangeReadyState(false);
                    }
                    else
                    {
                        if (GUI.Button(readyCancelRect, "Ready"))
                            CmdChangeReadyState(true);
                    }
                }
            }
        }

        #endregion
    }
}