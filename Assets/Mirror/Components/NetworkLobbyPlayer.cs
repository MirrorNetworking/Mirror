using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Components.NetworkLobby
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkLobbyPlayer")]
    public class NetworkLobbyPlayer : NetworkBehaviour
    {

        [SerializeField] public bool ShowLobbyGUI = true;

		[SyncVar]
        public bool ReadyToBegin = false;

		[SyncVar]
		public int Index;

        void Start()
        {
            DontDestroyOnLoad(gameObject);
        }

        public override void OnStartClient()
        {
            var lobby = NetworkManager.singleton as NetworkLobbyManager;
            if (lobby)
            {
                //lobby.lobbySlots[Slot] = this;
                ReadyToBegin = false;
                OnClientEnterLobby();
            }
            else
            {
                Debug.LogError("LobbyPlayer could not find a NetworkLobbyManager. The LobbyPlayer requires a NetworkLobbyManager object to function. Make sure that there is one in the scene.");
            }
        }

		[Command]
		public void CmdChangeReadyState(bool ReadyState)
		{
			ReadyToBegin = ReadyState;
			var lobby = NetworkManager.singleton as NetworkLobbyManager;
			if (lobby)
			{
				lobby.ReadyStatusChanged();
			}
		}

        void OnLevelWasLoaded()
        {
            var lobby = NetworkManager.singleton as NetworkLobbyManager;
            if (lobby)
            {
                // dont even try this in the startup scene
                string loadedSceneName = SceneManager.GetSceneAt(0).name;
                if (loadedSceneName == lobby.LobbyScene)
                {
                    return;
                }
            }

            if (isLocalPlayer)
            {
                //SendSceneLoadedMessage();
            }
        }

        public void RemovePlayer()
        {
            if (isLocalPlayer && !ReadyToBegin)
            {
                if (LogFilter.Debug) { Debug.Log("NetworkLobbyPlayer RemovePlayer"); }

                ClientScene.RemovePlayer();
            }
        }

        // ------------------------ callbacks ------------------------

        public virtual void OnClientEnterLobby()
        {
        }

        public virtual void OnClientExitLobby()
        {
        }

        public virtual void OnClientReady(bool readyState)
        {
        }

        // ------------------------ optional UI ------------------------

        void OnGUI()
        {
            if (!ShowLobbyGUI)
                return;

            var lobby = NetworkManager.singleton as NetworkLobbyManager;
            if (lobby)
            {
                if (!lobby.m_ShowLobbyGUI)
                    return;

                string loadedSceneName = SceneManager.GetSceneAt(0).name;
                if (loadedSceneName != lobby.LobbyScene)
                    return;
            }

            Rect rec = new Rect(100 + Index * 100, 200, 90, 20);

            if (isLocalPlayer)
            {
                string youStr;
                if (ReadyToBegin)
                {
                    youStr = "(Ready)";
                }
                else
                {
                    youStr = "(Not Ready)";
                }
                GUI.Label(rec, youStr);

                if (ReadyToBegin)
                {
                    rec.y += 25;
                    if (GUI.Button(rec, "STOP"))
                    {
						CmdChangeReadyState(false);
                    }
                }
                else
                {
                    rec.y += 25;
                    if (GUI.Button(rec, "Ready"))
                    {
						CmdChangeReadyState(true);
                    }

                    rec.y += 25;
                    if (GUI.Button(rec, "Remove"))
                    {
                        ClientScene.RemovePlayer();
                    }
                }
            }
            else
            {
                GUI.Label(rec, "Player [" + netId + "]");
                rec.y += 25;
                GUI.Label(rec, "Ready [" + ReadyToBegin + "]");
            }
        }
    }
}