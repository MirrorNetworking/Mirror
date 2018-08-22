using System;
using System.Net.Sockets;
using System.Threading;

namespace Telepathy
{
    public class Client : Common
    {
        TcpClient client;

        public bool Connected
        {
            get
            {
                // TcpClient.Connected doesn't check if socket != null, which
                // results in NullReferenceExceptions if connection was closed.
                // -> let's check it manually instead
                return client != null &&
                       client.Client != null &&
                       client.Client.Connected;
            }
        }

        public bool Connecting
        {
            // client was created by Connect() call but not fully connected yet?
            get { return client != null && !Connected; }
        }

        // the thread function
        // (static to reduce state for maximum reliability)
        static void ThreadFunction(TcpClient client, string ip, int port, SafeQueue<Message> messageQueue)
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
                ReceiveLoop(0, client, messageQueue);
            }
            catch (SocketException exception)
            {
                // this happens if (for example) the ip address is correct
                // but there is no server running on that ip/port
                Logger.Log("Client: failed to connect to ip=" + ip + " port=" + port + " reason=" + exception);

                // add 'Disconnected' event to message queue so that the caller
                // knows that the Connect failed. otherwise they will never know
                messageQueue.Enqueue(new Message(0, EventType.Disconnected, null));
            }
            catch (Exception exception)
            {
                // something went wrong. probably important.
                Logger.LogError("Client Exception: " + exception);
            }

            // if we got here then we are done. ReceiveLoop cleans up already,
            // but we may never get there if connect fails. so let's clean up
            // here too.
            client.Close();
        }

        public void Connect(string ip, int port)
        {
            // not if already started
            if (Connecting || Connected) return;

            // TcpClient can only be used once. need to create a new one each
            // time.
            client = new TcpClient();

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
            Thread thread = new Thread(() => { ThreadFunction(client, ip, port, messageQueue); });
            thread.IsBackground = true;
            thread.Start();
        }

        public void Disconnect()
        {
            // only if started
            if (Connecting || Connected)
            {
                // close client, ThreadFunc will end and clean up
                client.Close();

                // clear client reference so that we can call Connect again
                // immediately after calling Disconnect.
                // -> this client's thread will end in the background in a few
                //    milliseconds, we don't need to worry about it anymore
                // -> setting it null here won't set it null in ThreadFunction,
                //    because it's static and we pass a reference. so there
                //    won't be any NullReferenceExceptions. the thread will just
                //    end gracefully.
                client = null;

                Logger.Log("Client: disconnected");
            }
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
