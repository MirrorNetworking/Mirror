#if ENABLE_UNET

using System;
using UnityEngine;
using UnityEngine.Networking.NetworkSystem;
using UnityEngine.SceneManagement;

namespace UnityEngine.Networking
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkLobbyPlayer")]
    public class NetworkLobbyPlayer : NetworkBehaviour
    {
        [Tooltip("Enable to show the default lobby GUI for this player.")]
        [SerializeField] public bool ShowLobbyGUI = true;

        byte m_Slot;
        bool m_ReadyToBegin;

        public byte slot { get { return m_Slot; } set { m_Slot = value; }}
        public bool readyToBegin { get { return m_ReadyToBegin; } set { m_ReadyToBegin = value; } }

        void Start()
        {
            DontDestroyOnLoad(gameObject);
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        public override void OnStartClient()
        {
            var lobby = NetworkManager.singleton as NetworkLobbyManager;
            if (lobby)
            {
                lobby.lobbySlots[m_Slot] = this;
                m_ReadyToBegin = false;
                OnClientEnterLobby();
            }
            else
            {
                Debug.LogError("LobbyPlayer could not find a NetworkLobbyManager. The LobbyPlayer requires a NetworkLobbyManager object to function. Make sure that there is one in the scene.");
            }
        }

        public void SendReadyToBeginMessage()
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkLobbyPlayer SendReadyToBeginMessage"); }

            var lobby = NetworkManager.singleton as NetworkLobbyManager;
            if (lobby)
            {
                var msg = new LobbyReadyToBeginMessage();
                msg.slotId = (byte)playerControllerId;
                msg.readyState = true;
                lobby.client.Send(MsgType.LobbyReadyToBegin, msg);
            }
        }

        public void SendNotReadyToBeginMessage()
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkLobbyPlayer SendReadyToBeginMessage"); }

            var lobby = NetworkManager.singleton as NetworkLobbyManager;
            if (lobby)
            {
                var msg = new LobbyReadyToBeginMessage();
                msg.slotId = (byte)playerControllerId;
                msg.readyState = false;
                lobby.client.Send(MsgType.LobbyReadyToBegin, msg);
            }
        }

        public void SendSceneLoadedMessage()
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkLobbyPlayer SendSceneLoadedMessage"); }

            var lobby = NetworkManager.singleton as NetworkLobbyManager;
            if (lobby)
            {
                var msg = new IntegerMessage(playerControllerId);
                lobby.client.Send(MsgType.LobbySceneLoaded, msg);
            }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var lobby = NetworkManager.singleton as NetworkLobbyManager;
            if (lobby)
            {
                // dont even try this in the startup scene
                // Should we check if the LoadSceneMode is Single or Additive??
                // Can the lobby scene be loaded Additively??
                string loadedSceneName = scene.name;
                if (loadedSceneName == lobby.lobbyScene)
                {
                    return;
                }
            }

            if (isLocalPlayer)
            {
                SendSceneLoadedMessage();
            }
        }

        public void RemovePlayer()
        {
            if (isLocalPlayer && !m_ReadyToBegin)
            {
                if (LogFilter.logDebug) { Debug.Log("NetworkLobbyPlayer RemovePlayer"); }

                ClientScene.RemovePlayer(GetComponent<NetworkIdentity>().playerControllerId);
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

        // ------------------------ Custom Serialization ------------------------

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            // dirty flag
            writer.WritePackedUInt32(1);

            writer.Write(m_Slot);
            writer.Write(m_ReadyToBegin);
            return true;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            var dirty = reader.ReadPackedUInt32();
            if (dirty == 0)
                return;

            m_Slot = reader.ReadByte();
            m_ReadyToBegin = reader.ReadBoolean();
        }

        // ------------------------ optional UI ------------------------

        void OnGUI()
        {
            if (!ShowLobbyGUI)
                return;

            var lobby = NetworkManager.singleton as NetworkLobbyManager;
            if (lobby)
            {
                if (!lobby.showLobbyGUI)
                    return;

                string loadedSceneName = SceneManager.GetSceneAt(0).name;
                if (loadedSceneName != lobby.lobbyScene)
                    return;
            }

            Rect rec = new Rect(100 + m_Slot * 100, 200, 90, 20);

            if (isLocalPlayer)
            {
                string youStr;
                if (m_ReadyToBegin)
                {
                    youStr = "(Ready)";
                }
                else
                {
                    youStr = "(Not Ready)";
                }
                GUI.Label(rec, youStr);

                if (m_ReadyToBegin)
                {
                    rec.y += 25;
                    if (GUI.Button(rec, "STOP"))
                    {
                        SendNotReadyToBeginMessage();
                    }
                }
                else
                {
                    rec.y += 25;
                    if (GUI.Button(rec, "START"))
                    {
                        SendReadyToBeginMessage();
                    }

                    rec.y += 25;
                    if (GUI.Button(rec, "Remove"))
                    {
                        ClientScene.RemovePlayer(GetComponent<NetworkIdentity>().playerControllerId);
                    }
                }
            }
            else
            {
                GUI.Label(rec, "Player [" + netId + "]");
                rec.y += 25;
                GUI.Label(rec, "Ready [" + m_ReadyToBegin + "]");
            }
        }
    }
}

#endif // ENABLE_UNET
