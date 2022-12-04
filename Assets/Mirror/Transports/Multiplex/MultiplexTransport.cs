using System;
using System.Collections.Generic;
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

        // underlying transport connectionId to multiplexed connectionId lookup.
        //
        // originally we used a formula to map the connectionId:
        //   connectionId * transportAmount + transportId
        //
        // if we have 3 transports, then
        //   transport 0 will produce connection ids [0, 3, 6, 9, ...]
        //   transport 1 will produce connection ids [1, 4, 7, 10, ...]
        //   transport 2 will produce connection ids [2, 5, 8, 11, ...]
        //
        // however, some transports like kcp may give very large connectionIds.
        // if they are near int.max, then "* transprotAmount + transportIndex"
        // will overflow, resulting in connIds which can't be projected back.
        //   https://github.com/vis2k/Mirror/issues/3280
        //
        // instead, use a simple lookup with 0-indexed ids.
        // with initial capacity to avoid runtime allocations.
        readonly Dictionary<int, int> lookup = new Dictionary<int, int>(100);

        // connection ids get mapped to base transports
        // if we have 3 transports, then
        // transport 0 will produce connection ids [0, 3, 6,  9, ...]
        // transport 1 will produce connection ids [1, 4, 7, 10, ...]
        // transport 2 will produce connection ids [2, 5, 8, 11, ...]

        // convert original transport connId to multiplexed connId
        public static int MultiplexConnectionId(int connectionId, int transportId, int transportAmount) =>
            connectionId * transportAmount + transportId;

        // convert multiplexed connectionId back to original transport connId
        public static int OriginalConnectionId(int multiplexConnectionId, int transportAmount) =>
            multiplexConnectionId / transportAmount;

        // convert multiplexed connectionId back to original transportId
        public static int OriginalTransportId(int multiplexConnectionId, int transportAmount) =>
            multiplexConnectionId % transportAmount;

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
                transport.ClientEarlyUpdate();
        }

        public override void ServerEarlyUpdate()
        {
            foreach (Transport transport in transports)
                transport.ServerEarlyUpdate();
        }

        public override void ClientLateUpdate()
        {
            foreach (Transport transport in transports)
                transport.ClientLateUpdate();
        }

        public override void ServerLateUpdate()
        {
            foreach (Transport transport in transports)
                transport.ServerLateUpdate();
        }

        void OnEnable()
        {
            foreach (Transport transport in transports)
                transport.enabled = true;
        }

        void OnDisable()
        {
            foreach (Transport transport in transports)
                transport.enabled = false;
        }

        public override bool Available()
        {
            // available if any of the transports is available
            foreach (Transport transport in transports)
                if (transport.Available())
                    return true;

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
        void AddServerCallbacks()
        {
            // all underlying transports should call the multiplex transport's events
            for (int i = 0; i < transports.Length; i++)
            {
                // this is required for the handlers, if I use i directly
                // then all the handlers will use the last i
                int locali = i;
                Transport transport = transports[i];

                transport.OnServerConnected = (baseConnectionId =>
                {
                    // invoke Multiplex event with multiplexed connectionId
                    OnServerConnected.Invoke(MultiplexConnectionId(baseConnectionId, locali, transports.Length));
                });

                transport.OnServerDataReceived = (baseConnectionId, data, channel) =>
                {
                    // invoke Multiplex event with multiplexed connectionId
                    OnServerDataReceived.Invoke(MultiplexConnectionId(baseConnectionId, locali, transports.Length), data, channel);
                };

                transport.OnServerError = (baseConnectionId, error, reason) =>
                {
                    // invoke Multiplex event with multiplexed connectionId
                    OnServerError.Invoke(MultiplexConnectionId(baseConnectionId, locali, transports.Length), error, reason);
                };
                transport.OnServerDisconnected = baseConnectionId =>
                {
                    // invoke Multiplex event with multiplexed connectionId
                    OnServerDisconnected.Invoke(MultiplexConnectionId(baseConnectionId, locali, transports.Length));
                };
            }
        }

        // for now returns the first uri,
        // should we return all available uris?
        public override Uri ServerUri() =>
            transports[0].ServerUri();

        public override bool ServerActive()
        {
            // avoid Linq.All allocations
            foreach (Transport transport in transports)
                if (!transport.ServerActive())
                    return false;

            return true;
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            // convert multiplexed connectionId to original transport + connId
            int baseConnectionId = OriginalConnectionId(connectionId, transports.Length);
            int transportId = OriginalTransportId(connectionId, transports.Length);
            return transports[transportId].ServerGetClientAddress(baseConnectionId);
        }

        public override void ServerDisconnect(int connectionId)
        {
            // convert multiplexed connectionId to original transport + connId
            int baseConnectionId = OriginalConnectionId(connectionId, transports.Length);
            int transportId = OriginalTransportId(connectionId, transports.Length);
            transports[transportId].ServerDisconnect(baseConnectionId);
        }

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId)
        {
            // convert multiplexed connectionId to original transport + connId
            int baseConnectionId = OriginalConnectionId(connectionId, transports.Length);
            int transportId = OriginalTransportId(connectionId, transports.Length);
            transports[transportId].ServerSend(baseConnectionId, segment, channelId);
        }

        public override void ServerStart()
        {
            AddServerCallbacks();

            foreach (Transport transport in transports)
                transport.ServerStart();
        }

        public override void ServerStop()
        {
            foreach (Transport transport in transports)
                transport.ServerStop();
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
                transport.Shutdown();
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();

            foreach (Transport transport in transports)
                builder.AppendLine(transport.ToString());

            return builder.ToString().Trim();
        }
    }
}
