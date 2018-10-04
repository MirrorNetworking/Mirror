using System;
using UnityEngine;

namespace Mirror
{
    // calculates synchronized time and rtt
    public class NetworkTime 
    {
        // some arbitrary point in time where time started
        static readonly DateTime epoch = new DateTime(2018, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        static ExponentialMovingAverage _rtt = new ExponentialMovingAverage(10);
        static ExponentialMovingAverage _offset = new ExponentialMovingAverage(10);

        // returns the clock time _in this system_
        static double LocalTime()
        {
            var now = DateTime.Now;
            TimeSpan span = DateTime.Now.Subtract(epoch);
            return span.TotalSeconds;
        }

        // how often are we synchronizing the clock
        public float syncInterval = 2.0f;
        // average out the last 10 values for RTT
        public int windowSize = 10;

        public static void Reset(int windowSize)
        {
            _rtt = new ExponentialMovingAverage(windowSize);
            _offset = new ExponentialMovingAverage(windowSize);
        }

        internal static NetworkPingMessage GetPing()
        {
            return new NetworkPingMessage
            {
                clientTime = LocalTime()
            };
        }

        // executed at the server when we receive a ping message
        // reply with a pong containing the time from the client
        // and time from the server
        internal static void OnServerPing(NetworkMessage netMsg)
        {
            var pingMsg = new NetworkPingMessage();
            netMsg.ReadMessage(pingMsg);

            if (LogFilter.logDev) { Debug.Log("OnPingServerMessage  conn=" + netMsg.conn); }

            var pongMsg = new NetworkPongMessage
            {
                clientTime = pingMsg.clientTime,
                serverTime = LocalTime()
            };

            netMsg.conn.Send((short)MsgType.Pong, pongMsg);
        }

        // Executed at the client when we receive a Pong message
        // find out how long it took since we sent the Ping
        // and update time offset
        internal static void OnClientPong(NetworkMessage netMsg)
        {
            NetworkPongMessage pongMsg = new NetworkPongMessage();
            netMsg.ReadMessage(pongMsg);

            // how long did this message take to come back
            double rtt = LocalTime() - pongMsg.clientTime;
            // the difference in time between the client and the server
            // but subtract half of the rtt to compensate for latency
            // half of rtt is the best approximation we have
            double offset = LocalTime() - rtt * 0.5f - pongMsg.serverTime;

            _rtt.Add(rtt);
            _offset.Add(offset);

        }

        // returns the same time in both client and server
        public static double time
        {
            get 
            {
                // Notice _offset is 0 at the server
                return LocalTime() - _offset.Value;
            }
        }

        // measure volatility of time.  
        // the higher the number,  the less accurate the time is
        public static double timeVar
        {
            get 
            {
                return _offset.Var;
            }
        }

        // standard deviation of time
        public static double timeSd
        {
            get 
            {
                return Math.Sqrt(timeVar);
            }
        }

        public static double offset
        {
            get
            {
                return _offset.Value;
            }
        }

        // how long does it take for a message to go 
        // to the server and come back
        public static double rtt
        {
            get 
            {
                return _rtt.Value;
            }
        }

        // measure volatility of rtt
        // the higher the number,  the less accurate rtt is
        public static double rttVar
        {
            get 
            {
                return _rtt.Var;
            }
        }

        // standard deviation of rtt
        public static double rttSd
        {
            get
            {
                return Math.Sqrt(rttVar);
            }
        }
    }
}
