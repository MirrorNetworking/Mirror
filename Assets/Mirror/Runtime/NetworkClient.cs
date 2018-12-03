using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Mirror
{
    public class NetworkClient
    {
        Type m_NetworkConnectionClass = typeof(NetworkConnection);

        static List<NetworkClient> s_Clients = new List<NetworkClient>();
        static bool s_IsActive;

        public static List<NetworkClient> allClients { get { return s_Clients; } }
        public static bool active { get { return s_IsActive; } }

        public static bool pauseMessageHandling;

        int m_HostPort;

        string m_ServerIp = "";
        int m_ServerPort;
        int m_ClientId = -1;

        readonly Dictionary<short, NetworkMessageDelegate> m_MessageHandlers = new Dictionary<short, NetworkMessageDelegate>();
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
            if (LogFilter.Debug) { Debug.Log("Client created version " + Version.Current); }
            AddClient(this);
        }

        public NetworkClient(NetworkConnection conn)
        {
            if (LogFilter.Debug) { Debug.Log("Client created version " + Version.Current); }
            AddClient(this);

            SetActive(true);
            m_Connection = conn;
            connectState = ConnectState.Connected;
            conn.SetHandlers(m_MessageHandlers);
            RegisterSystemHandlers(false);
        }

        public void Connect(string serverIp, int serverPort)
        {
            PrepareForConnect();

            if (LogFilter.Debug) { Debug.Log("Client Connect: " + serverIp + ":" + serverPort); }

            string hostnameOrIp = serverIp;
            m_ServerPort = serverPort;
            m_ServerIp = hostnameOrIp;

            connectState = ConnectState.Connecting;

            InitializeConnectionListeners();

            // setup all the handlers
            m_Connection = (NetworkConnection)Activator.CreateInstance(m_NetworkConnectionClass);
            m_Connection.SetHandlers(m_MessageHandlers);
            m_Connection.Initialize(m_ServerIp, m_ClientId, 0);

            Transport.layer.ClientConnect(serverIp, serverPort);

        }

        private void InitializeConnectionListeners()
        {
            Transport.layer.OnClientConnect += OnClientConnect;
            Transport.layer.OnClientData += OnClientData;
            Transport.layer.OnClientDisconnect += OnClientDisconnect;
            Transport.layer.OnClientError += OnClientError;
        }

        private void OnClientError(Exception obj)
        {
            Debug.Log("Error " + obj);
        }

        private void OnClientDisconnect()
        {
            connectState = ConnectState.Disconnected;

            ClientScene.HandleClientDisconnect(m_Connection);
            if (m_Connection != null)
            {
                m_Connection.InvokeHandlerNoData((short)MsgType.Disconnect);
            }
        }

        private void OnClientData(byte[] data)
        {
            if (m_Connection != null)
            {
                m_Connection.TransportReceive(data);
            }
            else Debug.LogError("Skipped Data message handling because m_Connection is null.");
        }

        private void OnClientConnect()
        {
            if (m_Connection != null)
            {
                // reset network time stats
                NetworkTime.Reset();

                // the handler may want to send messages to the client
                // thus we should set the connected state before calling the handler
                connectState = ConnectState.Connected;
                m_Connection.InvokeHandlerNoData((short)MsgType.Connect);
            }
            else Debug.LogError("Skipped Connect message handling because m_Connection is null.");
        }

        void PrepareForConnect()
        {
            SetActive(true);
            RegisterSystemHandlers(false);
            m_ClientId = 0;
            NetworkClient.pauseMessageHandling = false;
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
                    Debug.LogError("NetworkClient Send when not connected to a server");
                    return false;
                }
                return m_Connection.Send(msgType, msg);
            }
            Debug.LogError("NetworkClient Send with no connection");
            return false;
        }

        public void Shutdown()
        {
            if (LogFilter.Debug) Debug.Log("Shutting down client " + m_ClientId);
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
            if (connectState != ConnectState.Connecting &&
                connectState != ConnectState.Connected)
            {
                return;
            }

            // pause message handling while a scene load is in progress
            //
            // problem:
            //   if we handle packets (calling the msgDelegates) while a
            //   scene load is in progress, then all the handled data and state
            //   will be lost as soon as the scene load is finished, causing
            //   state bugs.
            //
            // solution:
            //   don't handle messages until scene load is finished. the
            //   transport layer will queue it automatically.
            if (pauseMessageHandling)
            {
                Debug.Log("NetworkClient.Update paused during scene load...");
                return;
            }

            if (connectState == ConnectState.Connected)
            {
                //NetworkTime.UpdateClient(this);
            }
        }

        [Obsolete("Use NetworkTime.rtt instead")]
        public float GetRTT()
        {
            return (float)NetworkTime.rtt;
        }

        internal void RegisterSystemHandlers(bool localClient)
        {
            ClientScene.RegisterSystemHandlers(this, localClient);
        }

        public void RegisterHandler(short msgType, NetworkMessageDelegate handler)
        {
            if (m_MessageHandlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) { Debug.Log("NetworkClient.RegisterHandler replacing " + msgType); }
            }
            m_MessageHandlers[msgType] = handler;
        }

        public void RegisterHandler(MsgType msgType, NetworkMessageDelegate handler)
        {
            RegisterHandler((short)msgType, handler);
        }

        public void UnregisterHandler(short msgType)
        {
            m_MessageHandlers.Remove(msgType);
        }

        public void UnregisterHandler(MsgType msgType)
        {
            UnregisterHandler((short)msgType);
        }

        internal static void AddClient(NetworkClient client)
        {
            s_Clients.Add(client);
        }

        internal static bool RemoveClient(NetworkClient client)
        {
            return s_Clients.Remove(client);
        }

        internal static void UpdateClients()
        {
            // remove null clients first
            s_Clients.RemoveAll(cl => cl == null);

            // now update valid clients
            for (int i = 0; i < s_Clients.Count; ++i)
            {
                s_Clients[i].Update();
            }
        }

        public static void ShutdownAll()
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
    }
}
