#if MIRROR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using kcp2k;

namespace Mirror.KCP
{
    public class KcpTransport : Transport
    {
        // common
        [Header("Transport Configuration")]
        public ushort Port = 7777;
        [Tooltip("NoDelay is recommended to reduce latency. This also scales better without buffers getting full.")]
        public bool NoDelay = true;
        [Tooltip("KCP internal update interval. 100ms is KCP default, but a lower interval is recommended to minimize latency and to scale to more networked entities.")]
        public uint Interval = 10;
        readonly byte[] buffer = new byte[Kcp.MTU_DEF];

        // server
        Socket serverSocket;
        EndPoint serverNewClientEP = new IPEndPoint(IPAddress.IPv6Any, 0);
        // connections <connectionId, connection> where connectionId is EndPoint.GetHashCode
        Dictionary<int, KcpServerConnection> connections = new Dictionary<int, KcpServerConnection>();

        // client
        KcpClientConnection clientConnection;
        bool clientConnected;

        void Awake()
        {
            Debug.Log("KcpTransport initialized!");
        }

        // all except WebGL
        public override bool Available() =>
            Application.platform != RuntimePlatform.WebGLPlayer;

        // use same Kcp configuration on server and client
        void ConfigureKcpConnection(KcpConnection connection)
        {
            // TODO consider lower interval IF interval matters in nodelay mode

            // we did this in previous test
            connection.kcp.SetNoDelay(1, 10, 2, true);

            // this works for 4k:
            //connection.kcp.SetWindowSize(128, 128);
            // this works for 10k:
            connection.kcp.SetWindowSize(512, 512);
            // this works for 20k:
            //connection.kcp.SetWindowSize(8192, 8192);
        }

        // client
        public override bool ClientConnected() => clientConnection != null;
        public override void ClientConnect(string address)
        {
            if (clientConnected)
            {
                Debug.LogWarning("KCP: client already connected!");
                return;
            }

            clientConnection = new KcpClientConnection();
            // setup events
            clientConnection.OnConnected += () =>
            {
                Debug.Log($"KCP: OnClientConnected");
                clientConnected = true;
                OnClientConnected.Invoke();
            };
            clientConnection.OnData += (message) =>
            {
                //Debug.Log($"KCP: OnClientDataReceived({BitConverter.ToString(message.Array, message.Offset, message.Count)})");
                OnClientDataReceived.Invoke(message);
            };
            clientConnection.OnDisconnected += () =>
            {
                Debug.Log($"KCP: OnClientDisconnected");
                clientConnected = false;
                OnClientDisconnected.Invoke();
            };

            // connect
            clientConnection.Connect(address, Port, NoDelay, Interval);

            // configure connection for max scale
            ConfigureKcpConnection(clientConnection);
        }
        public override bool ClientSend(int channelId, ArraySegment<byte> segment)
        {
            if (clientConnection != null)
            {
                clientConnection.Send(segment);
                return true;
            }
            Debug.LogWarning("KCP: can't send because client not connected!");
            return false;
        }

        public override void ClientDisconnect()
        {
            // only if connected
            // otherwise we end up in a deadlock because of an open Mirror bug:
            // https://github.com/vis2k/Mirror/issues/2353
            if (clientConnected)
            {
                clientConnection?.Disconnect();
                clientConnection = null;
            }
        }

        HashSet<int> connectionsToRemove = new HashSet<int>();
        void UpdateServer()
        {
            while (serverSocket != null && serverSocket.Poll(0, SelectMode.SelectRead))
            {
                int msgLength = serverSocket.ReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref serverNewClientEP);
                //Debug.Log($"KCP: server raw recv {msgLength} bytes = {BitConverter.ToString(buffer, 0, msgLength)}");

                // calculate connectionId from endpoint
                int connectionId = serverNewClientEP.GetHashCode();

                // is this a new connection?
                if (!connections.TryGetValue(connectionId, out KcpServerConnection connection))
                {
                    // add it to a queue
                    connection = new KcpServerConnection(serverSocket, serverNewClientEP, NoDelay, Interval);

                    // configure connection for max scale
                    ConfigureKcpConnection(connection);

                    //acceptedConnections.Writer.TryWrite(connection);
                    connections.Add(connectionId, connection);
                    Debug.Log($"KCP: server added connection {serverNewClientEP}");

                    // setup connected event
                    connection.OnConnected += () =>
                    {
                        // call mirror event
                        Debug.Log($"KCP: OnServerConnected({connectionId})");
                        OnServerConnected.Invoke(connectionId);
                    };

                    // setup data event
                    connection.OnData += (message) =>
                    {
                        // call mirror event
                        //Debug.Log($"KCP: OnServerDataReceived({connectionId}, {BitConverter.ToString(message.Array, message.Offset, message.Count)})");
                        OnServerDataReceived.Invoke(connectionId, message);
                    };

                    // setup disconnected event
                    connection.OnDisconnected += () =>
                    {
                        // flag for removal
                        // (can't remove directly because connection is updated
                        //  and event is called while iterating all connections)
                        connectionsToRemove.Add(connectionId);

                        // call mirror event
                        Debug.Log($"KCP: OnServerDisconnected({connectionId})");
                        OnServerDisconnected.Invoke(connectionId);
                    };

                    // send handshake
                    connection.Handshake();
                }

                connection.RawInput(buffer, msgLength);
            }

            // tick all server connections
            foreach (KcpServerConnection connection in connections.Values)
            {
                connection.Tick();
                connection.Receive();
            }

            // remove disconnected connections
            // (can't do it in connection.OnDisconnected because Tick is called
            //  while iterating connections)
            foreach (int connectionId in connectionsToRemove)
            {
                connections.Remove(connectionId);
            }
            connectionsToRemove.Clear();
        }

        void UpdateClient()
        {
            // tick client connection
            if (clientConnection != null)
            {
                clientConnection.Tick();
                // recv on socket
                clientConnection.RawReceive();
                // recv on kcp
                clientConnection.Receive();
            }
        }

        // IMPORTANT: set script execution order to >1000 to call Transport's
        //            LateUpdate after all others. Fixes race condition where
        //            e.g. in uSurvival Transport would apply Cmds before
        //            ShoulderRotation.LateUpdate, resulting in projectile
        //            spawns at the point before shoulder rotation.
        public void LateUpdate()
        {
            // note: we need to check enabled in case we set it to false
            // when LateUpdate already started.
            // (https://github.com/vis2k/Mirror/pull/379)
            if (!enabled)
                return;

            UpdateServer();
            UpdateClient();
        }

        // server
        public override bool ServerActive() => serverSocket != null;
        public override void ServerStart()
        {
            // only start once
            if (serverSocket != null)
            {
                Debug.LogWarning("KCP: server already started!");
            }

            // listen
            serverSocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            serverSocket.DualMode = true;
            serverSocket.Bind(new IPEndPoint(IPAddress.IPv6Any, Port));
        }
        public override bool ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
        {
            if (connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                connection.Send(segment);
                return true;
            }
            return false;
        }
        public override bool ServerDisconnect(int connectionId)
        {
            if (connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                connection.Disconnect();
                return true;
            }
            return false;
        }
        public override string ServerGetClientAddress(int connectionId)
        {
            if (connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                return (connection.GetRemoteEndPoint() as IPEndPoint).Address.ToString();
            }
            return "";
        }
        public override void ServerStop()
        {
            serverSocket?.Close();
            serverSocket = null;
        }

        // common
        public override void Shutdown() {}

        // MTU
        public override ushort GetMaxPacketSize() => Kcp.MTU_DEF;

        public override string ToString()
        {
            return "KCP";
        }

        int GetTotalSendQueue() =>
            connections.Values.Sum(conn => conn.kcp.snd_queue.Count);
        int GetTotalReceiveQueue() =>
            connections.Values.Sum(conn => conn.kcp.rcv_queue.Count);
        int GetTotalSendBuffer() =>
            connections.Values.Sum(conn => conn.kcp.snd_buf.Count);
        int GetTotalReceiveBuffer() =>
            connections.Values.Sum(conn => conn.kcp.rcv_buf.Count);

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(5, 100, 300, 300));

            if (ServerActive())
            {
                GUILayout.BeginVertical("Box");
                GUILayout.Label("SERVER");
                GUILayout.Label("  SendQueue: " + GetTotalSendQueue());
                GUILayout.Label("  ReceiveQueue: " + GetTotalReceiveQueue());
                GUILayout.Label("  SendBuffer: " + GetTotalSendBuffer());
                GUILayout.Label("  ReceiveBuffer: " + GetTotalReceiveBuffer());
                GUILayout.EndVertical();
            }

            if (ClientConnected())
            {
                GUILayout.BeginVertical("Box");
                GUILayout.Label("CLIENT");
                GUILayout.Label("  SendQueue: " + clientConnection.kcp.snd_queue.Count);
                GUILayout.Label("  ReceiveQueue: " + clientConnection.kcp.rcv_queue.Count);
                GUILayout.Label("  SendBuffer: " + clientConnection.kcp.snd_buf.Count);
                GUILayout.Label("  ReceiveBuffer: " + clientConnection.kcp.rcv_buf.Count);
                GUILayout.EndVertical();
            }

            GUILayout.EndArea();
        }
    }
}
#endif
