using UnityEngine;
using System.Net;
using System.Net.Sockets;
using Guid = System.Guid;
using UnityEngine.Profiling;
using System;
using System.Threading.Tasks;

namespace Mirror.Discovery
{
    // Based on https://github.com/in0finite/MirrorNetworkDiscovery (license missing, contacted in0finite confirmed MIT license)

    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkDiscovery")]
    [HelpURL("https://mirror-networking.com/xmldocs/articles/Transports/NetworkDiscovery.html")]
    public class NetworkDiscovery : MonoBehaviour
    {
        public static bool SupportedOnThisPlatform { get { return Application.platform != RuntimePlatform.WebGLPlayer; } }

        public static event Action<ServerInfo> OnServerFound;

        public long ServerId { get; private set; }

        // each game should have a random unique handshake,  this way you can tell if this is the same game or not
        [HideInInspector]
        public long secretHandshake;

        [SerializeField]
        [Tooltip("The UDP port the server will listen for multi-cast messages")]
        int serverBroadcastListenPort = 47777;

        [SerializeField]
        [Tooltip("Time in seconds between multi-cast messages")]
        float ActiveDiscoverySecondInterval = 3;

        UdpClient serverUdpClient = null;
        UdpClient clientUdpClient = null;

        [Tooltip("Port where the transport is listening,  set this to the same thing you set your transport")]
        public int transportPort = 7777;

        public void Awake()
        {
            ServerId = RandomLong();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (secretHandshake == 0)
            {
                secretHandshake = RandomLong();
                UnityEditor.Undo.RecordObject(this, "Set secret handshake");
            }
        }
#endif

        private static long RandomLong()
        {
            int value1 = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            int value2 = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            return value1 + ((long)value2 << 32);
        }


        // Ensure the ports are cleared no matter when Game/Unity UI exits
        void OnApplicationQuit()
        {
            Shutdown();
        }

        void Shutdown()
        {
            if (serverUdpClient != null)
            {
                try
                {
                    serverUdpClient.Close();
                }
                catch(Exception)
                {
                    // it is just close, swallow the error
                }
                serverUdpClient = null;
            }

            if (clientUdpClient != null)
            {
                try
                {
                    clientUdpClient.Close();
                }
                catch (Exception)
                {
                    // it is just close, swallow the error
                }
                clientUdpClient = null;
            }

            CancelInvoke();
        }

        #region Server

        /// <summary>
        /// Advertise this server in the local network
        /// </summary>
        /// <param name="networkManager">Network Manager</param>
        public void AdvertiseServer()
        {
            if (!SupportedOnThisPlatform)
            {
                throw new PlatformNotSupportedException("Network discovery not supported in this platform");
            }

            StopDiscovery();

            // Setup port
            // may throw exception
            serverUdpClient = new UdpClient(serverBroadcastListenPort)
            {
                EnableBroadcast = true,
                MulticastLoopback = false
            };

            // listen for client pings
            _ = ServerListenAsync();
        }

        public async Task ServerListenAsync()
        {
            while (true)
            {

                try
                {
                    ServerRequest request = await ReceiveRequestAsync(serverUdpClient);
                    if (request.secretHandshake == secretHandshake)
                    {
                        ReplyToClient(request);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // socket has been closed
                    break;
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

        static async Task<ServerRequest> ReceiveRequestAsync(UdpClient udpClient)
        {
            // only proceed if there is available data in network buffer, or otherwise Receive() will block
            // average time for UdpClient.Available : 10 us

            UdpReceiveResult udpReceiveResult = await udpClient.ReceiveAsync();

            ServerRequest packet = MessagePacker.Unpack<ServerRequest>(udpReceiveResult.Buffer);

            packet.EndPoint = udpReceiveResult.RemoteEndPoint;

            return packet;
        }

        private void ReplyToClient(ServerRequest request)
        {
            // a client just sent a request,  send it our info

            ServerInfo info = new ServerInfo
            {
                age = Time.time,
                secretHandshake = secretHandshake,
                totalPlayers = (ushort)NetworkServer.connections.Count,
                serverId = this.ServerId,
                port = transportPort,
            };

            byte[] data = MessagePacker.Pack(info);

            // signature matches
            // send response
            serverUdpClient.Send(data, data.Length, request.EndPoint);
        }

        #endregion


        #region Client
        // I call this when the Lobby screen is loaded in my game
        public void StartDiscovery()
        {
            if (!SupportedOnThisPlatform)
            {
                throw new PlatformNotSupportedException("Network discovery not supported in this platform");
            }

            StopDiscovery();

            try
            {
                // Setup port
                clientUdpClient = new UdpClient(0)
                {
                    EnableBroadcast = true,
                    MulticastLoopback = false
                };
            }
            catch (Exception)
            {
                // Free the port if we took it
                Shutdown();
                throw;
            }

            _ = ClientListenAsync();

            InvokeRepeating(nameof(BroadcastDiscoveryRequest), 0, ActiveDiscoverySecondInterval);

        }

        // I call this when I leave the lobby menu and in my override of NetworkManager::OnStopServer
        // Note that if plugged into a Mirrror sample the game continues being broadcast in the background
        public void StopDiscovery()
        {
            Shutdown();
        }

        public async Task ClientListenAsync()
        {
            while (true)
            {
                try
                {

                    ServerInfo info = await ReceiveGameBroadcastAsync(clientUdpClient);
                    if (info != null)
                        OnServerFound?.Invoke(info);

                }
                catch (ObjectDisposedException)
                {
                    // socket was closed, no problem
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        public void BroadcastDiscoveryRequest()
        {
            if (clientUdpClient == null)
                return;

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, serverBroadcastListenPort);

            ServerRequest request = new ServerRequest
            {
                secretHandshake = secretHandshake
            };

            byte[] packet = MessagePacker.Pack(request);

            try
            {
                clientUdpClient.SendAsync(packet, packet.Length, endPoint);
            }
            catch (Exception)
            {
                // It is ok if we can't broadcast to one of the addresses
            }
        }

        static async Task<ServerInfo> ReceiveGameBroadcastAsync(UdpClient udpClient)
        {
            // only proceed if there is available data in network buffer, or otherwise Receive() will block
            // average time for UdpClient.Available : 10 us

            UdpReceiveResult udpReceiveResult = await udpClient.ReceiveAsync();

            ServerInfo packet = MessagePacker.Unpack<ServerInfo>(udpReceiveResult.Buffer);

            packet.EndPoint = udpReceiveResult.RemoteEndPoint;

            return packet;
        }

        #endregion
    }
}
