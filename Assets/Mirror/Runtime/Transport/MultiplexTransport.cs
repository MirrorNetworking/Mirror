using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Mirror
{
    // a transport that can listen to multiple underlying transport at the same time
    public class MultiplexTransport : Transport
    {
        public Transport[] transports;

        Transport available;

        // used to partition recipients for each one of the base transports
        // without allocating a new list every time
        List<int>[] recipientsCache;

        public void Awake()
        {
            if (transports == null || transports.Length == 0)
            {
                Debug.LogError("Multiplex transport requires at least 1 underlying transport");
            }
            InitClient();
            InitServer();
        }

        public override bool Available()
        {
            // available if any of the transports is available
            foreach (Transport transport in transports)
            {
                if (transport.Available())
                {
                    return true;
                }
            }
            return false;
        }

        #region Client
        // clients always pick the first transport
        void InitClient()
        {
            // wire all the base transports to my events
            foreach (Transport transport in transports)
            {
                transport.OnClientConnected.AddListener(OnClientConnected.Invoke );
                transport.OnClientDataReceived.AddListener(OnClientDataReceived.Invoke);
                transport.OnClientError.AddListener(OnClientError.Invoke );
                transport.OnClientDisconnected.AddListener(OnClientDisconnected.Invoke);
            }
        }

        public override void ClientConnect(string address)
        {
            foreach (Transport transport in transports)
            {
                if (transport.Available())
                {
                    available = transport;
                    transport.ClientConnect(address);

                }
            }
            throw new Exception("No transport suitable for this platform");
        }

        public override bool ClientConnected()
        {
            return available != null && available.ClientConnected();
        }

        public override void ClientDisconnect()
        {
            if (available != null)
                available.ClientDisconnect();
        }

        public override bool ClientSend(int channelId, ArraySegment<byte> segment)
        {
            return available.ClientSend(channelId, segment);
        }

        public override int GetMaxPacketSize(int channelId = 0)
        {
            return available.GetMaxPacketSize(channelId);
        }

        #endregion


        #region Server
        // connection ids get mapped to base transports
        // if we have 3 transports,  then
        // transport 0 will produce connection ids [0, 3, 6, 9, ...]
        // transport 1 will produce connection ids [1, 4, 7, 10, ...]
        // transport 2 will produce connection ids [2, 5, 8, 11, ...]
        int FromBaseId(int transportId, int connectionId)
        {
            return connectionId * transports.Length + transportId;
        }

        int ToBaseId(int connectionId)
        {
            return connectionId / transports.Length;
        }

        int ToTransportId(int connectionId)
        {
            return connectionId % transports.Length;
        }

        void InitServer()
        {
            recipientsCache = new List<int>[transports.Length];

            // wire all the base transports to my events
            for (int i = 0; i < transports.Length; i++)
            {
                recipientsCache[i] = new List<int>();

                // this is required for the handlers,  if I use i directly
                // then all the handlers will use the last i
                int locali = i;
                Transport transport = transports[i];

                transport.OnServerConnected.AddListener(baseConnectionId =>
                {
                    OnServerConnected.Invoke(FromBaseId(locali, baseConnectionId));
                });

                transport.OnServerDataReceived.AddListener((baseConnectionId, data, channel) =>
                {
                    OnServerDataReceived.Invoke(FromBaseId(locali, baseConnectionId), data, channel);
                });

                transport.OnServerError.AddListener((baseConnectionId, error) =>
                {
                    OnServerError.Invoke(FromBaseId(locali, baseConnectionId), error);
                });
                transport.OnServerDisconnected.AddListener(baseConnectionId =>
                {
                    OnServerDisconnected.Invoke(FromBaseId(locali, baseConnectionId));
                });
            }
        }

        public override bool ServerActive()
        {
            // avoid Linq.All allocations
            foreach (Transport transport in transports)
            {
                if (!transport.ServerActive())
                {
                    return false;
                }
            }
            return true;
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            int baseConnectionId = ToBaseId(connectionId);
            int transportId = ToTransportId(connectionId);
            return transports[transportId].ServerGetClientAddress(baseConnectionId);
        }

        public override bool ServerDisconnect(int connectionId)
        {
            int baseConnectionId = ToBaseId(connectionId);
            int transportId = ToTransportId(connectionId);
            return transports[transportId].ServerDisconnect(baseConnectionId);
        }

        public override bool ServerSend(List<int> connectionIds, int channelId, ArraySegment<byte> segment)
        {
            // the message may be for different transports,
            // partition the recipients by transport
            foreach (List<int> list in recipientsCache)
            {
                list.Clear();
            }

            foreach (int connectionId in connectionIds)
            {
                int baseConnectionId = ToBaseId(connectionId);
                int transportId = ToTransportId(connectionId);
                recipientsCache[transportId].Add(baseConnectionId);
            }

            bool result = true;
            for (int i = 0; i < transports.Length; ++i)
            {
                List<int> baseRecipients = recipientsCache[i];
                if (baseRecipients.Count > 0)
                {
                    result &= transports[i].ServerSend(baseRecipients, channelId, segment);
                }
            }
            return result;
        }

        public override void ServerStart()
        {
            foreach (Transport transport in transports)
            {
                transport.ServerStart();
            }
        }

        public override void ServerStop()
        {
            foreach (Transport transport in transports)
            {
                transport.ServerStop();
            }
        }
        #endregion

        public override void Shutdown()
        {
            foreach (Transport transport in transports)
            {
                transport.Shutdown();
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (Transport transport in transports)
            {
                builder.AppendLine(transport.ToString());
            }
            return builder.ToString().Trim();
        }
    }
}
