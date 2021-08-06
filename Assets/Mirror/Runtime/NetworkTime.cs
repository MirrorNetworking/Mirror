using System;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Mirror
{
    /// <summary>Synchronizes server time to clients.</summary>
    public static class NetworkTime
    {
        /// <summary>Ping message frequency, used to calculate network time and RTT</summary>
        public static float PingFrequency = 2.0f;

        /// <summary>Average out the last few results from Ping</summary>
        public static int PingWindowSize = 10;

        static double lastPingTime;

        // Date and time when the application started
        // TODO Unity 2020 / 2021 supposedly has double Time.time now?
        static readonly Stopwatch stopwatch = new Stopwatch();

        static NetworkTime()
        {
            stopwatch.Start();
        }

        static ExponentialMovingAverage _rtt = new ExponentialMovingAverage(10);
        static ExponentialMovingAverage _offset = new ExponentialMovingAverage(10);

        // the true offset guaranteed to be in this range
        static double offsetMin = double.MinValue;
        static double offsetMax = double.MaxValue;

        /// <summary>Returns double precision clock time _in this system_, unaffected by the network.</summary>
        // useful until we have Unity's 'double' Time.time
        //
        // CAREFUL: unlike Time.time, this is not a FRAME time.
        //          it changes during the frame too.
        public static double localTime => stopwatch.Elapsed.TotalSeconds;

        /// <summary>The time in seconds since the server started.</summary>
        //
        // I measured the accuracy of float and I got this:
        // for the same day,  accuracy is better than 1 ms
        // after 1 day,  accuracy goes down to 7 ms
        // after 10 days, accuracy is 61 ms
        // after 30 days , accuracy is 238 ms
        // after 60 days, accuracy is 454 ms
        // in other words,  if the server is running for 2 months,
        // and you cast down to float,  then the time will jump in 0.4s intervals.
        //
        // TODO consider using Unbatcher's remoteTime for NetworkTime
        public static double time => localTime - _offset.Value;

        /// <summary>Time measurement variance. The higher, the less accurate the time is.</summary>
        // TODO does this need to be public? user should only need NetworkTime.time
        public static double timeVariance => _offset.Var;

        // Deprecated 2021-03-10
        [Obsolete("NetworkTime.timeVar was renamed to timeVariance")]
        public static double timeVar => timeVariance;

        /// <summary>Time standard deviation. The highe, the less accurate the time is.</summary>
        // TODO does this need to be public? user should only need NetworkTime.time
        public static double timeStandardDeviation => Math.Sqrt(timeVariance);

        // Deprecated 2021-03-10
        [Obsolete("NetworkTime.timeSd was renamed to timeStandardDeviation")]
        public static double timeSd => timeStandardDeviation;

        /// <summary>Clock difference in seconds between the client and the server. Always 0 on server.</summary>
        public static double offset => _offset.Value;

        /// <summary>Round trip time (in seconds) that it takes a message to go client->server->client.</summary>
        public static double rtt => _rtt.Value;

        /// <summary>Round trip time variance. The higher, the less accurate the rtt is.</summary>
        // TODO does this need to be public? user should only need NetworkTime.time
        public static double rttVariance => _rtt.Var;

        // Deprecated 2021-03-02
        [Obsolete("NetworkTime.rttVar was renamed to rttVariance")]
        public static double rttVar => rttVariance;

        /// <summary>Round trip time standard deviation. The higher, the less accurate the rtt is.</summary>
        // TODO does this need to be public? user should only need NetworkTime.time
        public static double rttStandardDeviation => Math.Sqrt(rttVariance);

        // Deprecated 2021-03-02
        [Obsolete("NetworkTime.rttSd was renamed to rttStandardDeviation")]
        public static double rttSd => rttStandardDeviation;

        public static void Reset()
        {
            stopwatch.Restart();
            _rtt = new ExponentialMovingAverage(PingWindowSize);
            _offset = new ExponentialMovingAverage(PingWindowSize);
            offsetMin = double.MinValue;
            offsetMax = double.MaxValue;
            lastPingTime = 0;
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
        internal static void OnServerPing(NetworkConnection conn, NetworkPingMessage message)
        {
            // Debug.Log("OnPingServerMessage  conn=" + conn);
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
            double now = localTime;

            // how long did this message take to come back
            double newRtt = now - message.clientTime;
            _rtt.Add(newRtt);

            // the difference in time between the client and the server
            // but subtract half of the rtt to compensate for latency
            // half of rtt is the best approximation we have
            double newOffset = now - newRtt * 0.5f - message.serverTime;

            double newOffsetMin = now - newRtt - message.serverTime;
            double newOffsetMax = now - message.serverTime;
            offsetMin = Math.Max(offsetMin, newOffsetMin);
            offsetMax = Math.Min(offsetMax, newOffsetMax);

            if (_offset.Value < offsetMin || _offset.Value > offsetMax)
            {
                // the old offset was offrange,  throw it away and use new one
                _offset = new ExponentialMovingAverage(PingWindowSize);
                _offset.Add(newOffset);
            }
            else if (newOffset >= offsetMin || newOffset <= offsetMax)
            {
                // new offset looks reasonable,  add to the average
                _offset.Add(newOffset);
            }
        }
    }
}
