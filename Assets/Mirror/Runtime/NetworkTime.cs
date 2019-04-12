using System;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Mirror
{
    // calculates synchronized time and rtt
    public static class NetworkTime
    {
        // how often are we sending ping messages
        // used to calculate network time and RTT
        public static float PingFrequency = 2.0f;
        // average out the last few results from Ping
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
                clientTime = msg.value,
                serverTime = LocalTime()
            };

            conn.Send(pongMsg);
        }

        // Executed at the client when we receive a Pong message
        // find out how long it took since we sent the Ping
        // and update time offset
        internal static void OnClientPong(NetworkConnection conn, NetworkPongMessage msg)
        {
            double now = LocalTime();

            // how long did this message take to come back
            double rtt = now - msg.clientTime;
            _rtt.Add(rtt);

            // the difference in time between the client and the server
            // but subtract half of the rtt to compensate for latency
            // half of rtt is the best approximation we have
            double offset = now - rtt * 0.5f - msg.serverTime;

            double newOffsetMin = now - rtt - msg.serverTime;
            double newOffsetMax = now - msg.serverTime;
            offsetMin = Math.Max(offsetMin, newOffsetMin);
            offsetMax = Math.Min(offsetMax, newOffsetMax);

            if (_offset.Value < offsetMin || _offset.Value > offsetMax)
            {
                // the old offset was offrange,  throw it away and use new one
                _offset = new ExponentialMovingAverage(PingWindowSize);
                _offset.Add(offset);
            }
            else if (offset >= offsetMin || offset <= offsetMax)
            {
                // new offset looks reasonable,  add to the average
                _offset.Add(offset);
            }
        }

        // returns the same time in both client and server
        // time should be a double because after a while
        // float loses too much accuracy if the server is up for more than
        // a few days.  I measured the accuracy of float and I got this:
        // for the same day,  accuracy is better than 1 ms
        // after 1 day,  accuracy goes down to 7 ms
        // after 10 days, accuracy is 61 ms
        // after 30 days , accuracy is 238 ms
        // after 60 days, accuracy is 454 ms
        // in other words,  if the server is running for 2 months,
        // and you cast down to float,  then the time will jump in 0.4s intervals.
        // Notice _offset is 0 at the server
        public static double time => LocalTime() - _offset.Value;

        // measure volatility of time.
        // the higher the number,  the less accurate the time is
        public static double timeVar => _offset.Var;

        // standard deviation of time
        public static double timeSd => Math.Sqrt(timeVar);

        public static double offset => _offset.Value;

        // how long does it take for a message to go
        // to the server and come back
        public static double rtt => _rtt.Value;

        // measure volatility of rtt
        // the higher the number,  the less accurate rtt is
        public static double rttVar => _rtt.Var;

        // standard deviation of rtt
        public static double rttSd => Math.Sqrt(rttVar);
    }
}
