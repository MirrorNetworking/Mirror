using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class NetworkClient
    {
        static bool s_IsActive;

        public static List<NetworkClient> allClients = new List<NetworkClient>();
        public static bool active => s_IsActive;

        string m_ServerIp = "";
        int m_ClientId = -1;

        public readonly Dictionary<short, NetworkMessageDelegate> handlers = new Dictionary<short, NetworkMessageDelegate>();
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
            conn.SetHandlers(handlers);
        }

        public string serverIp => m_ServerIp; 
        public NetworkConnection connection => m_Connection;

        public bool isConnected => connectState == ConnectState.Connected;

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
            conn.SetHandlers(handlers);
            RegisterSystemHandlers(false);
        }

        public void Connect(string serverIp)
        {
            PrepareForConnect();

            if (LogFilter.Debug) { Debug.Log("Client Connect: " + serverIp); }

            string hostnameOrIp = serverIp;
            m_ServerIp = hostnameOrIp;

            connectState = ConnectState.Connecting;
            NetworkManager.singleton.transport.ClientConnect(serverIp);

            // setup all the handlers
            m_Connection = new NetworkConnection(m_ServerIp, m_ClientId, 0);
            m_Connection.SetHandlers(handlers);
        }

        private void InitializeTransportHandlers()
        {
            // TODO do this in inspector?
            NetworkManager.singleton.transport.OnClientConnected.AddListener(OnConnected);
            NetworkManager.singleton.transport.OnClientDataReceived.AddListener(OnDataReceived);
            NetworkManager.singleton.transport.OnClientDisconnected.AddListener(OnDisconnected);
            NetworkManager.singleton.transport.OnClientError.AddListener(OnError);
        }

        void OnError(Exception exception)
        {
            Debug.LogException(exception);
        }

        void OnDisconnected()
        {
            connectState = ConnectState.Disconnected;

            ClientScene.HandleClientDisconnect(m_Connection);
           
            m_Connection?.InvokeHandlerNoData((short)MsgType.Disconnect);
        }

        void OnDataReceived(byte[] data)
        {
            if (m_Connection != null)
            {
                m_Connection.TransportReceive(data);
            }
            else Debug.LogError("Skipped Data message handling because m_Connection is null.");
        }

        void OnConnected()
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
            NetworkManager.singleton.transport.enabled = true;
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
                m_ClientId = -1;
                RemoveTransportHandlers();
            }
        }

        void RemoveTransportHandlers()
        {
            // so that we don't register them more than once
            NetworkManager.singleton.transport.OnClientConnected.RemoveListener(OnConnected);
            NetworkManager.singleton.transport.OnClientDataReceived.RemoveListener(OnDataReceived);
            NetworkManager.singleton.transport.OnClientDisconnected.RemoveListener(OnDisconnected);
            NetworkManager.singleton.transport.OnClientError.RemoveListener(OnError);
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

            if (connectState == ConnectState.Connected)
            {
                NetworkTime.UpdateClient(this);
            }
        }

        void GenerateConnectError(byte error)
        {
            Debug.LogError("UNet Client Error Connect Error: " + error);
            GenerateError(error);
        }

        /* TODO use or remove
        void GenerateDataError(byte error)
        {
            NetworkError dataError = (NetworkError)error;
            Debug.LogError("UNet Client Data Error: " + dataError);
            GenerateError(error);
        }

        void GenerateDisconnectError(byte error)
        {
            NetworkError disconnectError = (NetworkError)error;
            Debug.LogError("UNet Client Disconnect Error: " + disconnectError);
            GenerateError(error);
        }
        */

        void GenerateError(byte error)
        {
            NetworkMessageDelegate msgDelegate;
            if (handlers.TryGetValue((short)MsgType.Error, out msgDelegate))
            {
                ErrorMessage msg = new ErrorMessage
                {
                    value = error
                };

                // write the message to a local buffer
                NetworkWriter writer = new NetworkWriter();
                msg.Serialize(writer);

                NetworkMessage netMsg = new NetworkMessage
                {
                    msgType = (short)MsgType.Error,
                    reader = new NetworkReader(writer.ToArray()),
                    conn = m_Connection
                };
                msgDelegate(netMsg);
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
            if (handlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) { Debug.Log("NetworkClient.RegisterHandler replacing " + msgType); }
            }
            handlers[msgType] = handler;
        }

        public void RegisterHandler(MsgType msgType, NetworkMessageDelegate handler)
        {
            RegisterHandler((short)msgType, handler);
        }

        public void UnregisterHandler(short msgType)
        {
            handlers.Remove(msgType);
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
            // IMPORTANT: no foreach, otherwise we get an InvalidOperationException
            // when stopping the client.
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
