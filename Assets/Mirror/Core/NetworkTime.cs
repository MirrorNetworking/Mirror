// NetworkTime now uses NetworkClient's snapshot interpolated timeline.
// this gives ideal results & ensures everything is on the same timeline.
// previously, NetworkTransforms were on separate timelines.
//
// however, some of the old NetworkTime code remains for ping time (rtt).
// some users may still be using that.
using System.Runtime.CompilerServices;
using UnityEngine;
#if !UNITY_2020_3_OR_NEWER
using Stopwatch = System.Diagnostics.Stopwatch;
#endif

namespace Mirror
{
    /// <summary>Synchronizes server time to clients.</summary>
    public static class NetworkTime
    {
        /// <summary>Ping message frequency, used to calculate network time and RTT</summary>
        public static float PingFrequency = 2;

        /// <summary>Average out the last few results from Ping</summary>
        public static int PingWindowSize = 10;

        static double lastPingTime;

        static ExponentialMovingAverage _rtt = new ExponentialMovingAverage(10);

        /// <summary>Returns double precision clock time _in this system_, unaffected by the network.</summary>
#if UNITY_2020_3_OR_NEWER
        public static double localTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Time.timeAsDouble;
        }
#else
        // need stopwatch for older Unity versions, but it's quite slow.
        // CAREFUL: unlike Time.time, this is not a FRAME time.
        //          it changes during the frame too.
        static readonly Stopwatch stopwatch = new Stopwatch();
        static NetworkTime() => stopwatch.Start();
        public static double localTime => stopwatch.Elapsed.TotalSeconds;
#endif

        /// <summary>The time in seconds since the server started.</summary>
        // via global NetworkClient snapshot interpolated timeline (if client).
        // on server, this is simply Time.timeAsDouble.
        //
        // I measured the accuracy of float and I got this:
        // for the same day,  accuracy is better than 1 ms
        // after 1 day,  accuracy goes down to 7 ms
        // after 10 days, accuracy is 61 ms
        // after 30 days , accuracy is 238 ms
        // after 60 days, accuracy is 454 ms
        // in other words,  if the server is running for 2 months,
        // and you cast down to float,  then the time will jump in 0.4s intervals.
        public static double time
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => NetworkServer.active
                ? localTime
                : NetworkClient.localTimeline;
        }

        /// <summary>Clock difference in seconds between the client and the server. Always 0 on server.</summary>
        // original implementation used 'client - server' time. keep it this way.
        // TODO obsolete later. people shouldn't worry about this.
        public static double offset => localTime - time;

        /// <summary>Round trip time (in seconds) that it takes a message to go client->server->client.</summary>
        public static double rtt => _rtt.Value;

        // RuntimeInitializeOnLoadMethod -> fast playmode without domain reload
        [RuntimeInitializeOnLoadMethod]
        public static void ResetStatics()
        {
            PingFrequency = 2;
            PingWindowSize = 10;
            lastPingTime = 0;
            _rtt = new ExponentialMovingAverage(PingWindowSize);
#if !UNITY_2020_3_OR_NEWER
            stopwatch.Restart();
#endif
        }

        internal static void UpdateClient()
        {
            // localTime (double) instead of Time.time for accuracy over days
            if (localTime - lastPingTime >= PingFrequency)
            {
                NetworkPingMessage pingMessage = new NetworkPingMessage(localTime);
                NetworkClient.Send(pingMessage, Channels.Unreliable);
                lastPingTime = localTime;
            }
        }

        // executed at the server when we receive a ping message
        // reply with a pong containing the time from the client
        // and time from the server
        internal static void OnServerPing(NetworkConnectionToClient conn, NetworkPingMessage message)
        {
            // Debug.Log($"OnPingServerMessage conn:{conn}");
            NetworkPongMessage pongMessage = new NetworkPongMessage
            {
                clientTime = message.clientTime,
                serverTime = localTime
            };
            conn.Send(pongMessage, Channels.Unreliable);
        }

        // Executed at the client when we receive a Pong message
        // find out how long it took since we sent the Ping
        // and update time offset
        internal static void OnClientPong(NetworkPongMessage message)
        {
            // how long did this message take to come back
            double newRtt = localTime - message.clientTime;
            _rtt.Add(newRtt);
        }
    }
}
