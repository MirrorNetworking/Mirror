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

        // Mirror needs a way to stop kcp message processing while loop
        // immediately after a scene change message. Mirror can't process any
        // other messages during a scene change.
        // (could be useful for others too)
        public Func<bool> OnCheckEnabled = () => true;

        // state
        public KcpClientConnection connection;
        public bool connected;

        public KcpClient(Action OnConnected, Action<ArraySegment<byte>> OnData, Action OnDisconnected)
        {
            this.OnConnected = OnConnected;
            this.OnData = OnData;
            this.OnDisconnected = OnDisconnected;
        }

        public void Connect(string address, ushort port, bool noDelay, uint interval, int fastResend = 0, bool congestionWindow = true, uint sendWindowSize = Kcp.WND_SND, uint receiveWindowSize = Kcp.WND_RCV)
        {
            if (connected)
            {
                Log.Warning("KCP: client already connected!");
                return;
            }

            connection = new KcpClientConnection();

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

            // setup OnCheckEnabled to safely support Mirror
            // scene changes (see comments in Awake() above)
            connection.OnCheckEnabled = OnCheckEnabled;

            // connect
            connection.Connect(address, port, noDelay, interval, fastResend, congestionWindow, sendWindowSize, receiveWindowSize);
        }

        public void Send(ArraySegment<byte> segment)
        {
            if (connected)
            {
                connection.Send(segment);
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

        public void Tick()
        {
            // tick client connection
            if (connection != null)
            {
                // recv on socket first
                connection.RawReceive();
                // then update
                connection.Tick();
            }
        }
    }
}
