using System;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Mirror
{
    /// <summary>
    /// Synchronize time between the server and the clients
    /// </summary>
    public class NetworkTime
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkTime));

        /// <summary>
        /// how often are we sending ping messages
        /// used to calculate network time and RTT
        /// </summary>
        public float PingFrequency = 2.0f;

        /// <summary>
        /// average out the last few results from Ping
        /// </summary>
        public int PingWindowSize = 10;

        double lastPingTime;

        // Date and time when the application started
        private readonly Stopwatch stopwatch = new Stopwatch();

        public NetworkTime()
        {
            stopwatch.Start();
        }

        ExponentialMovingAverage _rtt = new ExponentialMovingAverage(10);
        ExponentialMovingAverage _offset = new ExponentialMovingAverage(10);
        private double _time;
        private int lastFrame;

        // the true offset guaranteed to be in this range
        double offsetMin = double.MinValue;
        double offsetMax = double.MaxValue;

        // returns the clock time _in this system_
        double LocalTime() => stopwatch.Elapsed.TotalSeconds;

        public void Reset()
        {
            _rtt = new ExponentialMovingAverage(PingWindowSize);
            _offset = new ExponentialMovingAverage(PingWindowSize);
            offsetMin = double.MinValue;
            offsetMax = double.MaxValue;
        }

        internal void UpdateClient(NetworkClient client)
        {
            if (UnityEngine.Time.time - lastPingTime >= PingFrequency)
            {
                var pingMessage = new NetworkPingMessage(LocalTime());
                client.Connection.Send(pingMessage);
                lastPingTime = UnityEngine.Time.time;
            }
        }

        // executed at the server when we receive a ping message
        // reply with a pong containing the time from the client
        // and time from the server
        internal void OnServerPing(INetworkConnection conn, NetworkPingMessage msg)
        {
            if (logger.LogEnabled()) logger.Log("OnPingServerMessage  conn=" + conn);

            var pongMsg = new NetworkPongMessage
            {
                clientTime = msg.clientTime,
                serverTime = LocalTime()
            };

            conn.Send(pongMsg);
        }

        // Executed at the client when we receive a Pong message
        // find out how long it took since we sent the Ping
        // and update time offset
        internal void OnClientPong(NetworkPongMessage msg)
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
        /// <para>Note this value works in the client and the server
        /// the value is synchronized accross the network with high accuracy</para>
        /// <para>You should not cast this down to a float because the it loses too much accuracy
        /// when the server is up for a while</para>
        /// <para>I measured the accuracy of float and I got this:</para>
        /// <list type="bullet">
        ///     <item>
        ///         <description>for the same day,  accuracy is better than 1 ms</description>
        ///     </item>
        ///     <item>
        ///         <description>after 1 day,  accuracy goes down to 7 ms</description>
        ///     </item>
        ///     <item>
        ///         <description>after 10 days, accuracy is 61 ms</description>
        ///     </item>
        ///     <item>
        ///         <description>after 30 days , accuracy is 238 ms</description>
        ///     </item>
        ///     <item>
        ///         <description>after 60 days, accuracy is 454 ms</description>
        ///     </item>
        /// </list>
        /// <para>in other words,  if the server is running for 2 months,
        /// and you cast down to float,  then the time will jump in 0.4s intervals.</para>
        /// </remarks>
        public double Time
        {
            get
            {
                // paul: LocalTime is very expensive
                // so cache the time for the duration of the frame
                // if someone asks for .time serveral times in a frame this has significant impact
                // this also makes it more consistent with Time.time
                if (lastFrame != UnityEngine.Time.frameCount)
                {
                    // Notice _offset is 0 at the server
                    _time = LocalTime() - _offset.Value;
                    lastFrame = UnityEngine.Time.frameCount;
                }
                return _time;

            }
        }

        /// <summary>
        /// Measurement of the variance of time.
        /// <para>The higher the variance, the less accurate the time is</para>
        /// </summary>
        public double TimeVar => _offset.Var;

        /// <summary>
        /// standard deviation of time.
        /// <para>The higher the variance, the less accurate the time is</para>
        /// </summary>
        public double TimeSd => Math.Sqrt(TimeVar);

        /// <summary>
        /// Clock difference in seconds between the client and the server
        /// </summary>
        /// <remarks>
        /// Note this value is always 0 at the server
        /// </remarks>
        public double Offset => _offset.Value;

        /// <summary>
        /// how long in seconds does it take for a message to go
        /// to the server and come back
        /// </summary>
        public double Rtt => _rtt.Value;

        /// <summary>
        /// measure variance of rtt
        /// the higher the number,  the less accurate rtt is
        /// </summary>
        public double RttVar => _rtt.Var;

        /// <summary>
        /// Measure the standard deviation of rtt
        /// the higher the number,  the less accurate rtt is
        /// </summary>
        public double RttSd => Math.Sqrt(RttVar);
    }
}
