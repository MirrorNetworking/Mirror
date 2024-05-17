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
        /// <summary>Ping message interval, used to calculate latency / RTT and predicted time.</summary>
        // 2s was enough to get a good average RTT.
        // for prediction, we want to react to latency changes more rapidly.
        const float DefaultPingInterval = 0.1f; // for resets
        public static float PingInterval = DefaultPingInterval;

        /// <summary>Average out the last few results from Ping</summary>
        // const because it's used immediately in _rtt constructor.
        public const int PingWindowSize = 50; // average over 50 * 100ms = 5s

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
        // CAREFUL: unlike Time.time, the stopwatch time is not a FRAME time.
        //          it changes during the frame, so we have an extra step to "cache" it in EarlyUpdate.
        static readonly Stopwatch stopwatch = new Stopwatch();
        static NetworkTime() => stopwatch.Start();
        static double localFrameTime;
        public static double localTime => localFrameTime;
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

        // prediction //////////////////////////////////////////////////////////
        // NetworkTime.time is server time, behind by bufferTime.
        // for prediction, we want server time, ahead by latency.
        // so that client inputs at predictedTime=2 arrive on server at time=2.
        // the more accurate this is, the more closesly will corrections be
        // be applied and the less jitter we will see.
        //
        // we'll use a two step process to calculate predicted time:
        // 1. move snapshot interpolated time to server time, without being behind by bufferTime
        // 2. constantly send this time to server (included in ping message)
        //    server replies with how far off it was.
        //    client averages that offset and applies it to predictedTime to get ever closer.
        //
        // this is also very easy to test & verify:
        // - add LatencySimulation with 50ms latency
        // - log predictionError on server in OnServerPing, see if it gets closer to 0
        //
        // credits: FakeByte, imer, NinjaKickja, mischa
        // const because it's used immediately in _predictionError constructor.

        static int PredictionErrorWindowSize = 20; // average over 20 * 100ms = 2s
        static ExponentialMovingAverage _predictionErrorUnadjusted = new ExponentialMovingAverage(PredictionErrorWindowSize);
        public static double predictionErrorUnadjusted => _predictionErrorUnadjusted.Value;
        public static double predictionErrorAdjusted { get; private set; } // for debugging

        /// <summary>Predicted timeline in order for client inputs to be timestamped with the exact time when they will most likely arrive on the server. This is the basis for all prediction like PredictedRigidbody.</summary>
        // on client, this is based on localTime (aka Time.time) instead of the snapshot interpolated timeline.
        // this gives much better and immediately accurate results.
        // -> snapshot interpolation timeline tries to emulate a server timeline without hard offset corrections.
        // -> predictedTime does have hard offset corrections, so might as well use Time.time directly for this.
        //
        // note that predictedTime over unreliable is enough!
        // even with reliable components, it gives better results than if we were
        // to implemented predictedTime over reliable channel.
        public static double predictedTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => NetworkServer.active
                ? localTime // server always uses it's own timeline
                : localTime + predictionErrorUnadjusted; // add the offset that the server told us we are off by
        }
        ////////////////////////////////////////////////////////////////////////

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
            PingInterval = DefaultPingInterval;
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
                SendPing();
        }

        // Separate method so we can call it from NetworkClient directly.
        internal static void SendPing()
        {
            // send raw predicted time without the offset applied yet.
            // we then apply the offset to it after.
            NetworkPingMessage pingMessage = new NetworkPingMessage
            (
                localTime,
                predictedTime
            );
            NetworkClient.Send(pingMessage, Channels.Unreliable);
            lastPingTime = localTime;
        }

        // client rtt calculation //////////////////////////////////////////////
        // executed at the server when we receive a ping message
        // reply with a pong containing the time from the client
        // and time from the server
        internal static void OnServerPing(NetworkConnectionToClient conn, NetworkPingMessage message)
        {
            // calculate the prediction offset that the client needs to apply to unadjusted time to reach server time.
            // this will be sent back to client for corrections.
            double unadjustedError = localTime - message.localTime;

            // to see how well the client's final prediction worked, compare with adjusted time.
            // this is purely for debugging.
            // >0 means: server is ... seconds ahead of client's prediction (good if small)
            // <0 means: server is ... seconds behind client's prediction.
            //           in other words, client is predicting too far ahead (not good)
            double adjustedError = localTime - message.predictedTimeAdjusted;
            // Debug.Log($"[Server] unadjustedError:{(unadjustedError*1000):F1}ms adjustedError:{(adjustedError*1000):F1}ms");

            // Debug.Log($"OnServerPing conn:{conn}");
            NetworkPongMessage pongMessage = new NetworkPongMessage
            (
                message.localTime,
                unadjustedError,
                adjustedError
            );
            conn.Send(pongMessage, Channels.Unreliable);
        }

        // Executed at the client when we receive a Pong message
        // find out how long it took since we sent the Ping
        // and update time offset & prediction offset.
        internal static void OnClientPong(NetworkPongMessage message)
        {
            // prevent attackers from sending timestamps which are in the future
            if (message.localTime > localTime) return;

            // how long did this message take to come back
            double newRtt = localTime - message.localTime;
            _rtt.Add(newRtt);

            // feed unadjusted prediction error into our exponential moving average
            // store adjusted prediction error for debug / GUI purposes
            _predictionErrorUnadjusted.Add(message.predictionErrorUnadjusted);
            predictionErrorAdjusted = message.predictionErrorAdjusted;
            // Debug.Log($"[Client] predictionError avg={(_predictionErrorUnadjusted.Value*1000):F1} ms");
        }

        // server rtt calculation //////////////////////////////////////////////
        // Executed at the client when we receive a ping message from the server.
        // in other words, this is for server sided ping + rtt calculation.
        // reply with a pong containing the time from the server
        internal static void OnClientPing(NetworkPingMessage message)
        {
            // Debug.Log($"OnClientPing conn:{conn}");
            NetworkPongMessage pongMessage = new NetworkPongMessage
            (
                message.localTime,
                0, 0 // server doesn't predict
            );
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

        internal static void EarlyUpdate()
        {
#if !UNITY_2020_3_OR_NEWER
            localFrameTime = stopwatch.Elapsed.TotalSeconds;
#endif
        }
    }
}
