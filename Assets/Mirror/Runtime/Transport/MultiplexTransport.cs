using System;
using System.Linq;
using UnityEngine;

namespace Mirror.Transport
{
    // a transport that can listen to multiple underlying transport at the same time
    public class MultiplexTransport : TransportLayer
    {
        private readonly TransportLayer[] transports;

        public MultiplexTransport(params TransportLayer [] baseTransports)
        {
            if (baseTransports.Length == 0)
            {
                Debug.LogError("Multiplex transport requires at least 1 underlying transport");
            }
            this.transports = baseTransports;
            InitClient();
            InitServer();
        }

        #region Client
        // clients always pick the first transport

        public event Action OnClientConnect;
        public event Action<byte[]> OnClientData;
        public event Action<Exception> OnClientError;
        public event Action OnClientDisconnect;

        private void InitClient()
        {
            // wire all the base transports to my events
            foreach (TransportLayer transport in transports)
            {
                transport.OnClientConnect += () => { OnClientConnect?.Invoke(); };
                transport.OnClientData += data => { OnClientData?.Invoke(data); };
                transport.OnClientError += error => { OnClientError?.Invoke(error); };
                transport.OnClientDisconnect += () => { OnClientDisconnect?.Invoke(); };
            }
        }

        public void ClientConnect(string address, int port)
        {
            transports[0].ClientConnect(address, port);
        }

        public bool ClientConnected()
        {
            return transports[0].ClientConnected();
        }

        public void ClientDisconnect()
        {
            transports[0].ClientDisconnect();
        }

        public void ClientSend(int channelId, byte[] data)
        {
            transports[0].ClientSend(channelId, data);
        }

        public int GetMaxPacketSize(int channelId = 0)
        {
            return transports[0].GetMaxPacketSize(channelId);
        }

        #endregion


        #region Server
        public event Action<int> OnServerConnect;
        public event Action<int, byte[]> OnServerData;
        public event Action<int, Exception> OnServerError;
        public event Action<int> OnServerDisconnect;

        // connection ids get mapped to base transports
        // if we have 3 transports,  then
        // transport 0 will produce connection ids [0, 3, 6, 9, ...]
        // transport 1 will produce connection ids [1, 4, 7, 10, ...]
        // transport 2 will produce connection ids [2, 5, 8, 11, ...]
        private int FromBaseId(int transportId, int connectionId)
        {
            return connectionId * transports.Length + transportId;
        }

        private int ToBaseId(int connectionId)
        {
            return connectionId / transports.Length;
        }

        private int ToTransportId(int connectionId)
        {
            return connectionId % transports.Length;
        }

        void InitServer()
        {
            // wire all the base transports to my events
            for (int i=0; i< transports.Length; i++)
            {
                // this is required for the handlers,  if I use i directly
                // then all the handlers will use the last i
                int locali = i;
                TransportLayer transport = transports[i];

                transport.OnServerConnect += baseConnectionId =>
                {
                    OnServerConnect?.Invoke(FromBaseId(locali, baseConnectionId));
                };

                transport.OnServerData += (baseConnectionId, data) =>
                {
                    OnServerData?.Invoke(FromBaseId(locali, baseConnectionId), data);
                };
                transport.OnServerError += (baseConnectionId, error) =>
                {
                    OnServerError?.Invoke(FromBaseId(locali, baseConnectionId), error);
                };
                transport.OnServerDisconnect += baseConnectionId =>
                {
                    OnServerDisconnect?.Invoke(FromBaseId(locali, baseConnectionId));
                };
            }
        }


        public bool ServerActive()
        {
            return transports.All(t => t.ServerActive());
        }


        public bool GetConnectionInfo(int connectionId, out string address)
        {
            int baseConnectionId = ToBaseId(connectionId);
            int transportId = ToTransportId(connectionId);
            return transports[transportId].GetConnectionInfo(baseConnectionId, out address);
        }

        public bool ServerDisconnect(int connectionId)
        {
            int baseConnectionId = ToBaseId(connectionId);
            int transportId = ToTransportId(connectionId);
            return transports[transportId].ServerDisconnect(baseConnectionId);
        }

        public void ServerSend(int connectionId, int channelId, byte[] data)
        {
            int baseConnectionId = ToBaseId(connectionId);
            int transportId = ToTransportId(connectionId);
            transports[transportId].ServerSend(baseConnectionId, channelId, data);
        }

        public void ServerStart()
        {
            foreach (TransportLayer transport in transports)
            {
                transport.ServerStart();
            }
        }

        public void ServerStop()
        {
            foreach (TransportLayer transport in transports)
            {
                transport.ServerStop();
            }
        }
        #endregion

        public void Shutdown()
        {
            foreach (TransportLayer transport in transports)
            {
                transport.Shutdown();
            }
        }
    }
}
