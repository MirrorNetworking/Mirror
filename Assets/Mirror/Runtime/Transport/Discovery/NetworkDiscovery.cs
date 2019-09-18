using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Guid = System.Guid;
using UnityEngine.Profiling;
using System.Collections;
using System;
using Assets.Scripts.Utility.Serialisation;
using Assets.Scripts.NetworkMessages;

namespace Mirror
{
    // Based on https://github.com/in0finite/MirrorNetworkDiscovery (license missing, contacted in0finite confirmed MIT license)

    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkDiscovery")]
    [HelpURL("https://mirror-networking.com/xmldocs/articles/Transports/NetworkDiscovery.html")]
    public class NetworkDiscovery : MonoBehaviour
    {
        public static NetworkDiscovery instance { get; private set; }

        public static bool SupportedOnThisPlatform { get { return Application.platform != RuntimePlatform.WebGLPlayer; } }

        public static event Action<DiscoveryInfo> onReceivedServerResponse = delegate { };

        public string serverId { get; } = Guid.NewGuid().ToString();

        // Change the text to make your game distinct from any other network traffic (this is the signature)
        [HideInInspector]
        public byte[] handshakeData { get; set; } = ByteStreamer.StreamToBytes("IAmAnExperimentalTeapotWith6FacetsOfTruth");

        // This is the port the server will listen on and the port the client will multi-cast on
        [SerializeField]
        int serverBroadcastListenPort = 47777;

        // This is the frequency in seconds at which the client will multi-cast out to all network interfaces to discover a server
        [SerializeField]
        int ActiveDiscoverySecondInterval = 3;

        UdpClient serverUdpClient = null;
        UdpClient clientUdpClient = null;

        // This is a list of the NIC's found on start-up, I restart the client when the lobby screen loads and am not worried about refreshing this periodically
        IPAddress[] cachedIPs = null;

        // This is a cached copy of the current state of the servers game available for broadcast when a client calls the server
        byte[] serverBroadcastPacket;

        void Awake()
        {
            if (instance != null)
                Destroy(gameObject);

            instance = this;

            DontDestroyOnLoad(gameObject);

            // I normally only call this when the Lobby UI page is loaded but to make the sample cooler I made it start searching when you run the sample
            ClientRunActiveDiscovery();
        }

        // I call this from my NetworkManage::OnStartServer
        public bool ServerPassiveBroadcastGame(byte[] serverBroadcastPacket)
        {
            if (!SupportedOnThisPlatform) return false;

            StopDiscovery();

            try
            {
                // Setup port
                serverUdpClient = new UdpClient(serverBroadcastListenPort);
                serverUdpClient.EnableBroadcast = true;
                serverUdpClient.MulticastLoopback = false;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                // Free the port if we took it
                if (serverUdpClient != null)
                {
                    NetworkDiscoveryUtility.RunSafe(() =>
                    {
                        ShutdownUdpClients();
                    });
                }
                return false;
            }

            this.serverBroadcastPacket = serverBroadcastPacket;

            // TODO add coroutine to refresh server stats infrequently/make external update pathway for server to update broadcast packet
            StartCoroutine(ServerListenCoroutine());

            return true;
        }

        // I call this when my network manager acquires new players or other game state changes occur that I want to display in the lobby screen
        public void UpdateServerBroadcastPacket(byte[] serverBroadcastPacket)
        {
            this.serverBroadcastPacket = serverBroadcastPacket;
        }

        // I call this when the Lobby screen is loaded in my game
        public bool ClientRunActiveDiscovery()
        {
            if (!SupportedOnThisPlatform)
            {
                return false;
            }

            StopDiscovery();

            // Refresh the NIC list on entry to the lobby screen, could refresh on a timer if desired
            cachedIPs = IPAddressUtility.GetBroadcastAdresses();

            try
            {
                // Setup port
                clientUdpClient = new UdpClient(0);
                clientUdpClient.EnableBroadcast = true;
                clientUdpClient.MulticastLoopback = false;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                // Free the port if we took it
                if (clientUdpClient != null)
                {
                    NetworkDiscoveryUtility.RunSafe(() =>
                    {
                        ShutdownUdpClients();
                    });
                }
                return false;
            }

            StartCoroutine(ClientListenCoroutine());
            StartCoroutine(ClientBroadcastCoroutine());

            return true;
        }

        // I call this when I leave the lobby menu and in my override of NetworkManager::OnStopServer
        // Note that if plugged into a Mirrror sample the game continues being broadcast in the background
        public void StopDiscovery()
        {
            StopAllCoroutines();
            ShutdownUdpClients();
        }

        // Ensure the ports are cleared no matter when Game/Unity UI exits
        void OnApplicationQuit()
        {
            ShutdownUdpClients();
        }

        void ShutdownUdpClients()
        {
            if (serverUdpClient != null)
            {
                serverUdpClient.Close();
                serverUdpClient = null;
            }

            if (clientUdpClient != null)
            {
                clientUdpClient.Close();
                clientUdpClient = null;
            }
        }

        IEnumerator ServerListenCoroutine()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.3f);

                // average time for this (including data receiving and processing): less than 100 us
                Profiler.BeginSample("Receive broadcast");

                NetworkDiscoveryUtility.RunSafe(() =>
                {
                    var info = ReadDataFromUdpClient(serverUdpClient);
                    if (info != null)
                        ServerOnClientBroadcast(info);
                });

                Profiler.EndSample();
            }
        }

        void ServerOnClientBroadcast(DiscoveryInfo info)
        {
            // This is the handshake, objective is not security just sheer improbability
            if (handshakeData.Length == info.packetData.Length && handshakeData.SequenceEqual(info.packetData))
            {
                // signature matches
                // send response
                Profiler.BeginSample("Send response");
                serverUdpClient.Send(serverBroadcastPacket, serverBroadcastPacket.Length, info.EndPoint);

                Profiler.EndSample();
            }
        }

        IEnumerator ClientListenCoroutine()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.3f);

                if (clientUdpClient == null)
                    continue;

                NetworkDiscoveryUtility.RunSafe(() =>
                {
                    var info = ReadDataFromUdpClient(clientUdpClient);
                    if (info != null)
                        OnReceivedServerResponse(info);
                });
            }
        }

        IEnumerator ClientBroadcastCoroutine()
        {
            while (true)
            {
                if (clientUdpClient == null)
                    continue;

                NetworkDiscoveryUtility.RunSafe(() => { SendClientBroadcast(); });

                yield return new WaitForSecondsRealtime(ActiveDiscoverySecondInterval);
            }
        }

        void SendClientBroadcast()
        {
            // We can't just send packet to 255.255.255.255 - the OS will only broadcast it to the network interface which the socket is bound to.
            // We need to broadcast packet on every network interface.
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, serverBroadcastListenPort);

            foreach (var address in cachedIPs)
            {
                endPoint.Address = address;
                SendDiscoveryRequest(endPoint);
            }
        }

        void SendDiscoveryRequest(IPEndPoint endPoint)
        {
            Profiler.BeginSample("UdpClient.Send");
            try
            {
                clientUdpClient.SendAsync(handshakeData, handshakeData.Length, endPoint);
            }
            catch (SocketException ex)
            {
                if (ex.ErrorCode == 10051)
                {
                    // Network is unreachable
                    // ignore this error
                }
                else
                {
                    throw;
                }
            }
            Profiler.EndSample();
        }

        void OnReceivedServerResponse(DiscoveryInfo info)
        {
            // Validation is our capacity to decode the message, if the payload is so different we cant parse it we silently dump it!
            NetworkDiscoveryUtility.RunSafe(() => { info.unpackedData = (GameBroadcastPacket)ByteStreamer.StreamFromBytes(info.packetData); }, false);

            if (info.unpackedData != null)
                onReceivedServerResponse(info);
        }

        static DiscoveryInfo ReadDataFromUdpClient(UdpClient udpClient)
        {
            // only proceed if there is available data in network buffer, or otherwise Receive() will block
            // average time for UdpClient.Available : 10 us
            if (udpClient.Available <= 0)
                return null;

            Profiler.BeginSample("UdpClient.Receive");
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] receivedBytes = udpClient.Receive(ref remoteEP);
            Profiler.EndSample();

            if (remoteEP != null && receivedBytes != null && receivedBytes.Length > 0)
                return new DiscoveryInfo(remoteEP, receivedBytes);

            return null;
        }
    }
}
