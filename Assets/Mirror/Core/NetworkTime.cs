// NetworkTime now uses NetworkClient's snapshot interpolated timeline.
// this gives ideal results & ensures everything is on the same timeline.
// previously, NetworkTransforms were on separate timelines.
//
// however, some of the old NetworkTime code remains for ping time (rtt).
// some users may still be using that.
using System;
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
        /// <summary>Ping message interval, used to calculate network time and RTT</summary>
        public static float PingInterval = 2;

        // DEPRECATED 2023-07-06
        [Obsolete("NetworkTime.PingFrequency was renamed to PingInterval, because we use it as seconds, not as Hz. Please rename all usages, but keep using it just as before.")]
        public static float PingFrequency
        {
            get => PingInterval;
            set => PingInterval = value;
        }

        /// <summary>Average out the last few results from Ping</summary>
        public static int PingWindowSize = 6;

        static double lastPingTime;

        static ExponentialMovingAverage _rtt = new ExponentialMovingAverage(PingWindowSize);

        /// <summary>Returns double precision clock time _in this system_, unaffected by the network.</summary>
#if UNITY_2020_3_OR_NEWER
        public static double localTime
        {
            // NetworkTime uses unscaled time and ignores Time.timeScale.
            // fixes Time.timeScale getting server & client time out of sync:
            // https://github.com/MirrorNetworking/Mirror/issues/3409
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Time.unscaledTimeAsDouble;
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

        /// <Summary>Round trip time variance aka jitter, in seconds.</Summary>
        // "rttVariance" instead of "rttVar" for consistency with older versions.
        public static double rttVariance => _rtt.Variance;

        // RuntimeInitializeOnLoadMethod -> fast playmode without domain reload
        [RuntimeInitializeOnLoadMethod]
        public static void ResetStatics()
        {
            PingInterval = 2;
            PingWindowSize = 6;
            lastPingTime = 0;
            _rtt = new ExponentialMovingAverage(PingWindowSize);
#if !UNITY_2020_3_OR_NEWER
            stopwatch.Restart();
#endif
        }

        internal static void UpdateClient()
        {
            // localTime (double) instead of Time.time for accuracy over days
            if (localTime >= lastPingTime + PingInterval)
            {
                NetworkPingMessage pingMessage = new NetworkPingMessage(localTime);
                NetworkClient.Send(pingMessage, Channels.Unreliable);
                lastPingTime = localTime;
            }
        }

        // client rtt calculation //////////////////////////////////////////////
        // executed at the server when we receive a ping message
        // reply with a pong containing the time from the client
        // and time from the server
        internal static void OnServerPing(NetworkConnectionToClient conn, NetworkPingMessage message)
        {
            // Debug.Log($"OnServerPing conn:{conn}");
            NetworkPongMessage pongMessage = new NetworkPongMessage
            {
                localTime = message.localTime,
            };
            conn.Send(pongMessage, Channels.Unreliable);
        }

        // Executed at the client when we receive a Pong message
        // find out how long it took since we sent the Ping
        // and update time offset
        internal static void OnClientPong(NetworkPongMessage message)
        {
            // prevent attackers from sending timestamps which are in the future
            if (message.localTime > localTime) return;

            // how long did this message take to come back
            double newRtt = localTime - message.localTime;
            _rtt.Add(newRtt);
        }

        // server rtt calculation //////////////////////////////////////////////
        // Executed at the client when we receive a ping message from the server.
        // in other words, this is for server sided ping + rtt calculation.
        // reply with a pong containing the time from the server
        internal static void OnClientPing(NetworkPingMessage message)
        {
            // Debug.Log($"OnClientPing conn:{conn}");
            NetworkPongMessage pongMessage = new NetworkPongMessage
            {
                localTime = message.localTime,
            };
            NetworkClient.Send(pongMessage, Channels.Unreliable);
        }

        // Executed at the server when we receive a Pong message back.
        // find out how long it took since we sent the Ping
        // and update time offset
        internal static void OnServerPong(NetworkConnectionToClient conn, NetworkPongMessage message)
        {
            // prevent attackers from sending timestamps which are in the future
            if (message.localTime > localTime) return;

            // how long did this message take to come back
            double newRtt = localTime - message.localTime;
            conn._rtt.Add(newRtt);
        }
    }
}
