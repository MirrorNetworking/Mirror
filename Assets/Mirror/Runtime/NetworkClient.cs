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

        static bool s_IsActive;

        public static List<NetworkClient> allClients = new List<NetworkClient>();
        public static bool active { get { return s_IsActive; } }

        public static bool pauseMessageHandling;

        string m_ServerIp = "";
        ushort m_ServerPort;
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
        public ushort serverPort { get { return m_ServerPort; } }
        public ushort hostPort;
        public NetworkConnection connection { get { return m_Connection; } }

        public Dictionary<short, NetworkMessageDelegate> handlers { get { return m_MessageHandlers; } }

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

        public void Connect(string serverIp, ushort serverPort)
        {
            PrepareForConnect();

            if (LogFilter.Debug) { Debug.Log("Client Connect: " + serverIp + ":" + serverPort); }

            string hostnameOrIp = serverIp;
            m_ServerPort = serverPort;
            m_ServerIp = hostnameOrIp;

            connectState = ConnectState.Connecting;

            // setup all the handlers
            m_Connection = (NetworkConnection)Activator.CreateInstance(m_NetworkConnectionClass);
            m_Connection.SetHandlers(m_MessageHandlers);
            m_Connection.Initialize(m_ServerIp, m_ClientId, 0);

            Transport.layer.ClientConnect(serverIp, serverPort);
        }

        private void InitializeTransportHandlers()
        {
            Transport.layer.OnClientConnect += OnClientConnect;
            Transport.layer.OnClientData += OnClientData;
            Transport.layer.OnClientDisconnect += OnClientDisconnect;
            Transport.layer.OnClientError += OnClientError;
        }

        private void OnClientError(Exception exception)
        {
            NetworkError errorMessage = new NetworkError
            {
                msgType = (short)MsgType.Error,
                conn = m_Connection,
                exception = exception
            };

            if (m_Connection != null)
            {
                m_Connection.InvokeHandler(errorMessage);
            }
            else
            {
                Debug.LogException(exception);
            }
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
                NetworkTime.UpdateClient(this);
                m_Connection.InvokeHandlerNoData((short)MsgType.Connect);
            }
            else Debug.LogError("Skipped Connect message handling because m_Connection is null.");
        }

        void PrepareForConnect()
        {
            SetActive(true);
            RegisterSystemHandlers(false);
            m_ClientId = 0;
            pauseMessageHandling = false;
            InitializeTransportHandlers();
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
                RemoveTransportHandlers();

                m_ClientId = -1;
            }
        }

        private void RemoveTransportHandlers()
        {
            // so that we don't register them more than once
            Transport.layer.OnClientConnect -= OnClientConnect;
            Transport.layer.OnClientData -= OnClientData;
            Transport.layer.OnClientDisconnect -= OnClientDisconnect;
            Transport.layer.OnClientError -= OnClientError;
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
            if (allClients.Count == 0)
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
                NetworkTime.UpdateClient(this);
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
            allClients.Add(client);
        }

        internal static bool RemoveClient(NetworkClient client)
        {
            return allClients.Remove(client);
        }

        internal static void UpdateClients()
        {
            // remove null clients first
            allClients.RemoveAll(cl => cl == null);

            // now update valid clients
            for (int i = 0; i < allClients.Count; ++i)
            {
                allClients[i].Update();
            }
        }

        public static void ShutdownAll()
        {
            while (allClients.Count != 0)
            {
                allClients[0].Shutdown();
            }
            allClients = new List<NetworkClient>();
            s_IsActive = false;
            ClientScene.Shutdown();
        }

        internal static void SetActive(bool state)
        {
            s_IsActive = state;
        }
    }
}
