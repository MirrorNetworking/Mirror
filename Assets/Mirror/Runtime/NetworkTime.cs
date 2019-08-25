using System;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Mirror
{
    /// <summary>
    /// Synchronize time between the server and the clients
    /// </summary>
    public static class NetworkTime
    {
        /// <summary>
        /// how often are we sending ping messages
        /// used to calculate network time and RTT
        /// </summary>
        public static float PingFrequency = 2.0f;

        /// <summary>
        /// average out the last few results from Ping
        /// </summary>
        public static int PingWindowSize = 10;

        static double lastPingTime;


        // Date and time when the application started
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

        // returns the clock time _in this system_
        static double LocalTime()
        {
            return stopwatch.Elapsed.TotalSeconds;
        }

        public static void Reset()
        {
            _rtt = new ExponentialMovingAverage(PingWindowSize);
            _offset = new ExponentialMovingAverage(PingWindowSize);
            offsetMin = double.MinValue;
            offsetMax = double.MaxValue;
        }

        internal static void UpdateClient()
        {
            if (Time.time - lastPingTime >= PingFrequency)
            {
                NetworkPingMessage pingMessage = new NetworkPingMessage(LocalTime());
                NetworkClient.Send(pingMessage);
                lastPingTime = Time.time;
            }
        }

        // executed at the server when we receive a ping message
        // reply with a pong containing the time from the client
        // and time from the server
        internal static void OnServerPing(NetworkConnection conn, NetworkPingMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("OnPingServerMessage  conn=" + conn);

            NetworkPongMessage pongMsg = new NetworkPongMessage
            {
                clientTime = msg.clientTime,
                serverTime = LocalTime()
            };

            conn.Send(pongMsg);
        }

        // Executed at the client when we receive a Pong message
        // find out how long it took since we sent the Ping
        // and update time offset
        internal static void OnClientPong(NetworkConnection _, NetworkPongMessage msg)
        {
            double now = LocalTime();

            // how long did this message take to come back
            double newRtt = now - msg.clientTime;
            _rtt.Add(newRtt);

            // the difference in time between the client and the server
            // but subtract half of the rtt to compensate for latency
            // half of rtt is the best approximation we have
            double newOffset = now - newRtt * 0.5f - msg.serverTime;

            double newOffsetMin = now - newRtt - msg.serverTime;
            double newOffsetMax = now - msg.serverTime;
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

        /// <summary>
        /// The time in seconds since the server started.
        /// </summary>
        /// <remarks>
        /// 
        /// <para>Note this value works in the client and the server
        /// the value is synchronized accross the network with high accuracy</para>
        ///
        /// <para>You should not cast this down to a float because the it loses too much accuracy
        /// when the server is up for a while</para>
        /// <para>I measured the accuracy of float and I got this:</para>
        /// <list type="bullet">
        /// <item>for the same day,  accuracy is better than 1 ms</item>
        /// <item>after 1 day,  accuracy goes down to 7 ms</item>
        /// <item>after 10 days, accuracy is 61 ms</item>
        /// <item>after 30 days , accuracy is 238 ms</item>
        /// <item>after 60 days, accuracy is 454 ms</item>
        /// </list>
        /// 
        /// <para>in other words,  if the server is running for 2 months,
        /// and you cast down to float,  then the time will jump in 0.4s intervals.</para>
        /// </remarks>
        public static double time => LocalTime() - _offset.Value;

        /// <summary>
        /// Measurement of the variance of time.
        /// <para>The higher the variance, the less accurate the time is</para>
        /// </summary>
        public static double timeVar => _offset.Var;

        /// <summary>
        /// standard deviation of time.
        /// <para>The higher the variance, the less accurate the time is</para>
        /// </summary>
        public static double timeSd => Math.Sqrt(timeVar);

        /// <summary>
        /// Clock difference in seconds between the client and the server
        /// </summary>
        /// <remarks>
        /// Note this value is always 0 at the server
        /// </remarks>
        public static double offset => _offset.Value;

        /// <summary>
        /// how long in seconds does it take for a message to go
        /// to the server and come back
        /// </summary>
        public static double rtt => _rtt.Value;

        /// <summary>
        /// measure variance of rtt
        /// the higher the number,  the less accurate rtt is
        /// </summary>
        public static double rttVar => _rtt.Var;

        /// <summary>
        /// Measure the standard deviation of rtt
        /// the higher the number,  the less accurate rtt is
        /// </summary>
        public static double rttSd => Math.Sqrt(rttVar);
    }
}
