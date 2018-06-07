#if ENABLE_UNET
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine.Networking.Match;
using UnityEngine.Networking.NetworkSystem;

namespace UnityEngine.Networking
{
    public class NetworkClient
    {
        Type m_NetworkConnectionClass = typeof(NetworkConnection);

        const int k_MaxEventsPerFrame = 500;

        static List<NetworkClient> s_Clients = new List<NetworkClient>();
        static bool s_IsActive;

        public static List<NetworkClient> allClients { get { return s_Clients; } }
        public static bool active { get { return s_IsActive; } }

        HostTopology m_HostTopology;
        int m_HostPort;

        bool m_UseSimulator;
        int m_SimulatedLatency;
        float m_PacketLoss;

        string m_ServerIp = "";
        int m_ServerPort;
        int m_ClientId = -1;
        int m_ClientConnectionId = -1;
        //int m_RelaySlotId = -1;

        int m_StatResetTime;

        EndPoint m_RemoteEndPoint;

        // static message objects to avoid runtime-allocations
        static CRCMessage s_CRCMessage = new CRCMessage();

        NetworkMessageHandlers m_MessageHandlers = new NetworkMessageHandlers();
        protected NetworkConnection m_Connection;

        byte[] m_MsgBuffer;
        NetworkReader m_MsgReader;

        protected enum ConnectState
        {
            None,
            Resolving,
            Resolved,
            Connecting,
            Connected,
            Disconnected,
            Failed
        }
        protected ConnectState m_AsyncConnect = ConnectState.None;
        string m_RequestedServerHost = "";

        internal void SetHandlers(NetworkConnection conn)
        {
            conn.SetHandlers(m_MessageHandlers);
        }

        public string serverIp { get { return m_ServerIp; } }
        public int serverPort { get { return m_ServerPort; } }
        public NetworkConnection connection { get { return m_Connection; } }

#if ENABLE_UNET_HOST_MIGRATION
        [Obsolete("Moved to NetworkMigrationManager.")]
#else
        [Obsolete("Removed")]
#endif
        public PeerInfoMessage[] peers { get { return null; } }

        internal int hostId { get { return m_ClientId; } }
        public Dictionary<short, NetworkMessageDelegate> handlers { get { return m_MessageHandlers.GetHandlers(); } }
        public int numChannels { get { return m_HostTopology.DefaultConfig.ChannelCount; } }
        public HostTopology hostTopology { get { return m_HostTopology; }}
        public int hostPort
        {
            get { return m_HostPort; }
            set
            {
                if (value < 0)
                    throw new ArgumentException("Port must not be a negative number.");

                if (value > 65535)
                    throw new ArgumentException("Port must not be greater than 65535.");

                m_HostPort = value;
            }
        }

        public bool isConnected { get { return m_AsyncConnect == ConnectState.Connected; }}

        public Type networkConnectionClass
        {
            get { return m_NetworkConnectionClass; }
        }

        public void SetNetworkConnectionClass<T>() where T : NetworkConnection
        {
            m_NetworkConnectionClass = typeof(T);
        }

        public NetworkClient()
        {
            if (LogFilter.logDev) { Debug.Log("Client created version " + Version.Current); }
            m_MsgBuffer = new byte[NetworkMessage.MaxMessageSize];
            m_MsgReader = new NetworkReader(m_MsgBuffer);
            AddClient(this);
        }

        public NetworkClient(NetworkConnection conn)
        {
            if (LogFilter.logDev) { Debug.Log("Client created version " + Version.Current); }
            m_MsgBuffer = new byte[NetworkMessage.MaxMessageSize];
            m_MsgReader = new NetworkReader(m_MsgBuffer);
            AddClient(this);

            SetActive(true);
            m_Connection = conn;
            m_AsyncConnect = ConnectState.Connected;
            conn.SetHandlers(m_MessageHandlers);
            RegisterSystemHandlers(false);
        }

        public bool Configure(ConnectionConfig config, int maxConnections)
        {
            HostTopology top = new HostTopology(config, maxConnections);
            return Configure(top);
        }

        public bool Configure(HostTopology topology)
        {
            //NOTE: this maxConnections is across all clients that use this tuner, so it is
            //      effectively the number of _clients_.
            m_HostTopology = topology;
            return true;
        }

        public void Connect(MatchInfo matchInfo)
        {
            PrepareForConnect();
            ConnectWithRelay(matchInfo);
        }

#if ENABLE_UNET_HOST_MIGRATION
        public bool ReconnectToNewHost(string serverIp, int serverPort)
        {
            if (!NetworkClient.active)
            {
                if (LogFilter.logError) { Debug.LogError("Reconnect - NetworkClient must be active"); }
                return false;
            }

            if (m_Connection == null)
            {
                if (LogFilter.logError) { Debug.LogError("Reconnect - no old connection exists"); }
                return false;
            }

            if (LogFilter.logInfo) { Debug.Log("NetworkClient Reconnect " + serverIp + ":" + serverPort); }

            ClientScene.HandleClientDisconnect(m_Connection);
            ClientScene.ClearLocalPlayers();

            m_Connection.Disconnect();
            m_Connection = null;
            m_ClientId = NetworkTransport.AddHost(m_HostTopology, m_HostPort);

            string hostnameOrIp = serverIp;
            m_ServerPort = serverPort;

            //TODO: relay reconnect
            /*
            if (Match.NetworkMatch.matchSingleton != null)
            {
                hostnameOrIp = Match.NetworkMatch.matchSingleton.address;
                m_ServerPort = Match.NetworkMatch.matchSingleton.port;
            }*/

            if (UnityEngine.Application.platform == RuntimePlatform.WebGLPlayer)
            {
                m_ServerIp = hostnameOrIp;
                m_AsyncConnect = ConnectState.Resolved;
            }
            else if (serverIp.Equals("127.0.0.1") || serverIp.Equals("localhost"))
            {
                m_ServerIp = "127.0.0.1";
                m_AsyncConnect = ConnectState.Resolved;
            }
            else
            {
                if (LogFilter.logDebug) { Debug.Log("Async DNS START:" + hostnameOrIp); }
                m_AsyncConnect = ConnectState.Resolving;
                Dns.BeginGetHostAddresses(hostnameOrIp, new AsyncCallback(GetHostAddressesCallback), this);
            }
            return true;
        }

        public bool ReconnectToNewHost(EndPoint secureTunnelEndPoint)
        {
            if (!NetworkClient.active)
            {
                if (LogFilter.logError) { Debug.LogError("Reconnect - NetworkClient must be active"); }
                return false;
            }

            if (m_Connection == null)
            {
                if (LogFilter.logError) { Debug.LogError("Reconnect - no old connection exists"); }
                return false;
            }

            if (LogFilter.logInfo) { Debug.Log("NetworkClient Reconnect to remoteSockAddr"); }

            ClientScene.HandleClientDisconnect(m_Connection);
            ClientScene.ClearLocalPlayers();

            m_Connection.Disconnect();
            m_Connection = null;
            m_ClientId = NetworkTransport.AddHost(m_HostTopology, m_HostPort);

            if (secureTunnelEndPoint == null)
            {
                if (LogFilter.logError) { Debug.LogError("Reconnect failed: null endpoint passed in"); }
                m_AsyncConnect = ConnectState.Failed;
                return false;
            }

            // Make sure it's either IPv4 or IPv6
            if (secureTunnelEndPoint.AddressFamily != AddressFamily.InterNetwork && secureTunnelEndPoint.AddressFamily != AddressFamily.InterNetworkV6)
            {
                if (LogFilter.logError) { Debug.LogError("Reconnect failed: Endpoint AddressFamily must be either InterNetwork or InterNetworkV6"); }
                m_AsyncConnect = ConnectState.Failed;
                return false;
            }

            // Make sure it's an Endpoint we know what to do with
            string endPointType = secureTunnelEndPoint.GetType().FullName;
            if (endPointType == "System.Net.IPEndPoint")
            {
                IPEndPoint tmp = (IPEndPoint)secureTunnelEndPoint;
                Connect(tmp.Address.ToString(), tmp.Port);
                return m_AsyncConnect != ConnectState.Failed;
            }
            if ((endPointType != "UnityEngine.XboxOne.XboxOneEndPoint") && (endPointType != "UnityEngine.PS4.SceEndPoint"))
            {
                if (LogFilter.logError) { Debug.LogError("Reconnect failed: invalid Endpoint (not IPEndPoint or XboxOneEndPoint or SceEndPoint)"); }
                m_AsyncConnect = ConnectState.Failed;
                return false;
            }

            byte error = 0;
            // regular non-relay connect
            m_RemoteEndPoint = secureTunnelEndPoint;
            m_AsyncConnect = ConnectState.Connecting;

            try
            {
                m_ClientConnectionId = NetworkTransport.ConnectEndPoint(m_ClientId, m_RemoteEndPoint, 0, out error);
            }
            catch (Exception ex)
            {
                if (LogFilter.logError) { Debug.LogError("Reconnect failed: Exception when trying to connect to EndPoint: " + ex); }
                m_AsyncConnect = ConnectState.Failed;
                return false;
            }
            if (m_ClientConnectionId == 0)
            {
                if (LogFilter.logError) { Debug.LogError("Reconnect failed: Unable to connect to EndPoint (" + error + ")"); }
                m_AsyncConnect = ConnectState.Failed;
                return false;
            }

            m_Connection = (NetworkConnection)Activator.CreateInstance(m_NetworkConnectionClass);
            m_Connection.SetHandlers(m_MessageHandlers);
            m_Connection.Initialize(m_ServerIp, m_ClientId, m_ClientConnectionId, m_HostTopology);
            return true;
        }

#endif

        public void ConnectWithSimulator(string serverIp, int serverPort, int latency, float packetLoss)
        {
            m_UseSimulator = true;
            m_SimulatedLatency = latency;
            m_PacketLoss = packetLoss;
            Connect(serverIp, serverPort);
        }

        static bool IsValidIpV6(string address)
        {
            for (int i = 0; i < address.Length; i++)
            {
                var c = address[i];
                if (
                    (c == ':') ||
                    (c >= '0' && c <= '9') ||
                    (c >= 'a' && c <= 'f') ||
                    (c >= 'A' && c <= 'F')
                    )
                {
                    continue;
                }
                return false;
            }
            return true;
        }

        public void Connect(string serverIp, int serverPort)
        {
            PrepareForConnect();

            if (LogFilter.logDebug) { Debug.Log("Client Connect: " + serverIp + ":" + serverPort); }

            string hostnameOrIp = serverIp;
            m_ServerPort = serverPort;

            if (UnityEngine.Application.platform == RuntimePlatform.WebGLPlayer)
            {
                m_ServerIp = hostnameOrIp;
                m_AsyncConnect = ConnectState.Resolved;
            }
            else if (serverIp.Equals("127.0.0.1") || serverIp.Equals("localhost"))
            {
                m_ServerIp = "127.0.0.1";
                m_AsyncConnect = ConnectState.Resolved;
            }
            else if (serverIp.IndexOf(":") != -1 && IsValidIpV6(serverIp))
            {
                m_ServerIp = serverIp;
                m_AsyncConnect = ConnectState.Resolved;
            }
            else
            {
                if (LogFilter.logDebug) { Debug.Log("Async DNS START:" + hostnameOrIp); }
                m_RequestedServerHost = hostnameOrIp;
                m_AsyncConnect = ConnectState.Resolving;
                Dns.BeginGetHostAddresses(hostnameOrIp, GetHostAddressesCallback, this);
            }
        }

        public void Connect(EndPoint secureTunnelEndPoint)
        {
            bool usePlatformSpecificProtocols = NetworkTransport.DoesEndPointUsePlatformProtocols(secureTunnelEndPoint);
            PrepareForConnect(usePlatformSpecificProtocols);

            if (LogFilter.logDebug) { Debug.Log("Client Connect to remoteSockAddr"); }

            if (secureTunnelEndPoint == null)
            {
                if (LogFilter.logError) { Debug.LogError("Connect failed: null endpoint passed in"); }
                m_AsyncConnect = ConnectState.Failed;
                return;
            }

            // Make sure it's either IPv4 or IPv6
            if (secureTunnelEndPoint.AddressFamily != AddressFamily.InterNetwork && secureTunnelEndPoint.AddressFamily != AddressFamily.InterNetworkV6)
            {
                if (LogFilter.logError) { Debug.LogError("Connect failed: Endpoint AddressFamily must be either InterNetwork or InterNetworkV6"); }
                m_AsyncConnect = ConnectState.Failed;
                return;
            }

            // Make sure it's an Endpoint we know what to do with
            string endPointType = secureTunnelEndPoint.GetType().FullName;
            if (endPointType == "System.Net.IPEndPoint")
            {
                IPEndPoint tmp = (IPEndPoint)secureTunnelEndPoint;
                Connect(tmp.Address.ToString(), tmp.Port);
                return;
            }
            if ((endPointType != "UnityEngine.XboxOne.XboxOneEndPoint") && (endPointType != "UnityEngine.PS4.SceEndPoint") && (endPointType != "UnityEngine.PSVita.SceEndPoint"))
            {
                if (LogFilter.logError) { Debug.LogError("Connect failed: invalid Endpoint (not IPEndPoint or XboxOneEndPoint or SceEndPoint)"); }
                m_AsyncConnect = ConnectState.Failed;
                return;
            }

            byte error = 0;
            // regular non-relay connect
            m_RemoteEndPoint = secureTunnelEndPoint;
            m_AsyncConnect = ConnectState.Connecting;

            try
            {
                m_ClientConnectionId = NetworkTransport.ConnectEndPoint(m_ClientId, m_RemoteEndPoint, 0, out error);
            }
            catch (Exception ex)
            {
                if (LogFilter.logError) { Debug.LogError("Connect failed: Exception when trying to connect to EndPoint: " + ex); }
                m_AsyncConnect = ConnectState.Failed;
                return;
            }
            if (m_ClientConnectionId == 0)
            {
                if (LogFilter.logError) { Debug.LogError("Connect failed: Unable to connect to EndPoint (" + error + ")"); }
                m_AsyncConnect = ConnectState.Failed;
                return;
            }

            m_Connection = (NetworkConnection)Activator.CreateInstance(m_NetworkConnectionClass);
            m_Connection.SetHandlers(m_MessageHandlers);
            m_Connection.Initialize(m_ServerIp, m_ClientId, m_ClientConnectionId, m_HostTopology);
        }

        void PrepareForConnect()
        {
            PrepareForConnect(false);
        }

        void PrepareForConnect(bool usePlatformSpecificProtocols)
        {
            SetActive(true);
            RegisterSystemHandlers(false);

            if (m_HostTopology == null)
            {
                var config = new ConnectionConfig();
                config.AddChannel(QosType.ReliableSequenced);
                config.AddChannel(QosType.Unreliable);
                config.UsePlatformSpecificProtocols = usePlatformSpecificProtocols;
                m_HostTopology = new HostTopology(config, 8);
            }

            if (m_UseSimulator)
            {
                int minTimeout = (m_SimulatedLatency / 3) - 1;
                if (minTimeout < 1)
                {
                    minTimeout = 1;
                }
                int maxTimeout = m_SimulatedLatency * 3;

                if (LogFilter.logDebug) { Debug.Log("AddHost Using Simulator " + minTimeout + "/" + maxTimeout); }
                m_ClientId = NetworkTransport.AddHostWithSimulator(m_HostTopology, minTimeout, maxTimeout, m_HostPort);
            }
            else
            {
                m_ClientId = NetworkTransport.AddHost(m_HostTopology, m_HostPort);
            }
        }

        // this called in another thread! Cannot call Update() here.
        internal static void GetHostAddressesCallback(IAsyncResult ar)
        {
            try
            {
                IPAddress[] ip = Dns.EndGetHostAddresses(ar);
                NetworkClient client = (NetworkClient)ar.AsyncState;

                if (ip.Length == 0)
                {
                    if (LogFilter.logError) { Debug.LogError("DNS lookup failed for:" + client.m_RequestedServerHost); }
                    client.m_AsyncConnect = ConnectState.Failed;
                    return;
                }

                client.m_ServerIp = ip[0].ToString();
                client.m_AsyncConnect = ConnectState.Resolved;
                if (LogFilter.logDebug) { Debug.Log("Async DNS Result:" + client.m_ServerIp + " for " + client.m_RequestedServerHost + ": " + client.m_ServerIp); }
            }
            catch (SocketException e)
            {
                NetworkClient client = (NetworkClient)ar.AsyncState;
                if (LogFilter.logError) { Debug.LogError("DNS resolution failed: " + e.GetErrorCode()); }
                if (LogFilter.logDebug) { Debug.Log("Exception:" + e); }
                client.m_AsyncConnect = ConnectState.Failed;
            }
        }

        internal void ContinueConnect()
        {
            byte error;
            // regular non-relay connect
            if (m_UseSimulator)
            {
                int simLatency = m_SimulatedLatency / 3;
                if (simLatency < 1)
                {
                    simLatency = 1;
                }

                if (LogFilter.logDebug) { Debug.Log("Connect Using Simulator " + (m_SimulatedLatency / 3) + "/" + m_SimulatedLatency); }
                var simConfig = new ConnectionSimulatorConfig(
                        simLatency,
                        m_SimulatedLatency,
                        simLatency,
                        m_SimulatedLatency,
                        m_PacketLoss);

                m_ClientConnectionId = NetworkTransport.ConnectWithSimulator(m_ClientId, m_ServerIp, m_ServerPort, 0, out error, simConfig);
            }
            else
            {
                m_ClientConnectionId = NetworkTransport.Connect(m_ClientId, m_ServerIp, m_ServerPort, 0, out error);
            }

            m_Connection = (NetworkConnection)Activator.CreateInstance(m_NetworkConnectionClass);
            m_Connection.SetHandlers(m_MessageHandlers);
            m_Connection.Initialize(m_ServerIp, m_ClientId, m_ClientConnectionId, m_HostTopology);
        }

        void ConnectWithRelay(MatchInfo info)
        {
            m_AsyncConnect = ConnectState.Connecting;

            Update();

            byte error;
            m_ClientConnectionId = NetworkTransport.ConnectToNetworkPeer(
                    m_ClientId,
                    info.address,
                    info.port,
                    0,
                    0,
                    info.networkId,
                    Utility.GetSourceID(),
                    info.nodeId,
                    out error);

            m_Connection = (NetworkConnection)Activator.CreateInstance(m_NetworkConnectionClass);
            m_Connection.SetHandlers(m_MessageHandlers);
            m_Connection.Initialize(info.address, m_ClientId, m_ClientConnectionId, m_HostTopology);

            if (error != 0) { Debug.LogError("ConnectToNetworkPeer Error: " + error); }
        }

        public virtual void Disconnect()
        {
            m_AsyncConnect = ConnectState.Disconnected;
            ClientScene.HandleClientDisconnect(m_Connection);
            if (m_Connection != null)
            {
                m_Connection.Disconnect();
                m_Connection.Dispose();
                m_Connection = null;
                if (m_ClientId != -1)
                {
                    NetworkTransport.RemoveHost(m_ClientId);
                    m_ClientId = -1;
                }
            }
        }

        public bool Send(short msgType, MessageBase msg)
        {
            if (m_Connection != null)
            {
                if (m_AsyncConnect != ConnectState.Connected)
                {
                    if (LogFilter.logError) { Debug.LogError("NetworkClient Send when not connected to a server"); }
                    return false;
                }
#if UNITY_EDITOR
                UnityEditor.NetworkDetailStats.IncrementStat(
                    UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                    MsgType.UserMessage, msgType.ToString() + ":" + msg.GetType().Name, 1);
#endif
                return m_Connection.Send(msgType, msg);
            }
            if (LogFilter.logError) { Debug.LogError("NetworkClient Send with no connection"); }
            return false;
        }

        public bool SendWriter(NetworkWriter writer, int channelId)
        {
            if (m_Connection != null)
            {
                if (m_AsyncConnect != ConnectState.Connected)
                {
                    if (LogFilter.logError) { Debug.LogError("NetworkClient SendWriter when not connected to a server"); }
                    return false;
                }
                return m_Connection.SendWriter(writer, channelId);
            }
            if (LogFilter.logError) { Debug.LogError("NetworkClient SendWriter with no connection"); }
            return false;
        }

        public bool SendBytes(byte[] data, int numBytes, int channelId)
        {
            if (m_Connection != null)
            {
                if (m_AsyncConnect != ConnectState.Connected)
                {
                    if (LogFilter.logError) { Debug.LogError("NetworkClient SendBytes when not connected to a server"); }
                    return false;
                }
                return m_Connection.SendBytes(data, numBytes, channelId);
            }
            if (LogFilter.logError) { Debug.LogError("NetworkClient SendBytes with no connection"); }
            return false;
        }

        public bool SendUnreliable(short msgType, MessageBase msg)
        {
            if (m_Connection != null)
            {
                if (m_AsyncConnect != ConnectState.Connected)
                {
                    if (LogFilter.logError) { Debug.LogError("NetworkClient SendUnreliable when not connected to a server"); }
                    return false;
                }
#if UNITY_EDITOR
                UnityEditor.NetworkDetailStats.IncrementStat(
                    UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                    MsgType.UserMessage, msgType.ToString() + ":" + msg.GetType().Name, 1);
#endif
                return m_Connection.SendUnreliable(msgType, msg);
            }
            if (LogFilter.logError) { Debug.LogError("NetworkClient SendUnreliable with no connection"); }
            return false;
        }

        public bool SendByChannel(short msgType, MessageBase msg, int channelId)
        {
#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                MsgType.UserMessage, msgType.ToString() + ":" + msg.GetType().Name, 1);
#endif
            if (m_Connection != null)
            {
                if (m_AsyncConnect != ConnectState.Connected)
                {
                    if (LogFilter.logError) { Debug.LogError("NetworkClient SendByChannel when not connected to a server"); }
                    return false;
                }
                return m_Connection.SendByChannel(msgType, msg, channelId);
            }
            if (LogFilter.logError) { Debug.LogError("NetworkClient SendByChannel with no connection"); }
            return false;
        }

        public void SetMaxDelay(float seconds)
        {
            if (m_Connection == null)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("SetMaxDelay failed, not connected."); }
                return;
            }
            m_Connection.SetMaxDelay(seconds);
        }

        public void Shutdown()
        {
            if (LogFilter.logDebug) Debug.Log("Shutting down client " + m_ClientId);
            if (m_ClientId != -1)
            {
                NetworkTransport.RemoveHost(m_ClientId);
                m_ClientId = -1;
            }
            RemoveClient(this);
            if (s_Clients.Count == 0)
            {
                SetActive(false);
            }
        }

        internal virtual void Update()
        {
            if (m_ClientId == -1)
            {
                return;
            }

            switch (m_AsyncConnect)
            {
                case ConnectState.None:
                case ConnectState.Resolving:
                case ConnectState.Disconnected:
                    return;

                case ConnectState.Failed:
                    GenerateConnectError((int)NetworkError.DNSFailure);
                    m_AsyncConnect = ConnectState.Disconnected;
                    return;

                case ConnectState.Resolved:
                    m_AsyncConnect = ConnectState.Connecting;
                    ContinueConnect();
                    return;

                case ConnectState.Connecting:
                case ConnectState.Connected:
                {
                    break;
                }
            }

            if (m_Connection != null)
            {
                if ((int)Time.time != m_StatResetTime)
                {
                    m_Connection.ResetStats();
                    m_StatResetTime = (int)Time.time;
                }
            }

            int numEvents = 0;
            NetworkEventType networkEvent;
            do
            {
                int connectionId;
                int channelId;
                int receivedSize;
                byte error;

                networkEvent = NetworkTransport.ReceiveFromHost(m_ClientId, out connectionId, out channelId, m_MsgBuffer, (ushort)m_MsgBuffer.Length, out receivedSize, out error);
                if (m_Connection != null) m_Connection.lastError = (NetworkError)error;

                if (networkEvent != NetworkEventType.Nothing)
                {
                    if (LogFilter.logDev) { Debug.Log("Client event: host=" + m_ClientId + " event=" + networkEvent + " error=" + error); }
                }

                switch (networkEvent)
                {
                    case NetworkEventType.ConnectEvent:

                        if (LogFilter.logDebug) { Debug.Log("Client connected"); }

                        if (error != 0)
                        {
                            GenerateConnectError(error);
                            return;
                        }

                        m_AsyncConnect = ConnectState.Connected;
                        m_Connection.InvokeHandlerNoData(MsgType.Connect);
                        break;

                    case NetworkEventType.DataEvent:
                        if (error != 0)
                        {
                            GenerateDataError(error);
                            return;
                        }

#if UNITY_EDITOR
                        UnityEditor.NetworkDetailStats.IncrementStat(
                        UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                        MsgType.LLAPIMsg, "msg", 1);
#endif

                        m_MsgReader.SeekZero();
                        m_Connection.TransportReceive(m_MsgBuffer, receivedSize, channelId);
                        break;

                    case NetworkEventType.DisconnectEvent:
                        if (LogFilter.logDebug) { Debug.Log("Client disconnected"); }

                        m_AsyncConnect = ConnectState.Disconnected;

                        if (error != 0)
                        {
                            if ((NetworkError)error != NetworkError.Timeout)
                            {
                                GenerateDisconnectError(error);
                            }
                        }
                        ClientScene.HandleClientDisconnect(m_Connection);
                        if (m_Connection != null)
                        {
                            m_Connection.InvokeHandlerNoData(MsgType.Disconnect);
                        }
                        break;

                    case NetworkEventType.Nothing:
                        break;

                    default:
                        if (LogFilter.logError) { Debug.LogError("Unknown network message type received: " + networkEvent); }
                        break;
                }

                if (++numEvents >= k_MaxEventsPerFrame)
                {
                    if (LogFilter.logDebug) { Debug.Log("MaxEventsPerFrame hit (" + k_MaxEventsPerFrame + ")"); }
                    break;
                }
                if (m_ClientId == -1)
                {
                    break;
                }
            }
            while (networkEvent != NetworkEventType.Nothing);

            if (m_Connection != null &&  m_AsyncConnect == ConnectState.Connected)
                m_Connection.FlushChannels();
        }

        void GenerateConnectError(int error)
        {
            if (LogFilter.logError) { Debug.LogError("UNet Client Error Connect Error: " + error); }
            GenerateError(error);
        }

        void GenerateDataError(int error)
        {
            NetworkError dataError = (NetworkError)error;
            if (LogFilter.logError) { Debug.LogError("UNet Client Data Error: " + dataError); }
            GenerateError(error);
        }

        void GenerateDisconnectError(int error)
        {
            NetworkError disconnectError = (NetworkError)error;
            if (LogFilter.logError) { Debug.LogError("UNet Client Disconnect Error: " + disconnectError); }
            GenerateError(error);
        }

        void GenerateError(int error)
        {
            NetworkMessageDelegate msgDelegate = m_MessageHandlers.GetHandler(MsgType.Error);
            if (msgDelegate == null)
            {
                msgDelegate = m_MessageHandlers.GetHandler(MsgType.Error);
            }
            if (msgDelegate != null)
            {
                ErrorMessage msg = new ErrorMessage();
                msg.errorCode = error;

                // write the message to a local buffer
                byte[] errorBuffer = new byte[200];
                NetworkWriter writer = new NetworkWriter(errorBuffer);
                msg.Serialize(writer);

                // pass a reader (attached to local buffer) to handler
                NetworkReader reader = new NetworkReader(errorBuffer);

                NetworkMessage netMsg = new NetworkMessage();
                netMsg.msgType = MsgType.Error;
                netMsg.reader = reader;
                netMsg.conn = m_Connection;
                netMsg.channelId = 0;
                msgDelegate(netMsg);
            }
        }

        public void GetStatsOut(out int numMsgs, out int numBufferedMsgs, out int numBytes, out int lastBufferedPerSecond)
        {
            numMsgs = 0;
            numBufferedMsgs = 0;
            numBytes = 0;
            lastBufferedPerSecond = 0;

            if (m_Connection != null)
            {
                m_Connection.GetStatsOut(out numMsgs, out numBufferedMsgs, out numBytes, out lastBufferedPerSecond);
            }
        }

        public void GetStatsIn(out int numMsgs, out int numBytes)
        {
            numMsgs = 0;
            numBytes = 0;

            if (m_Connection != null)
            {
                m_Connection.GetStatsIn(out numMsgs, out numBytes);
            }
        }

        public Dictionary<short, NetworkConnection.PacketStat> GetConnectionStats()
        {
            if (m_Connection == null)
                return null;

            return m_Connection.packetStats;
        }

        public void ResetConnectionStats()
        {
            if (m_Connection == null)
                return;

            m_Connection.ResetStats();
        }

        public int GetRTT()
        {
            if (m_ClientId == -1)
                return 0;

            byte err;
            return NetworkTransport.GetCurrentRTT(m_ClientId, m_ClientConnectionId, out err);
        }

        internal void RegisterSystemHandlers(bool localClient)
        {
            ClientScene.RegisterSystemHandlers(this, localClient);
            RegisterHandlerSafe(MsgType.CRC, OnCRC);
            RegisterHandlerSafe(MsgType.Fragment, NetworkConnection.OnFragment);
        }

        void OnCRC(NetworkMessage netMsg)
        {
            netMsg.ReadMessage(s_CRCMessage);
            NetworkCRC.Validate(s_CRCMessage.scripts, numChannels);
        }

        public void RegisterHandler(short msgType, NetworkMessageDelegate handler)
        {
            m_MessageHandlers.RegisterHandler(msgType, handler);
        }

        public void RegisterHandlerSafe(short msgType, NetworkMessageDelegate handler)
        {
            m_MessageHandlers.RegisterHandlerSafe(msgType, handler);
        }

        public void UnregisterHandler(short msgType)
        {
            m_MessageHandlers.UnregisterHandler(msgType);
        }

        static public Dictionary<short, NetworkConnection.PacketStat> GetTotalConnectionStats()
        {
            Dictionary<short, NetworkConnection.PacketStat> stats = new Dictionary<short, NetworkConnection.PacketStat>();
            for (int i = 0; i < s_Clients.Count; i++)
            {
                var client = s_Clients[i];
                var clientStats = client.GetConnectionStats();
                foreach (short k in clientStats.Keys)
                {
                    if (stats.ContainsKey(k))
                    {
                        NetworkConnection.PacketStat s = stats[k];
                        s.count += clientStats[k].count;
                        s.bytes += clientStats[k].bytes;
                        stats[k] = s;
                    }
                    else
                    {
                        stats[k] = new NetworkConnection.PacketStat(clientStats[k]);
                    }
                }
            }
            return stats;
        }

        internal static void AddClient(NetworkClient client)
        {
            s_Clients.Add(client);
        }

        internal static bool RemoveClient(NetworkClient client)
        {
            return s_Clients.Remove(client);
        }

        static internal void UpdateClients()
        {
            for (int i = 0; i < s_Clients.Count; ++i)
            {
                if (s_Clients[i] != null)
                    s_Clients[i].Update();
                else
                    s_Clients.RemoveAt(i);
            }
        }

        static public void ShutdownAll()
        {
            while (s_Clients.Count != 0)
            {
                s_Clients[0].Shutdown();
            }
            s_Clients = new List<NetworkClient>();
            s_IsActive = false;
            ClientScene.Shutdown();
#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.ResetAll();
#endif
        }

        internal static void SetActive(bool state)
        {
            // what is this check?
            //if (state == false && s_Clients.Count != 0)
            //  return;

            if (!s_IsActive && state)
            {
                NetworkTransport.Init();
            }
            s_IsActive = state;
        }
    };
}
#endif //ENABLE_UNET
