#if ENABLE_UNET
#if ENABLE_UNET_HOST_MIGRATION

using System;
using System.Collections.Generic;
using UnityEngine.Networking.Match;
using UnityEngine.Networking.NetworkSystem;
using UnityEngine.Networking.Types;

namespace UnityEngine.Networking
{
    [AddComponentMenu("Network/NetworkMigrationManager")]
    public class NetworkMigrationManager : MonoBehaviour
    {
        public enum SceneChangeOption
        {
            StayInOnlineScene,
            SwitchToOfflineScene
        }

        [SerializeField]
        bool m_HostMigration = true;

        [SerializeField]
        bool m_ShowGUI = true;

        [SerializeField]
        int m_OffsetX = 10;

        [SerializeField]
        int m_OffsetY = 300;

        NetworkClient m_Client;
        bool m_WaitingToBecomeNewHost;
        bool m_WaitingReconnectToNewHost;
        bool m_DisconnectedFromHost;
        bool m_HostWasShutdown;

        MatchInfo m_MatchInfo;
        int m_OldServerConnectionId = -1;
        string m_NewHostAddress;

        PeerInfoMessage m_NewHostInfo = new PeerInfoMessage();
        PeerListMessage m_PeerListMessage = new PeerListMessage();

        PeerInfoMessage[] m_Peers;

        // There can be multiple pending players for a connectionId, distinguished by oldNetId/playerControllerId
        public struct PendingPlayerInfo
        {
            public NetworkInstanceId netId;
            public short playerControllerId;
            public GameObject obj;
        }

        public struct ConnectionPendingPlayers
        {
            public List<PendingPlayerInfo> players;
        }
        Dictionary<int, ConnectionPendingPlayers> m_PendingPlayers = new Dictionary<int, ConnectionPendingPlayers>();

        void AddPendingPlayer(GameObject obj, int connectionId, NetworkInstanceId netId, short playerControllerId)
        {
            if (!m_PendingPlayers.ContainsKey(connectionId))
            {
                var pending = new ConnectionPendingPlayers();
                pending.players = new List<PendingPlayerInfo>();
                m_PendingPlayers[connectionId] = pending;
            }
            PendingPlayerInfo info = new PendingPlayerInfo();
            info.netId = netId;
            info.playerControllerId = playerControllerId;
            info.obj = obj;
            m_PendingPlayers[connectionId].players.Add(info);
        }

        GameObject FindPendingPlayer(int connectionId, NetworkInstanceId netId, short playerControllerId)
        {
            if (m_PendingPlayers.ContainsKey(connectionId))
            {
                for (int i = 0; i < m_PendingPlayers[connectionId].players.Count; i++)
                {
                    var info = m_PendingPlayers[connectionId].players[i];
                    if (info.netId == netId && info.playerControllerId == playerControllerId)
                    {
                        return info.obj;
                    }
                }
            }
            return null;
        }

        void RemovePendingPlayer(int connectionId)
        {
            m_PendingPlayers.Remove(connectionId);
        }

        public bool hostMigration
        {
            get { return m_HostMigration; }
            set { m_HostMigration = value; }
        }

        public bool showGUI
        {
            get { return m_ShowGUI; }
            set { m_ShowGUI = value; }
        }

        public int offsetX
        {
            get { return m_OffsetX; }
            set { m_OffsetX = value; }
        }

        public int offsetY
        {
            get { return m_OffsetY; }
            set { m_OffsetY = value; }
        }

        public NetworkClient client
        {
            get { return m_Client; }
        }

        public bool waitingToBecomeNewHost
        {
            get { return m_WaitingToBecomeNewHost; }
            set { m_WaitingToBecomeNewHost = value; }
        }

        public bool waitingReconnectToNewHost
        {
            get { return m_WaitingReconnectToNewHost; }
            set { m_WaitingReconnectToNewHost = value; }
        }

        public bool disconnectedFromHost
        {
            get { return m_DisconnectedFromHost; }
        }

        public bool hostWasShutdown
        {
            get { return m_HostWasShutdown; }
        }

        public MatchInfo matchInfo
        {
            get { return m_MatchInfo; }
        }

        public int oldServerConnectionId
        {
            get { return m_OldServerConnectionId; }
        }

        public string newHostAddress
        {
            get { return m_NewHostAddress; }
            set { m_NewHostAddress = value; }
        }

        public PeerInfoMessage[] peers
        {
            get { return m_Peers; }
        }

        public Dictionary<int, ConnectionPendingPlayers> pendingPlayers
        {
            get { return m_PendingPlayers; }
        }

        void Start()
        {
            Reset(ClientScene.ReconnectIdInvalid);
        }

        public void Reset(int reconnectId)
        {
            m_OldServerConnectionId = -1;
            m_WaitingToBecomeNewHost = false;
            m_WaitingReconnectToNewHost = false;
            m_DisconnectedFromHost = false;
            m_HostWasShutdown = false;
            ClientScene.SetReconnectId(reconnectId, m_Peers);

            if (NetworkManager.singleton != null)
            {
                NetworkManager.singleton.SetupMigrationManager(this);
            }
        }

        internal void AssignAuthorityCallback(NetworkConnection conn, NetworkIdentity uv, bool authorityState)
        {
            var msg = new PeerAuthorityMessage();
            msg.connectionId = conn.connectionId;
            msg.netId = uv.netId;
            msg.authorityState = authorityState;

            if (LogFilter.logDebug) { Debug.Log("AssignAuthorityCallback send for netId" + uv.netId); }

            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                var c = NetworkServer.connections[i];
                if (c != null)
                {
                    c.Send(MsgType.PeerClientAuthority, msg);
                }
            }
        }

        public void Initialize(NetworkClient newClient, MatchInfo newMatchInfo)
        {
            if (LogFilter.logDev) { Debug.Log("NetworkMigrationManager initialize"); }

            m_Client = newClient;
            m_MatchInfo = newMatchInfo;
            newClient.RegisterHandlerSafe(MsgType.NetworkInfo, OnPeerInfo);
            newClient.RegisterHandlerSafe(MsgType.PeerClientAuthority, OnPeerClientAuthority);

            NetworkIdentity.clientAuthorityCallback = AssignAuthorityCallback;
        }

        public void DisablePlayerObjects()
        {
            if (LogFilter.logDev) { Debug.Log("NetworkMigrationManager DisablePlayerObjects"); }

            if (m_Peers == null)
                return;

            for (int peerId = 0; peerId < m_Peers.Length; peerId++)
            {
                var peer = m_Peers[peerId];
                if (peer.playerIds != null)
                {
                    for (int i = 0; i < peer.playerIds.Length; i++)
                    {
                        var info = peer.playerIds[i];
                        if (LogFilter.logDev) { Debug.Log("DisablePlayerObjects disable player for " + peer.address + " netId:" + info.netId + " control:" + info.playerControllerId); }

                        GameObject playerObj = ClientScene.FindLocalObject(info.netId);
                        if (playerObj != null)
                        {
                            playerObj.SetActive(false);

                            AddPendingPlayer(playerObj, peer.connectionId, info.netId, info.playerControllerId);
                        }
                        else
                        {
                            if (LogFilter.logWarn) { Debug.LogWarning("DisablePlayerObjects didnt find player Conn:" + peer.connectionId + " NetId:" + info.netId); }
                        }
                    }
                }
            }
        }

        public void SendPeerInfo()
        {
            if (!m_HostMigration)
                return;

            var listMsg = new PeerListMessage();
            var addresses = new List<PeerInfoMessage>();

            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                var conn = NetworkServer.connections[i];
                if (conn != null)
                {
                    var peerInfo = new PeerInfoMessage();

                    string address;
                    int port;
                    NetworkID networkId;
                    NodeID node;
                    byte error2;
                    NetworkTransport.GetConnectionInfo(NetworkServer.serverHostId, conn.connectionId, out address, out port, out networkId, out node, out error2);

                    peerInfo.connectionId = conn.connectionId;
                    peerInfo.port = port;
                    if (i == 0)
                    {
                        peerInfo.port = NetworkServer.listenPort;
                        peerInfo.isHost = true;
                        peerInfo.address = "<host>";
                    }
                    else
                    {
                        peerInfo.address = address;
                        peerInfo.isHost = false;
                    }
                    var playerIds = new List<PeerInfoPlayer>();
                    for (int pid = 0; pid < conn.playerControllers.Count; pid++)
                    {
                        var player = conn.playerControllers[pid];
                        if (player != null && player.unetView != null)
                        {
                            PeerInfoPlayer info;
                            info.netId = player.unetView.netId;
                            info.playerControllerId = player.unetView.playerControllerId;
                            playerIds.Add(info);
                        }
                    }

                    if (conn.clientOwnedObjects != null)
                    {
                        foreach (var netId in conn.clientOwnedObjects)
                        {
                            var obj = NetworkServer.FindLocalObject(netId);
                            if (obj == null)
                                continue;

                            var objUV = obj.GetComponent<NetworkIdentity>();
                            if (objUV.playerControllerId != -1)
                            {
                                // already added players
                                continue;
                            }

                            PeerInfoPlayer info;
                            info.netId = netId;
                            info.playerControllerId = -1;
                            playerIds.Add(info);
                        }
                    }
                    if (playerIds.Count > 0)
                    {
                        peerInfo.playerIds = playerIds.ToArray();
                    }
                    addresses.Add(peerInfo);
                }
            }

            listMsg.peers = addresses.ToArray();

            // (re)send all peers to all peers (including the new one)
            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                var conn = NetworkServer.connections[i];
                if (conn != null)
                {
                    listMsg.oldServerConnectionId = conn.connectionId;
                    conn.Send(MsgType.NetworkInfo, listMsg);
                }
            }
        }

        // received on both host and clients
        void OnPeerClientAuthority(NetworkMessage netMsg)
        {
            var msg = netMsg.ReadMessage<PeerAuthorityMessage>();

            if (LogFilter.logDebug) { Debug.Log("OnPeerClientAuthority for netId:" + msg.netId); }

            if (m_Peers == null)
            {
                // havent received peers yet. just ignore this. the peer list will contain this data.
                return;
            }

            // find the peer for connId
            for (int peerId = 0; peerId < m_Peers.Length; peerId++)
            {
                var p = m_Peers[peerId];
                if (p.connectionId == msg.connectionId)
                {
                    if (p.playerIds == null)
                    {
                        p.playerIds = new PeerInfoPlayer[0];
                    }

                    if (msg.authorityState)
                    {
                        for (int i = 0; i < p.playerIds.Length; i++)
                        {
                            if (p.playerIds[i].netId == msg.netId)
                            {
                                // already in list
                                return;
                            }
                        }
                        var newPlayerId = new PeerInfoPlayer();
                        newPlayerId.netId = msg.netId;
                        newPlayerId.playerControllerId = -1;

                        var pl = new List<PeerInfoPlayer>(p.playerIds);
                        pl.Add(newPlayerId);
                        p.playerIds = pl.ToArray();
                    }
                    else
                    {
                        for (int i = 0; i < p.playerIds.Length; i++)
                        {
                            if (p.playerIds[i].netId == msg.netId)
                            {
                                var pl = new List<PeerInfoPlayer>(p.playerIds);
                                pl.RemoveAt(i);
                                p.playerIds = pl.ToArray();
                                break;
                            }
                        }
                    }
                }
            }

            var foundObj = ClientScene.FindLocalObject(msg.netId);
            OnAuthorityUpdated(foundObj, msg.connectionId, msg.authorityState);
        }

        // recieved on both host and clients
        void OnPeerInfo(NetworkMessage netMsg)
        {
            if (LogFilter.logDebug) { Debug.Log("OnPeerInfo"); }

            netMsg.ReadMessage(m_PeerListMessage);
            m_Peers = m_PeerListMessage.peers;
            m_OldServerConnectionId = m_PeerListMessage.oldServerConnectionId;

            for (int i = 0; i < m_Peers.Length; i++)
            {
                if (LogFilter.logDebug) { Debug.Log("peer conn " + m_Peers[i].connectionId + " your conn " + m_PeerListMessage.oldServerConnectionId); }

                if (m_Peers[i].connectionId == m_PeerListMessage.oldServerConnectionId)
                {
                    m_Peers[i].isYou = true;
                    break;
                }
            }
            OnPeersUpdated(m_PeerListMessage);
        }

        void OnServerReconnectPlayerMessage(NetworkMessage netMsg)
        {
            var msg = netMsg.ReadMessage<ReconnectMessage>();

            if (LogFilter.logDev) { Debug.Log("OnReconnectMessage: connId=" + msg.oldConnectionId + " playerControllerId:" + msg.playerControllerId + " netId:" + msg.netId); }

            var playerObject = FindPendingPlayer(msg.oldConnectionId, msg.netId, msg.playerControllerId);
            if (playerObject == null)
            {
                if (LogFilter.logError) { Debug.LogError("OnReconnectMessage connId=" + msg.oldConnectionId + " player null for netId:" + msg.netId + " msg.playerControllerId:" + msg.playerControllerId); }
                return;
            }

            if (playerObject.activeSelf)
            {
                if (LogFilter.logError) { Debug.LogError("OnReconnectMessage connId=" + msg.oldConnectionId + " player already active?"); }
                return;
            }

            if (LogFilter.logDebug) { Debug.Log("OnReconnectMessage: player=" + playerObject); }


            NetworkReader extraDataReader = null;
            if (msg.msgSize != 0)
            {
                extraDataReader = new NetworkReader(msg.msgData);
            }

            if (msg.playerControllerId != -1)
            {
                if (extraDataReader == null)
                {
                    OnServerReconnectPlayer(netMsg.conn, playerObject, msg.oldConnectionId, msg.playerControllerId);
                }
                else
                {
                    OnServerReconnectPlayer(netMsg.conn, playerObject, msg.oldConnectionId, msg.playerControllerId, extraDataReader);
                }
            }
            else
            {
                OnServerReconnectObject(netMsg.conn, playerObject, msg.oldConnectionId);
            }
        }

        // call this on the server to re-setup an object for a new connection
        public bool ReconnectObjectForConnection(NetworkConnection newConnection, GameObject oldObject, int oldConnectionId)
        {
            if (!NetworkServer.active)
            {
                if (LogFilter.logError) { Debug.LogError("ReconnectObjectForConnection must have active server"); }
                return false;
            }

            if (LogFilter.logDebug) { Debug.Log("ReconnectObjectForConnection: oldConnId=" + oldConnectionId + " obj=" + oldObject + " conn:" + newConnection); }

            if (!m_PendingPlayers.ContainsKey(oldConnectionId))
            {
                if (LogFilter.logError) { Debug.LogError("ReconnectObjectForConnection oldConnId=" + oldConnectionId + " not found."); }
                return false;
            }

            oldObject.SetActive(true);
            oldObject.GetComponent<NetworkIdentity>().SetNetworkInstanceId(new NetworkInstanceId(0));

            if (!NetworkServer.SpawnWithClientAuthority(oldObject, newConnection))
            {
                if (LogFilter.logError) { Debug.LogError("ReconnectObjectForConnection oldConnId=" + oldConnectionId + " SpawnWithClientAuthority failed."); }
                return false;
            }

            return true;
        }

        // call this on the server to re-setup a reconnecting player for a new connection
        public bool ReconnectPlayerForConnection(NetworkConnection newConnection, GameObject oldPlayer, int oldConnectionId, short playerControllerId)
        {
            if (!NetworkServer.active)
            {
                if (LogFilter.logError) { Debug.LogError("ReconnectPlayerForConnection must have active server"); }
                return false;
            }

            if (LogFilter.logDebug) { Debug.Log("ReconnectPlayerForConnection: oldConnId=" + oldConnectionId + " player=" + oldPlayer + " conn:" + newConnection); }

            if (!m_PendingPlayers.ContainsKey(oldConnectionId))
            {
                if (LogFilter.logError) { Debug.LogError("ReconnectPlayerForConnection oldConnId=" + oldConnectionId + " not found."); }
                return false;
            }

            oldPlayer.SetActive(true);

            // this ensures the observers are rebuilt for the player object
            NetworkServer.Spawn(oldPlayer);

            if (!NetworkServer.AddPlayerForConnection(newConnection, oldPlayer, playerControllerId))
            {
                if (LogFilter.logError) { Debug.LogError("ReconnectPlayerForConnection oldConnId=" + oldConnectionId + " AddPlayerForConnection failed."); }
                return false;
            }

            //NOTE. cannot remove the pending player here - could be more owned objects to come in later messages.

            if (NetworkServer.localClientActive)
            {
                SendPeerInfo();
            }

            return true;
        }

        // called by NetworkManager on clients when connection to host is lost.
        // return true to stay in online scene
        public bool LostHostOnClient(NetworkConnection conn)
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkMigrationManager client OnDisconnectedFromHost"); }

            if (UnityEngine.Application.platform == RuntimePlatform.WebGLPlayer)
            {
                if (LogFilter.logError) { Debug.LogError("LostHostOnClient: Host migration not supported on WebGL"); }
                return false;
            }

            if (m_Client == null)
            {
                if (LogFilter.logError) { Debug.LogError("NetworkMigrationManager LostHostOnHost client was never initialized."); }
                return false;
            }

            if (!m_HostMigration)
            {
                if (LogFilter.logError) { Debug.LogError("NetworkMigrationManager LostHostOnHost migration not enabled."); }
                return false;
            }

            m_DisconnectedFromHost = true;
            DisablePlayerObjects();


            byte error;
            NetworkTransport.Disconnect(m_Client.hostId, m_Client.connection.connectionId, out error);

            if (m_OldServerConnectionId != -1)
            {
                // only call this if we actually connected
                SceneChangeOption sceneOption;
                OnClientDisconnectedFromHost(conn, out sceneOption);
                return sceneOption == SceneChangeOption.StayInOnlineScene;
            }

            // never entered the online scene
            return false;
        }

        // called by NetworkManager on host when host is closed
        public void LostHostOnHost()
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkMigrationManager LostHostOnHost"); }

            if (UnityEngine.Application.platform == RuntimePlatform.WebGLPlayer)
            {
                if (LogFilter.logError) { Debug.LogError("LostHostOnHost: Host migration not supported on WebGL"); }
                return;
            }

            OnServerHostShutdown();

            if (m_Peers == null)
            {
                if (LogFilter.logError) { Debug.LogError("NetworkMigrationManager LostHostOnHost no peers"); }
                return;
            }

            if (m_Peers.Length != 1)
            {
                // there was another player that could become the host
                m_HostWasShutdown = true;
            }
        }

        public bool BecomeNewHost(int port)
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkMigrationManager BecomeNewHost " + m_MatchInfo); }

            NetworkServer.RegisterHandler(MsgType.ReconnectPlayer, OnServerReconnectPlayerMessage);

            var newClient = NetworkServer.BecomeHost(m_Client, port, m_MatchInfo, oldServerConnectionId, peers);
            if (newClient != null)
            {
                if (NetworkManager.singleton != null)
                {
                    NetworkManager.singleton.RegisterServerMessages();
                    NetworkManager.singleton.UseExternalClient(newClient);
                }
                else
                {
                    Debug.LogWarning("MigrationManager BecomeNewHost - No NetworkManager.");
                }

                newClient.RegisterHandlerSafe(MsgType.NetworkInfo, OnPeerInfo);

                RemovePendingPlayer(m_OldServerConnectionId);
                Reset(ClientScene.ReconnectIdInvalid);
                SendPeerInfo();
                return true;
            }
            else
            {
                if (LogFilter.logError) { Debug.LogError("NetworkServer.BecomeHost failed"); }
                return false;
            }
        }

        // ----------------------------- Callbacks ---------------------------------------

        // called on client after the connection to host is lost. controls whether to switch scenes
        protected virtual void OnClientDisconnectedFromHost(NetworkConnection conn, out SceneChangeOption sceneChange)
        {
            sceneChange = SceneChangeOption.StayInOnlineScene;
        }

        // called on host after the host is lost. host MUST change scenes
        protected virtual void OnServerHostShutdown()
        {
        }

        // called on new host (server) when a client from the old host re-connects a player
        protected virtual void OnServerReconnectPlayer(NetworkConnection newConnection, GameObject oldPlayer, int oldConnectionId, short playerControllerId)
        {
            ReconnectPlayerForConnection(newConnection, oldPlayer, oldConnectionId, playerControllerId);
        }

        // called on new host (server) when a client from the old host re-connects a player
        protected virtual void OnServerReconnectPlayer(NetworkConnection newConnection, GameObject oldPlayer, int oldConnectionId, short playerControllerId, NetworkReader extraMessageReader)
        {
            // extraMessageReader is not used in the default version, but it is available for custom versions to use
            ReconnectPlayerForConnection(newConnection, oldPlayer, oldConnectionId, playerControllerId);
        }

        // called on new host (server) when a client from the old host re-connects an object with authority
        protected virtual void OnServerReconnectObject(NetworkConnection newConnection, GameObject oldObject, int oldConnectionId)
        {
            ReconnectObjectForConnection(newConnection, oldObject, oldConnectionId);
        }

        // called on both host and client when the set of peers is updated
        protected virtual void OnPeersUpdated(PeerListMessage peers)
        {
            if (LogFilter.logDev) { Debug.Log("NetworkMigrationManager NumPeers "  + peers.peers.Length); }
        }

        // called on both host and client when authority changes on a non-player object
        protected virtual void OnAuthorityUpdated(GameObject go, int connectionId, bool authorityState)
        {
            if (LogFilter.logDev) { Debug.Log("NetworkMigrationManager OnAuthorityUpdated for " + go + " conn:" + connectionId + " state:" + authorityState); }
        }

        // utility function called by the default UI on client after connection to host was lost, to pick a new host.
        public virtual bool FindNewHost(out NetworkSystem.PeerInfoMessage newHostInfo, out bool youAreNewHost)
        {
            if (m_Peers == null)
            {
                if (LogFilter.logError) { Debug.LogError("NetworkMigrationManager FindLowestHost no peers"); }
                newHostInfo = null;
                youAreNewHost = false;
                return false;
            }

            if (LogFilter.logDev) { Debug.Log("NetworkMigrationManager FindLowestHost"); }

            const int k_FakeConnectionId = 50000;

            newHostInfo = new PeerInfoMessage();
            newHostInfo.connectionId = k_FakeConnectionId;
            newHostInfo.address = "";
            newHostInfo.port = 0;

            int yourConnectionId = -1;
            youAreNewHost = false;

            if (m_Peers == null)
                return false;

            for (int peerId = 0; peerId < m_Peers.Length; peerId++)
            {
                var peer = m_Peers[peerId];
                if (peer.connectionId == 0)
                {
                    continue;
                }

                if (peer.isHost)
                {
                    continue;
                }

                if (peer.isYou)
                {
                    yourConnectionId = peer.connectionId;
                }

                if (peer.connectionId < newHostInfo.connectionId)
                {
                    newHostInfo = peer;
                }
            }
            if (newHostInfo.connectionId == k_FakeConnectionId)
            {
                return false;
            }
            if (newHostInfo.connectionId == yourConnectionId)
            {
                youAreNewHost = true;
            }

            if (LogFilter.logDev) { Debug.Log("FindNewHost new host is " + newHostInfo.address); }
            return true;
        }

        // ----------------------------- GUI ---------------------------------------

        void OnGUIHost()
        {
            int ypos = m_OffsetY;
            const int spacing = 25;

            GUI.Label(new Rect(m_OffsetX, ypos, 200, 40), "Host Was Shutdown ID(" + m_OldServerConnectionId + ")");
            ypos += spacing;

            if (UnityEngine.Application.platform == RuntimePlatform.WebGLPlayer)
            {
                GUI.Label(new Rect(m_OffsetX, ypos, 200, 40), "Host Migration not supported for WebGL");
                return;
            }

            if (m_WaitingReconnectToNewHost)
            {
                if (GUI.Button(new Rect(m_OffsetX, ypos, 200, 20), "Reconnect as Client"))
                {
                    Reset(ClientScene.ReconnectIdHost);

                    if (NetworkManager.singleton != null)
                    {
                        NetworkManager.singleton.networkAddress = GUI.TextField(new Rect(m_OffsetX + 100, ypos, 95, 20), NetworkManager.singleton.networkAddress);
                        NetworkManager.singleton.StartClient();
                    }
                    else
                    {
                        Debug.LogWarning("MigrationManager Old Host Reconnect - No NetworkManager.");
                    }
                }
                ypos += spacing;
            }
            else
            {
                if (GUI.Button(new Rect(m_OffsetX, ypos, 200, 20), "Pick New Host"))
                {
                    bool youAreNewHost;
                    if (FindNewHost(out m_NewHostInfo, out youAreNewHost))
                    {
                        m_NewHostAddress = m_NewHostInfo.address;
                        if (youAreNewHost)
                        {
                            // you cannot be the new host.. you were the old host..?
                            Debug.LogWarning("MigrationManager FindNewHost - new host is self?");
                        }
                        else
                        {
                            m_WaitingReconnectToNewHost = true;
                        }
                    }
                }
                ypos += spacing;
            }

            if (GUI.Button(new Rect(m_OffsetX, ypos, 200, 20), "Leave Game"))
            {
                if (NetworkManager.singleton != null)
                {
                    NetworkManager.singleton.SetupMigrationManager(null);
                    NetworkManager.singleton.StopHost();
                }
                else
                {
                    Debug.LogWarning("MigrationManager Old Host LeaveGame - No NetworkManager.");
                }
                Reset(ClientScene.ReconnectIdInvalid);
            }
            ypos += spacing;
        }

        void OnGUIClient()
        {
            int ypos = m_OffsetY;
            const int spacing = 25;

            GUI.Label(new Rect(m_OffsetX, ypos, 200, 40), "Lost Connection To Host ID(" + m_OldServerConnectionId + ")");
            ypos += spacing;

            if (UnityEngine.Application.platform == RuntimePlatform.WebGLPlayer)
            {
                GUI.Label(new Rect(m_OffsetX, ypos, 200, 40), "Host Migration not supported for WebGL");
                return;
            }

            if (m_WaitingToBecomeNewHost)
            {
                GUI.Label(new Rect(m_OffsetX, ypos, 200, 40), "You are the new host");
                ypos += spacing;

                if (GUI.Button(new Rect(m_OffsetX, ypos, 200, 20), "Start As Host"))
                {
                    if (NetworkManager.singleton != null)
                    {
                        BecomeNewHost(NetworkManager.singleton.networkPort);
                    }
                    else
                    {
                        Debug.LogWarning("MigrationManager Client BecomeNewHost - No NetworkManager.");
                    }
                }
                ypos += spacing;
            }
            else if (m_WaitingReconnectToNewHost)
            {
                GUI.Label(new Rect(m_OffsetX, ypos, 200, 40), "New host is " + m_NewHostAddress);
                ypos += spacing;

                if (GUI.Button(new Rect(m_OffsetX, ypos, 200, 20), "Reconnect To New Host"))
                {
                    Reset(m_OldServerConnectionId);

                    if (NetworkManager.singleton != null)
                    {
                        NetworkManager.singleton.networkAddress = m_NewHostAddress;
                        NetworkManager.singleton.client.ReconnectToNewHost(m_NewHostAddress, NetworkManager.singleton.networkPort);
                    }
                    else
                    {
                        Debug.LogWarning("MigrationManager Client reconnect - No NetworkManager.");
                    }
                }
                ypos += spacing;
            }
            else
            {
                if (GUI.Button(new Rect(m_OffsetX, ypos, 200, 20), "Pick New Host"))
                {
                    bool youAreNewHost;
                    if (FindNewHost(out m_NewHostInfo, out youAreNewHost))
                    {
                        m_NewHostAddress = m_NewHostInfo.address;
                        if (youAreNewHost)
                        {
                            m_WaitingToBecomeNewHost = true;
                        }
                        else
                        {
                            m_WaitingReconnectToNewHost = true;
                        }
                    }
                }
                ypos += spacing;
            }

            if (GUI.Button(new Rect(m_OffsetX, ypos, 200, 20), "Leave Game"))
            {
                if (NetworkManager.singleton != null)
                {
                    NetworkManager.singleton.SetupMigrationManager(null);
                    NetworkManager.singleton.StopHost();
                }
                else
                {
                    Debug.LogWarning("MigrationManager Client LeaveGame - No NetworkManager.");
                }
                Reset(ClientScene.ReconnectIdInvalid);
            }
            ypos += spacing;
        }

        void OnGUI()
        {
            if (!m_ShowGUI)
                return;

            if (m_HostWasShutdown)
            {
                OnGUIHost();
                return;
            }

            if (m_DisconnectedFromHost && m_OldServerConnectionId != -1)
            {
                OnGUIClient();
            }
        }
    }
}

#endif
#endif
