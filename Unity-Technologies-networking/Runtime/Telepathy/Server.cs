using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Telepathy
{
    public class Server : Common
    {
        // listener
        TcpListener listener;
        Thread listenerThread;

        // clients with <connectionId, TcpClient>
        SafeDictionary<uint, TcpClient> clients = new SafeDictionary<uint, TcpClient>();

        // check if the server is running
        public bool Active
        {
            get { return listenerThread != null && listenerThread.IsAlive; }
        }

        // the listener thread's listen function
        void Listen(int port)
        {
            // absolutely must wrap with try/catch, otherwise thread
            // exceptions are silent
            try
            {
                // start listener
                // (NoDelay disables nagle algorithm. lowers CPU% and latency)
                listener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
                listener.Server.NoDelay = true;
                listener.Start();
                Logger.Log("Server is listening");

                // keep accepting new clients
                while (true)
                {
                    // wait and accept new client
                    // note: 'using' sucks here because it will try to
                    // dispose after thread was started but we still need it
                    // in the thread
                    TcpClient client = listener.AcceptTcpClient();

                    // generate the next connection id (thread safely)
                    uint connectionId = counter.Next();

                    // spawn a thread for each client to listen to his
                    // messages
                    Thread thread = new Thread(() =>
                    {
                        // add to dict immediately
                        clients.Add(connectionId, client);

                        // run the receive loop
                        ReceiveLoop(connectionId, client);

                        // remove client from clients dict afterwards
                        clients.Remove(connectionId);
                    });
                    thread.IsBackground = true;
                    thread.Start();
                }
            }
            catch (ThreadAbortException exception)
            {
                // UnityEditor causes AbortException if thread is still
                // running when we press Play again next time. that's okay.
                Logger.Log("Server thread aborted. That's okay. " + exception);
            }
            catch (SocketException exception)
            {
                // calling StopServer will interrupt this thread with a
                // 'SocketException: interrupted'. that's okay.
                Logger.Log("Server Thread stopped. That's okay. " + exception);
            }
            catch (Exception exception)
            {
                // something went wrong. probably important.
                Logger.LogError("Server Exception: " + exception);
            }
        }

        // start listening for new connections in a background thread and spawn
        // a new thread for each one.
        public void Start(int port)
        {
            // not if already started
            if (Active) return;

            // clear old messages in queue, just to be sure that the caller
            // doesn't receive data from last time and gets out of sync.
            // -> calling this in Stop isn't smart because the caller may
            //    still want to process all the latest messages afterwards
            messageQueue.Clear();

            // start the listener thread
            Logger.Log("Server: starting on port=" + port);
            listenerThread = new Thread(() => { Listen(port); });
            listenerThread.IsBackground = true;
            listenerThread.Start();
        }

        public void Stop()
        {
            // only if started
            if (!Active) return;

            Logger.Log("Server: stopping...");

            // stop listening to connections so that no one can connect while we
            // close the client connections
            listener.Stop();

            // close all client connections
            List<TcpClient> connections = clients.GetValues();
            foreach (TcpClient client in connections)
            {
                // this is supposed to disconnect gracefully, but the blocking
                // Read calls throw a 'Read failure' exception in Unity
                // sometimes (instead of returning 0)
                client.GetStream().Close();
                client.Close();
            }

            // clear clients list
            clients.Clear();
        }

        // send message to client using socket connection.
        public bool Send(uint connectionId, byte[] data)
        {
            // find the connection
            TcpClient client;
            if (clients.TryGetValue(connectionId, out client))
            {
                // GetStream() might throw exception if client is disconnected
                try
                {
                    NetworkStream stream = client.GetStream();
                    return SendMessage(stream, data);
                }
                catch (Exception exception)
                {
                    Logger.LogWarning("Server.Send exception: " + exception);
                    return false;
                }
            }
            Logger.LogWarning("Server.Send: invalid connectionId: " + connectionId);
            return false;
        }
    }
}
