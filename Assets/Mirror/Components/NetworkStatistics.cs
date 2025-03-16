using System;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Shows Network messages and bytes sent and received per second.
    /// </summary>
    /// <remarks>
    /// <para>Add this component to the same object as Network Manager.</para>
    /// </remarks>
    [AddComponentMenu("Network/Network Statistics")]
    [DisallowMultipleComponent]
    [HelpURL("https://mirror-networking.gitbook.io/docs/components/network-statistics")]
    public class NetworkStatistics : MonoBehaviour
    {
        // update interval
        double intervalStartTime;

        // ---------------------------------------------------------------------

        // CLIENT (public fields for other components to grab statistics)
        // long bytes to support >2GB
        [HideInInspector] public int  clientIntervalReceivedPackets;
        [HideInInspector] public long clientIntervalReceivedBytes;
        [HideInInspector] public int  clientIntervalSentPackets;
        [HideInInspector] public long clientIntervalSentBytes;

        // results from last interval
        // long bytes to support >2GB
        [HideInInspector] public int  clientReceivedPacketsPerSecond;
        [HideInInspector] public long clientReceivedBytesPerSecond;
        [HideInInspector] public int  clientSentPacketsPerSecond;
        [HideInInspector] public long clientSentBytesPerSecond;

        // ---------------------------------------------------------------------

        // SERVER (public fields for other components to grab statistics)
        // capture interval
        // long bytes to support >2GB
        [HideInInspector] public int  serverIntervalReceivedPackets;
        [HideInInspector] public long serverIntervalReceivedBytes;
        [HideInInspector] public int  serverIntervalSentPackets;
        [HideInInspector] public long serverIntervalSentBytes;

        // results from last interval
        // long bytes to support >2GB
        [HideInInspector] public int  serverReceivedPacketsPerSecond;
        [HideInInspector] public long serverReceivedBytesPerSecond;
        [HideInInspector] public int  serverSentPacketsPerSecond;
        [HideInInspector] public long serverSentBytesPerSecond;

        // NetworkManager sets Transport.active in Awake().
        // so let's hook into it in Start().
        void Start()
        {
            // find available transport
            Transport transport = Transport.active;
            if (transport != null)
            {
                transport.OnClientDataReceived += OnClientReceive;
                transport.OnClientDataSent += OnClientSend;
                transport.OnServerDataReceived += OnServerReceive;
                transport.OnServerDataSent += OnServerSend;
            }
            else Debug.LogError($"NetworkStatistics: no available or active Transport found on this platform: {Application.platform}");
        }

        void OnDestroy()
        {
            // remove transport hooks
            Transport transport = Transport.active;
            if (transport != null)
            {
                transport.OnClientDataReceived -= OnClientReceive;
                transport.OnClientDataSent -= OnClientSend;
                transport.OnServerDataReceived -= OnServerReceive;
                transport.OnServerDataSent -= OnServerSend;
            }
        }

        void OnClientReceive(ArraySegment<byte> data, int channelId)
        {
            ++clientIntervalReceivedPackets;
            clientIntervalReceivedBytes += data.Count;
        }

        void OnClientSend(ArraySegment<byte> data, int channelId)
        {
            ++clientIntervalSentPackets;
            clientIntervalSentBytes += data.Count;
        }

        void OnServerReceive(int connectionId, ArraySegment<byte> data, int channelId)
        {
            ++serverIntervalReceivedPackets;
            serverIntervalReceivedBytes += data.Count;
        }

        void OnServerSend(int connectionId, ArraySegment<byte> data, int channelId)
        {
            ++serverIntervalSentPackets;
            serverIntervalSentBytes += data.Count;
        }

        void Update()
        {
            // calculate results every second
            if (NetworkTime.localTime >= intervalStartTime + 1)
            {
                if (NetworkClient.active) UpdateClient();
                if (NetworkServer.active) UpdateServer();

                intervalStartTime = NetworkTime.localTime;
            }
        }

        void UpdateClient()
        {
            clientReceivedPacketsPerSecond = clientIntervalReceivedPackets;
            clientReceivedBytesPerSecond = clientIntervalReceivedBytes;
            clientSentPacketsPerSecond = clientIntervalSentPackets;
            clientSentBytesPerSecond = clientIntervalSentBytes;

            clientIntervalReceivedPackets = 0;
            clientIntervalReceivedBytes = 0;
            clientIntervalSentPackets = 0;
            clientIntervalSentBytes = 0;
        }

        void UpdateServer()
        {
            serverReceivedPacketsPerSecond = serverIntervalReceivedPackets;
            serverReceivedBytesPerSecond = serverIntervalReceivedBytes;
            serverSentPacketsPerSecond = serverIntervalSentPackets;
            serverSentBytesPerSecond = serverIntervalSentBytes;

            serverIntervalReceivedPackets = 0;
            serverIntervalReceivedBytes = 0;
            serverIntervalSentPackets = 0;
            serverIntervalSentBytes = 0;
        }

#if !UNITY_SERVER
        void OnGUI()
        {
            // only show if either server or client active
            if (NetworkClient.active || NetworkServer.active)
            {
                // create main GUI area
                // 120 is below NetworkManager HUD in all cases.
                GUILayout.BeginArea(new Rect(10, 120, 215, 300));

                // show client / server stats if active
                if (NetworkClient.active) OnClientGUI();
                if (NetworkServer.active) OnServerGUI();

                // end of GUI area
                GUILayout.EndArea();
            }
        }

        void OnClientGUI()
        {
            // background
            GUILayout.BeginVertical("Box");
            GUILayout.Label("<b>Client Statistics</b>");

            // sending ("msgs" instead of "packets" to fit larger numbers)
            GUILayout.Label($"Send: {clientSentPacketsPerSecond} msgs @ {Utils.PrettyBytes(clientSentBytesPerSecond)}/s");

            // receiving ("msgs" instead of "packets" to fit larger numbers)
            GUILayout.Label($"Recv: {clientReceivedPacketsPerSecond} msgs @ {Utils.PrettyBytes(clientReceivedBytesPerSecond)}/s");

            // end background
            GUILayout.EndVertical();
        }

        void OnServerGUI()
        {
            // background
            GUILayout.BeginVertical("Box");
            GUILayout.Label("<b>Server Statistics</b>");

            // sending ("msgs" instead of "packets" to fit larger numbers)
            GUILayout.Label($"Send: {serverSentPacketsPerSecond} msgs @ {Utils.PrettyBytes(serverSentBytesPerSecond)}/s");

            // receiving ("msgs" instead of "packets" to fit larger numbers)
            GUILayout.Label($"Recv: {serverReceivedPacketsPerSecond} msgs @ {Utils.PrettyBytes(serverReceivedBytesPerSecond)}/s");

            // end background
            GUILayout.EndVertical();
        }
#endif
    }
}
