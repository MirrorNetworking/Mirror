using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;

namespace Telepathy
{
    public class Client : Common
    {
        /// <summary>
        /// We need an instance to keep track of the current TcpClient
        /// <para>
        /// If the Client is disconnected while in TcpClient.Connect the thread 
        /// will not be stoped until the Connect either is sucess or fail.
        /// If another client is created to connect before the old one is finished
        /// then the old one will close the new one.
        /// </para>
        /// </summary>
        class TcpInstance
        {
            public TcpClient client;

            // TcpClient has no 'connecting' state to check. We need to keep track
            // of it manually.
            // -> checking 'thread.IsAlive && !Connected' is not enough because the
            //    thread is alive and connected is false for a short moment after
            //    disconnecting, so this would cause race conditions.
            // -> we use a threadsafe bool wrapper so that ThreadFunction can remain
            //    static (it needs a common lock)
            // => Connecting is true from first Connect() call in here, through the
            //    thread start, until TcpClient.Connect() returns. Simple and clear.
            // => bools are atomic according to
            //    https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/variables
            //    made volatile so the compiler does not reorder access to it
            public volatile bool Connecting;

            public Thread sendThread;
            public Thread receiveThread;

            /// <summary>
            /// Set this to true to interrupt the thread after a SocketException
            /// <para>Avoids the need to sleep to check for Thread.Interrupt();</para>
            /// </summary>
            public bool interrupt;
        }

        /// <summary>
        /// Current instance of TcpClient
        /// </summary>
        TcpInstance instance;
        public TcpClient client => instance != null ? instance.client : null;

        // TcpClient.Connected doesn't check if socket != null, which
        // results in NullReferenceExceptions if connection was closed.
        // -> let's check it manually instead
        public bool Connected => instance != null &&
                                 instance.client != null &&
                                 instance.client.Client != null &&
                                 instance.client.Client.Connected;


        public bool Connecting => instance != null && instance.Connecting;

        // send queue
        // => SafeQueue is twice as fast as ConcurrentQueue, see SafeQueue.cs!
        SafeQueue<byte[]> sendQueue = new SafeQueue<byte[]>();

        // ManualResetEvent to wake up the send thread. better than Thread.Sleep
        // -> call Set() if everything was sent
        // -> call Reset() if there is something to send again
        // -> call WaitOne() to block until Reset was called
        ManualResetEvent sendPending = new ManualResetEvent(false);

        // the thread function
        void ReceiveThreadFunction(string ip, int port, TcpInstance current)
        {
            // absolutely must wrap with try/catch, otherwise thread
            // exceptions are silent
            try
            {
                // connect (blocking)
                current.client.Connect(ip, port);

                // if thread has been interrupted during Connect we need to stop and clean up the current Instance below
                if (!current.interrupt)
                {
                    current.Connecting = false;

                    // set socket options after the socket was created in Connect()
                    // (not after the constructor because we clear the socket there)
                    current.client.NoDelay = NoDelay;
                    current.client.SendTimeout = SendTimeout;

                    // start send thread only after connected
                    current.sendThread = new Thread(() => { SendLoop(0, current.client, sendQueue, sendPending); });
                    current.sendThread.IsBackground = true;
                    current.sendThread.Start();

                    // run the receive loop
                    ReceiveLoop(0, current.client, receiveQueue, MaxMessageSize);
                }
            }
            catch (SocketException exception)
            {
                // if thread has been interrupted ignore the SocketException and clean up the current Instance below
                if (!current.interrupt)
                {
                    // this happens if (for example) the ip address is correct
                    // but there is no server running on that ip/port
                    Logger.Log("Client Recv: failed to connect to ip=" + ip + " port=" + port + " reason=" + exception);

                    // add 'Disconnected' event to message queue so that the caller
                    // knows that the Connect failed. otherwise they will never know
                    receiveQueue.Enqueue(new Message(0, EventType.Disconnected, null));
                }
            }
            catch (ThreadInterruptedException)
            {
                // expected if Disconnect() aborts it
            }
            catch (ThreadAbortException)
            {
                // expected if Disconnect() aborts it
            }
            catch (Exception exception)
            {
                // something went wrong. probably important.
                Logger.LogError("Client Recv Exception: " + exception);
            }

            // sendthread might be waiting on ManualResetEvent,
            // so let's make sure to end it if the connection
            // closed.
            // otherwise the send thread would only end if it's
            // actually sending data while the connection is
            // closed.
            current.sendThread?.Interrupt();

            // Connect might have failed. thread might have been closed.
            // let's reset connecting state no matter what.
            current.Connecting = false;

            // if we got here then we are done. ReceiveLoop cleans up already,
            // but we may never get there if connect fails. so let's clean up
            // here too.
            current.client?.Close();
        }

        public void Connect(string ip, int port)
        {
            // if already started, return
            if (instance != null)
            {
                Logger.LogWarning("Telepathy Client can not create connection because an existing connection is connecting or connected");
                return;
            }

            // see comments on TcpInstance
            instance = new TcpInstance();

            // We are connecting from now until Connect succeeds or fails
            instance.Connecting = true;

            // create a TcpClient with perfect IPv4, IPv6 and hostname resolving
            // support.
            //
            // * TcpClient(hostname, port): works but would connect (and block)
            //   already
            // * TcpClient(AddressFamily.InterNetworkV6): takes Ipv4 and IPv6
            //   addresses but only connects to IPv6 servers (e.g. Telepathy).
            //   does NOT connect to IPv4 servers (e.g. Mirror Booster), even
            //   with DualMode enabled.
            // * TcpClient(): creates IPv4 socket internally, which would force
            //   Connect() to only use IPv4 sockets.
            //
            // => the trick is to clear the internal IPv4 socket so that Connect
            //    resolves the hostname and creates either an IPv4 or an IPv6
            //    socket as needed (see TcpClient source)
            // creates IPv4 socket
            instance.client = new TcpClient();
            // clear internal IPv4 socket until Connect()
            instance.client.Client = null;

            // clear old messages in queue, just to be sure that the caller
            // doesn't receive data from last time and gets out of sync.
            // -> calling this in Disconnect isn't smart because the caller may
            //    still want to process all the latest messages afterwards
            receiveQueue = new ConcurrentQueue<Message>();
            sendQueue.Clear();

            // client.Connect(ip, port) is blocking. let's call it in the thread
            // and return immediately.
            // -> this way the application doesn't hang for 30s if connect takes
            //    too long, which is especially good in games
            // -> this way we don't async client.BeginConnect, which seems to
            //    fail sometimes if we connect too many clients too fast
            instance.receiveThread = new Thread(() => { ReceiveThreadFunction(ip, port, instance); });
            instance.receiveThread.IsBackground = true;
            instance.receiveThread.Start();
        }

        public void Disconnect()
        {
            // only if started
            if (instance != null)
            {
                // close client
                instance.client.Close();

                // wait until thread finished. this is the only way to guarantee
                // that we can call Connect() again immediately after Disconnect
                // -> calling .Join would sometimes wait forever, e.g. when
                //    calling Disconnect while trying to connect to a dead end
                instance.receiveThread?.Interrupt();

                instance.interrupt = true;

                // we interrupted the receive Thread, so we can't guarantee that
                // connecting was reset. let's do it manually.
                instance.Connecting = false;

                // clear send queues. no need to hold on to them.
                // (unlike receiveQueue, which is still needed to process the
                //  latest Disconnected message, etc.)
                sendQueue.Clear();

                // let go of this one completely. the thread ended, no one uses
                // it anymore and this way Connected is false again immediately.
                instance.client = null;
                instance = null;
            }
        }

        public bool Send(byte[] data)
        {
            if (Connected)
            {
                // respect max message size to avoid allocation attacks.
                if (data.Length <= MaxMessageSize)
                {
                    // add to send queue and return immediately.
                    // calling Send here would be blocking (sometimes for long times
                    // if other side lags or wire was disconnected)
                    sendQueue.Enqueue(data);
                    // interrupt SendThread WaitOne()
                    sendPending.Set();
                    return true;
                }
                Logger.LogError("Client.Send: message too big: " + data.Length + ". Limit: " + MaxMessageSize);
                return false;
            }
            Logger.LogWarning("Client.Send: not connected!");
            return false;
        }
    }
}
