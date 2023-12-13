using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Mirror
{
    // a transport that can listen to multiple underlying transport at the same time
    [DisallowMultipleComponent]
    public class MultiplexTransport : Transport, PortTransport
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

        // (original connectionId, transport#) to multiplexed connectionId
        readonly Dictionary<KeyValuePair<int, int>, int> originalToMultiplexedId =
            new Dictionary<KeyValuePair<int, int>, int>(100);

        // multiplexed connectionId to (original connectionId, transport#)
        readonly Dictionary<int, KeyValuePair<int, int>> multiplexedToOriginalId =
            new Dictionary<int, KeyValuePair<int, int>>(100);

        // next multiplexed id counter. start at 1 because 0 is reserved for host.
        int nextMultiplexedId = 1;

        // prevent log flood from OnGUI or similar per-frame updates
        bool alreadyWarned;

        public ushort Port
        {
            get
            {
                foreach (Transport transport in transports)
                    if (transport.Available() && transport is PortTransport portTransport)
                        return portTransport.Port;

                return 0;
            }
            set
            {
                if (Utils.IsHeadless() && !alreadyWarned)
                {
                    // prevent log flood from OnGUI or similar per-frame updates
                    alreadyWarned = true;
                    Debug.LogWarning($"MultiplexTransport: Server cannot set the same listen port for all transports! Set them directly instead.");
                }

                else
                {
                    // We can't set the same port for all transports because
                    // listen ports have to be different for each transport
                    // so we just set the first available one.
                    // This depends on the selected build platform.
                    foreach (Transport transport in transports)
                        if (transport.Available() && transport is PortTransport portTransport)
                        {
                            portTransport.Port = value;
                            break;
                        }
                }
            }
        }

        // add to bidirection lookup. returns the multiplexed connectionId.
        public int AddToLookup(int originalConnectionId, int transportIndex)
        {
            // add to both
            KeyValuePair<int, int> pair = new KeyValuePair<int, int>(originalConnectionId, transportIndex);
            int multiplexedId = nextMultiplexedId++;

            originalToMultiplexedId[pair] = multiplexedId;
            multiplexedToOriginalId[multiplexedId] = pair;

            return multiplexedId;
        }

        public void RemoveFromLookup(int originalConnectionId, int transportIndex)
        {
            // remove from both
            KeyValuePair<int, int> pair = new KeyValuePair<int, int>(originalConnectionId, transportIndex);
            int multiplexedId = originalToMultiplexedId[pair];

            originalToMultiplexedId.Remove(pair);
            multiplexedToOriginalId.Remove(multiplexedId);
        }

        public void OriginalId(int multiplexId, out int originalConnectionId, out int transportIndex)
        {
            KeyValuePair<int, int> pair = multiplexedToOriginalId[multiplexId];
            originalConnectionId = pair.Key;
            transportIndex       = pair.Value;
        }

        public int MultiplexId(int originalConnectionId, int transportIndex)
        {
            KeyValuePair<int, int> pair = new KeyValuePair<int, int>(originalConnectionId, transportIndex);
            return originalToMultiplexedId[pair];
        }

        ////////////////////////////////////////////////////////////////////////

        public void Awake()
        {
            if (transports == null || transports.Length == 0)
            {
                Debug.LogError("[Multiplexer] Multiplex transport requires at least 1 underlying transport");
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
            throw new ArgumentException("[Multiplexer] No transport suitable for this platform");
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
            throw new ArgumentException("[Multiplexer] No transport suitable for this platform");
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
                int transportIndex = i;
                Transport transport = transports[i];

                transport.OnServerConnected = (originalConnectionId =>
                {
                    // invoke Multiplex event with multiplexed connectionId
                    int multiplexedId = AddToLookup(originalConnectionId, transportIndex);
                    OnServerConnected.Invoke(multiplexedId);
                });

                transport.OnServerDataReceived = (originalConnectionId, data, channel) =>
                {
                    // invoke Multiplex event with multiplexed connectionId
                    int multiplexedId = MultiplexId(originalConnectionId, transportIndex);
                    OnServerDataReceived.Invoke(multiplexedId, data, channel);
                };

                transport.OnServerError = (originalConnectionId, error, reason) =>
                {
                    // invoke Multiplex event with multiplexed connectionId
                    int multiplexedId = MultiplexId(originalConnectionId, transportIndex);
                    OnServerError.Invoke(multiplexedId, error, reason);
                };

                transport.OnServerDisconnected = originalConnectionId =>
                {
                    // invoke Multiplex event with multiplexed connectionId
                    int multiplexedId = MultiplexId(originalConnectionId, transportIndex);
                    OnServerDisconnected.Invoke(multiplexedId);
                    RemoveFromLookup(originalConnectionId, transportIndex);
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
            // convert multiplexed connectionId to original id & transport index
            OriginalId(connectionId, out int originalConnectionId, out int transportIndex);
            return transports[transportIndex].ServerGetClientAddress(originalConnectionId);
        }

        public override void ServerDisconnect(int connectionId)
        {
            // convert multiplexed connectionId to original id & transport index
            OriginalId(connectionId, out int originalConnectionId, out int transportIndex);
            transports[transportIndex].ServerDisconnect(originalConnectionId);
        }

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId)
        {
            // convert multiplexed connectionId to original transport + connId
            OriginalId(connectionId, out int originalConnectionId, out int transportIndex);
            transports[transportIndex].ServerSend(originalConnectionId, segment, channelId);
        }

        public override void ServerStart()
        {
            AddServerCallbacks();

            foreach (Transport transport in transports)
            {
                transport.ServerStart();

                if (transport is PortTransport portTransport)
                {
                    if (Utils.IsHeadless())
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Server listening on port {portTransport.Port}");
                        Console.ResetColor();
                    }
                    else
                    {
                        Debug.Log($"Server listening on port {portTransport.Port}");
                    }
                }
            }
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
            builder.Append("Multiplexer:");

            foreach (Transport transport in transports)
                builder.Append($" {transport}");

            return builder.ToString().Trim();
        }
    }
}
