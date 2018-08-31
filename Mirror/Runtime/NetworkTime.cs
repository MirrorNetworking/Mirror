using System;
using UnityEngine;

namespace Mirror
{
    // calculates synchronized time and rtt
    public class NetworkTime : NetworkBehaviour
    {
        // some arbitrary point in time where time started
        private static readonly DateTime epoch = new DateTime(2018, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        private static ExponentialMovingAverage _rtt = new ExponentialMovingAverage(10);
        private static ExponentialMovingAverage _offset = new ExponentialMovingAverage(10);

        // returns the clock time _in this system_
        private static double LocalTime()
        {
            var now = DateTime.Now;
            TimeSpan span = DateTime.Now.Subtract(epoch);
            return span.TotalSeconds;
        }

        // how often are we synchronizing the clock
        public float syncInterval = 2.0f;
        // average out the last 10 values for RTT
        public int windowSize = 10;

        public override void OnStartClient()
        {

            if (!isServer)
            {
                NetworkManager.singleton.client.RegisterHandler((short)MsgType.Pong, OnNetworkPongClientMessage);
                _rtt = new ExponentialMovingAverage(windowSize);
                _offset = new ExponentialMovingAverage(windowSize);
                CancelInvoke("SendPing");
                InvokeRepeating("SendPing", 0, syncInterval);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
           
            NetworkServer.RegisterHandler((short)MsgType.Ping, OnNetworkPingServerMessage);

        }

        private void SendPing()
        {
            var pingMsg = new NetworkPingMessage
            {
                clientTime = LocalTime()
            };

            if (NetworkManager.singleton.client != null)
            {
                NetworkManager.singleton.client.Send((short)MsgType.Ping, pingMsg);
            }
        }

        // The server receives a time from the client
        // it replies with the same time plus the server time
        public void OnNetworkPingServerMessage(NetworkMessage netMsg)
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

        // we received the response to our ping message
        // Update round trip time and time offset
        public void OnNetworkPongClientMessage(NetworkMessage netMsg)
        {
            var pongMsg = new NetworkPongMessage();
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
            get {
                // Notice _offset is 0 at the server
                return LocalTime() - _offset.Value;
            }
        }

        // measure volatility of time.  
        // the higher the number,  the less accurate the time is
        public static double timeVar
        {
            get {
                return _offset.Var;
            }
        }

        // standard deviation of time
        public static double timeSd
        {
            get {
                return Math.Sqrt(timeVar);
            }
        }

        public static double offset
        {
            get {
                return _offset.Value;
            }
        }

        // how long does it take for a message to go 
        // to the server and come back
        public static double rtt
        {
            get {
                return _rtt.Value;
            }
        }

        // measure volatility of rtt
        // the higher the number,  the less accurate rtt is
        public static double rttVar
        {
            get {
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
