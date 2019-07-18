using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkLobbyPlayer")]
    [HelpURL("https://vis2k.github.io/Mirror/Components/NetworkLobbyPlayer")]
    public class NetworkLobbyPlayer : NetworkBehaviour
    {
        public bool showLobbyGUI = true;

        [SyncVar(hook=nameof(ReadyStateChanged))]
        public bool readyToBegin;

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

        public virtual void OnClientEnterLobby() {}

        public virtual void OnClientExitLobby() {}

        public virtual void OnClientReady(bool readyState) {}

        #endregion

        #region Optional UI

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
