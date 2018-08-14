using System;
using System.Net.Sockets;
using System.Threading;

namespace Telepathy
{
    public class Client : Common
    {
        TcpClient client = new TcpClient();
        Thread thread;

        public bool Connecting
        {
            get { return thread != null && thread.IsAlive && !client.Connected; }
        }

        public bool Connected
        {
            get { return thread != null && thread.IsAlive && client.Connected; }
        }

        // the thread function
        void ThreadFunction(string ip, int port)
        {
            // absolutely must wrap with try/catch, otherwise thread
            // exceptions are silent
            try
            {
                // connect (blocking)
                // (NoDelay disables nagle algorithm. lowers CPU% and latency)
                client.NoDelay = true;
                client.Connect(ip, port);

                // run the receive loop
                ReceiveLoop(0, client);
            }
            catch (SocketException exception)
            {
                // this happens if (for example) the ip address is correct
                // but there is no server running on that ip/port
                Logger.Log("Client: failed to connect to ip=" + ip + " port=" + port + " reason=" + exception);

                // clean up properly before exiting
                client.Close();
            }
            catch (Exception exception)
            {
                // something went wrong. probably important.
                Logger.LogError("Client Exception: " + exception);
            }
        }

        public void Connect(string ip, int port)
        {
            // not if already started
            if (Connecting || Connected) return;

            // clear old messages in queue, just to be sure that the caller
            // doesn't receive data from last time and gets out of sync.
            // -> calling this in Disconnect isn't smart because the caller may
            //    still want to process all the latest messages afterwards
            messageQueue.Clear();

            // client.Connect(ip, port) is blocking. let's call it in the thread
            // and return immediately.
            // -> this way the application doesn't hang for 30s if connect takes
            //    too long, which is especially good in games
            // -> this way we don't async client.BeginConnect, which seems to
            //    fail sometimes if we connect too many clients too fast
            thread = new Thread(() => { ThreadFunction(ip, port); });
            thread.IsBackground = true;
            thread.Start();
        }

        public void Disconnect()
        {
            // only if started
            if (!Connecting && !Connected) return;

            Logger.Log("Client: disconnecting");

            // this is supposed to disconnect gracefully, but the blocking Read
            // calls throw a 'Read failure' exception instead of returning 0.
            // (maybe it's Unity? maybe Mono?)
            client.GetStream().Close();
            client.Close();
        }

        public bool Send(byte[] data)
        {
            if (Connected)
            {
                return SendMessage(client.GetStream(), data);
            }
            Logger.LogWarning("Client.Send: not connected!");
            return false;
        }
    }
}
