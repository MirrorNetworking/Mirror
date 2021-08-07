using System;
using System.Text;
using UnityEngine;

namespace Mirror
{
    // a transport that can listen to multiple underlying transport at the same time
    [DisallowMultipleComponent]
    public class MultiplexTransport : Transport
    {
        public Transport[] transports;

        Transport available;

        public void Awake()
        {
            if (transports == null || transports.Length == 0)
            {
                Debug.LogError("Multiplex transport requires at least 1 underlying transport");
            }
        }

        public override void ClientEarlyUpdate()
        {
            foreach (Transport transport in transports)
            {
                transport.ClientEarlyUpdate();
            }
        }

        public override void ServerEarlyUpdate()
        {
            foreach (Transport transport in transports)
            {
                transport.ServerEarlyUpdate();
            }
        }

        public override void ClientLateUpdate()
        {
            foreach (Transport transport in transports)
            {
                transport.ClientLateUpdate();
            }
        }

        public override void ServerLateUpdate()
        {
            foreach (Transport transport in transports)
            {
                transport.ServerLateUpdate();
            }
        }

        void OnEnable()
        {
            foreach (Transport transport in transports)
            {
                transport.enabled = true;
            }
        }

        void OnDisable()
        {
            foreach (Transport transport in transports)
            {
                transport.enabled = false;
            }
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

        public override void ClientConnect(string address)
        {
            foreach (Transport transport in transports)
            {
                if (transport.Available())
                {
                    available = transport;
                    transport.OnClientConnected = OnClientConnected;
                    transport.OnClientDataReceived = OnClientDataReceived;
                    transport.OnClientError = OnClientError;
                    transport.OnClientDisconnected = OnClientDisconnected;
                    transport.ClientConnect(address);
                    return;
                }
            }
            throw new ArgumentException("No transport suitable for this platform");
        }

        public override void ClientConnect(Uri uri)
        {
            foreach (Transport transport in transports)
            {
                if (transport.Available())
                {
                    try
                    {
                        available = transport;
                        transport.OnClientConnected = OnClientConnected;
                        transport.OnClientDataReceived = OnClientDataReceived;
                        transport.OnClientError = OnClientError;
                        transport.OnClientDisconnected = OnClientDisconnected;
                        transport.ClientConnect(uri);
                        return;
                    }
                    catch (ArgumentException)
                    {
                        // transport does not support the schema, just move on to the next one
                    }
                }
            }
            throw new ArgumentException("No transport suitable for this platform");
        }

        public override bool ClientConnected()
        {
            return (object)available != null && available.ClientConnected();
        }

        public override void ClientDisconnect()
        {
            if ((object)available != null)
                available.ClientDisconnect();
        }

        public override void ClientSend(ArraySegment<byte> segment, int channelId)
        {
            available.ClientSend(segment, channelId);
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

        void AddServerCallbacks()
        {
            // wire all the base transports to my events
            for (int i = 0; i < transports.Length; i++)
            {
                // this is required for the handlers,  if I use i directly
                // then all the handlers will use the last i
                int locali = i;
                Transport transport = transports[i];

                transport.OnServerConnected = (baseConnectionId =>
                {
                    OnServerConnected.Invoke(FromBaseId(locali, baseConnectionId));
                });

                transport.OnServerDataReceived = (baseConnectionId, data, channel) =>
                {
                    OnServerDataReceived.Invoke(FromBaseId(locali, baseConnectionId), data, channel);
                };

                transport.OnServerError = (baseConnectionId, error) =>
                {
                    OnServerError.Invoke(FromBaseId(locali, baseConnectionId), error);
                };
                transport.OnServerDisconnected = baseConnectionId =>
                {
                    OnServerDisconnected.Invoke(FromBaseId(locali, baseConnectionId));
                };
            }
        }

        // for now returns the first uri,
        // should we return all available uris?
        public override Uri ServerUri()
        {
            return transports[0].ServerUri();
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

        public override void ServerDisconnect(int connectionId)
        {
            int baseConnectionId = ToBaseId(connectionId);
            int transportId = ToTransportId(connectionId);
            transports[transportId].ServerDisconnect(baseConnectionId);
        }

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId)
        {
            int baseConnectionId = ToBaseId(connectionId);
            int transportId = ToTransportId(connectionId);

            for (int i = 0; i < transports.Length; ++i)
            {
                if (i == transportId)
                {
                    transports[i].ServerSend(baseConnectionId, segment, channelId);
                }
            }
        }

        public override void ServerStart()
        {
            foreach (Transport transport in transports)
            {
                AddServerCallbacks();
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

        public override int GetMaxPacketSize(int channelId = 0)
        {
            // finding the max packet size in a multiplex environment has to be
            // done very carefully:
            // * servers run multiple transports at the same time
            // * different clients run different transports
            // * there should only ever be ONE true max packet size for everyone,
            //   otherwise a spawn message might be sent to all tcp sockets, but
            //   be too big for some udp sockets. that would be a debugging
            //   nightmare and allow for possible exploits and players on
            //   different platforms seeing a different game state.
            // => the safest solution is to use the smallest max size for all
            //    transports. that will never fail.
            int mininumAllowedSize = int.MaxValue;
            foreach (Transport transport in transports)
            {
                int size = transport.GetMaxPacketSize(channelId);
                mininumAllowedSize = Mathf.Min(size, mininumAllowedSize);
            }
            return mininumAllowedSize;
        }

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
