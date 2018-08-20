#if ENABLE_UNET
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine.Networking.NetworkSystem;

namespace UnityEngine.Networking
{
    public class NetworkClient
    {
        Type m_NetworkConnectionClass = typeof(NetworkConnection);

        static List<NetworkClient> s_Clients = new List<NetworkClient>();
        static bool s_IsActive;

        public static List<NetworkClient> allClients { get { return s_Clients; } }
        public static bool active { get { return s_IsActive; } }

        int m_HostPort;

        string m_ServerIp = "";
        int m_ServerPort;
        int m_ClientId = -1;

        Dictionary<short, NetworkMessageDelegate> m_MessageHandlers = new Dictionary<short, NetworkMessageDelegate>();
        protected NetworkConnection m_Connection;

        protected enum ConnectState
        {
            None,
            Connecting,
            Connected,
            Disconnected,
        }
        protected ConnectState connectState = ConnectState.None;

        internal void SetHandlers(NetworkConnection conn)
        {
            conn.SetHandlers(m_MessageHandlers);
        }

        public string serverIp { get { return m_ServerIp; } }
        public int serverPort { get { return m_ServerPort; } }
        public NetworkConnection connection { get { return m_Connection; } }

        public Dictionary<short, NetworkMessageDelegate> handlers { get { return m_MessageHandlers; } }
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

        public bool isConnected { get { return connectState == ConnectState.Connected; } }

        public Type networkConnectionClass { get { return m_NetworkConnectionClass; } }

        public void SetNetworkConnectionClass<T>() where T : NetworkConnection
        {
            m_NetworkConnectionClass = typeof(T);
        }

        public NetworkClient()
        {
            if (LogFilter.logDev) { Debug.Log("Client created version " + Version.Current); }
            AddClient(this);
        }

        public NetworkClient(NetworkConnection conn)
        {
            if (LogFilter.logDev) { Debug.Log("Client created version " + Version.Current); }
            AddClient(this);

            SetActive(true);
            m_Connection = conn;
            connectState = ConnectState.Connected;
            conn.SetHandlers(m_MessageHandlers);
            RegisterSystemHandlers(false);
        }

        static bool IsValidIpV6(string address)
        {
            // use C# built-in method
            IPAddress temp;
            return IPAddress.TryParse(address, out temp) && temp.AddressFamily == AddressFamily.InterNetworkV6;
        }

        public void Connect(string serverIp, int serverPort)
        {
            PrepareForConnect(false);

            if (LogFilter.logDebug) { Debug.Log("Client Connect: " + serverIp + ":" + serverPort); }

            string hostnameOrIp = serverIp;
            m_ServerPort = serverPort;
            m_ServerIp = hostnameOrIp;

            connectState = ConnectState.Connecting;
            Transport.layer.ClientConnect(serverIp, serverPort);

            // setup all the handlers
            m_Connection = (NetworkConnection)Activator.CreateInstance(m_NetworkConnectionClass);
            m_Connection.SetHandlers(m_MessageHandlers);
            m_Connection.Initialize(m_ServerIp, m_ClientId, 0);
        }

        void PrepareForConnect(bool usePlatformSpecificProtocols)
        {
            SetActive(true);
            RegisterSystemHandlers(false);
            m_ClientId = 0;
        }

        public virtual void Disconnect()
        {
            connectState = ConnectState.Disconnected;
            ClientScene.HandleClientDisconnect(m_Connection);
            if (m_Connection != null)
            {
                m_Connection.Disconnect();
                m_Connection.Dispose();
                m_Connection = null;
                m_ClientId = -1;
            }
        }

        public bool Send(short msgType, MessageBase msg)
        {
            if (m_Connection != null)
            {
                if (connectState != ConnectState.Connected)
                {
                    if (LogFilter.logError) { Debug.LogError("NetworkClient Send when not connected to a server"); }
                    return false;
                }
                return m_Connection.Send(msgType, msg);
            }
            if (LogFilter.logError) { Debug.LogError("NetworkClient Send with no connection"); }
            return false;
        }

        public void Shutdown()
        {
            if (LogFilter.logDebug) Debug.Log("Shutting down client " + m_ClientId);
            m_ClientId = -1;
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

            // don't do anything if we aren't fully connected
            // -> we don't check Client.Connected because then we wouldn't
            //    process the last disconnect message.
            if (connectState != ConnectState.Connecting && connectState != ConnectState.Connected)
            {
                return;
            }

            // any new message?
            // -> calling it once per frame is okay, but really why not just
            //    process all messages and make it empty..
            TransportEvent transportEvent;
            byte[] data;
            while (Transport.layer.ClientGetNextMessage(out transportEvent, out data))
            {
                switch (transportEvent)
                {
                    case TransportEvent.Connected:
                        //Debug.Log("NetworkClient loop: Connected");
                        m_Connection.InvokeHandlerNoData((short)MsgType.Connect);
                        connectState = ConnectState.Connected;
                        break;
                    case TransportEvent.Data:
                        //Debug.Log("NetworkClient loop: Data: " + BitConverter.ToString(msg.data));
                        m_Connection.TransportReceive(data);
                        break;
                    case TransportEvent.Disconnected:
                        //Debug.Log("NetworkClient loop: Disconnected");
                        connectState = ConnectState.Disconnected;

                        //GenerateDisconnectError(error); TODO which one?
                        ClientScene.HandleClientDisconnect(m_Connection);
                        if (m_Connection != null)
                        {
                            m_Connection.InvokeHandlerNoData((short)MsgType.Disconnect);
                        }
                        break;
                }
            }
        }

        void GenerateConnectError(byte error)
        {
            if (LogFilter.logError) { Debug.LogError("UNet Client Error Connect Error: " + error); }
            GenerateError(error);
        }

        void GenerateDataError(byte error)
        {
            NetworkError dataError = (NetworkError)error;
            if (LogFilter.logError) { Debug.LogError("UNet Client Data Error: " + dataError); }
            GenerateError(error);
        }

        void GenerateDisconnectError(byte error)
        {
            NetworkError disconnectError = (NetworkError)error;
            if (LogFilter.logError) { Debug.LogError("UNet Client Disconnect Error: " + disconnectError); }
            GenerateError(error);
        }

        void GenerateError(byte error)
        {
            NetworkMessageDelegate msgDelegate;
            if (m_MessageHandlers.TryGetValue((short)MsgType.Error, out msgDelegate))
            {
                ErrorMessage msg = new ErrorMessage();
                msg.errorCode = error;

                // write the message to a local buffer
                NetworkWriter writer = new NetworkWriter();
                msg.Serialize(writer);

                NetworkMessage netMsg = new NetworkMessage();
                netMsg.msgType = (short)MsgType.Error;
                netMsg.reader = new NetworkReader(writer.ToArray());
                netMsg.conn = m_Connection;
                msgDelegate(netMsg);
            }
        }

        public float GetRTT()
        {
            if (m_ClientId == -1)
                return 0;

            return Transport.layer.ClientGetRTT();
        }

        internal void RegisterSystemHandlers(bool localClient)
        {
            ClientScene.RegisterSystemHandlers(this, localClient);
        }

        public void RegisterHandler(short msgType, NetworkMessageDelegate handler)
        {
            if (m_MessageHandlers.ContainsKey(msgType))
            {
                if (LogFilter.logDebug) { Debug.Log("NetworkClient.RegisterHandler replacing " + msgType); }
            }
            m_MessageHandlers[msgType] = handler;
        }

        public void UnregisterHandler(short msgType)
        {
            m_MessageHandlers.Remove(msgType);
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
            // remove null clients first
            s_Clients.RemoveAll(cl => cl == null);

            // now updating valid clients
            for (int i = 0; i < s_Clients.Count; ++i)
            {
                s_Clients[i].Update();
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
        }

        internal static void SetActive(bool state)
        {
            s_IsActive = state;
        }
    };
}
#endif //ENABLE_UNET
