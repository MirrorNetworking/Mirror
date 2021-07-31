// kcp client logic abstracted into a class.
// for use in Mirror, DOTSNET, testing, etc.
using System;

namespace kcp2k
{
    public class KcpClient
    {
        // events
        public Action OnConnected;
        public Action<ArraySegment<byte>> OnData;
        public Action OnDisconnected;

        // state
        public KcpClientConnection connection;
        public bool connected;

        public KcpClient(Action OnConnected, Action<ArraySegment<byte>> OnData, Action OnDisconnected)
        {
            this.OnConnected = OnConnected;
            this.OnData = OnData;
            this.OnDisconnected = OnDisconnected;
        }

        // CreateConnection can be overwritten for where-allocation:
        // https://github.com/vis2k/where-allocation
        protected virtual KcpClientConnection CreateConnection() =>
            new KcpClientConnection();

        public void Connect(string address, ushort port, bool noDelay, uint interval, int fastResend = 0, bool congestionWindow = true, uint sendWindowSize = Kcp.WND_SND, uint receiveWindowSize = Kcp.WND_RCV, int timeout = KcpConnection.DEFAULT_TIMEOUT)
        {
            if (connected)
            {
                Log.Warning("KCP: client already connected!");
                return;
            }

            // create connection
            connection = CreateConnection();

            // setup events
            connection.OnAuthenticated = () =>
            {
                Log.Info($"KCP: OnClientConnected");
                connected = true;
                OnConnected.Invoke();
            };
            connection.OnData = (message) =>
            {
                //Log.Debug($"KCP: OnClientData({BitConverter.ToString(message.Array, message.Offset, message.Count)})");
                OnData.Invoke(message);
            };
            connection.OnDisconnected = () =>
            {
                Log.Info($"KCP: OnClientDisconnected");
                connected = false;
                connection = null;
                OnDisconnected.Invoke();
            };

            // connect
            connection.Connect(address, port, noDelay, interval, fastResend, congestionWindow, sendWindowSize, receiveWindowSize, timeout);
        }

        public void Send(ArraySegment<byte> segment, KcpChannel channel)
        {
            if (connected)
            {
                connection.SendData(segment, channel);
            }
            else Log.Warning("KCP: can't send because client not connected!");
        }

        public void Disconnect()
        {
            // only if connected
            // otherwise we end up in a deadlock because of an open Mirror bug:
            // https://github.com/vis2k/Mirror/issues/2353
            if (connected)
            {
                // call Disconnect and let the connection handle it.
                // DO NOT set it to null yet. it needs to be updated a few more
                // times first. let the connection handle it!
                connection?.Disconnect();
            }
        }

        // process incoming messages. should be called before updating the world.
        public void TickIncoming()
        {
            // recv on socket first, then process incoming
            // (even if we didn't receive anything. need to tick ping etc.)
            // (connection is null if not active)
            connection?.RawReceive();
            connection?.TickIncoming();
        }

        // process outgoing messages. should be called after updating the world.
        public void TickOutgoing()
        {
            // process outgoing
            // (connection is null if not active)
            connection?.TickOutgoing();
        }

        // process incoming and outgoing for convenience
        // => ideally call ProcessIncoming() before updating the world and
        //    ProcessOutgoing() after updating the world for minimum latency
        public void Tick()
        {
            TickIncoming();
            TickOutgoing();
        }

        // pause/unpause to safely support mirror scene handling and to
        // immediately pause the receive while loop if needed.
        public void Pause() => connection?.Pause();
        public void Unpause() => connection?.Unpause();
    }
}
