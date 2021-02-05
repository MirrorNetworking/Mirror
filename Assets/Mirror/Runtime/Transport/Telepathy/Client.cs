using System;
using System.Net.Sockets;
using System.Threading;

namespace Telepathy
{
    // ClientState OBJECT that can be handed to the ReceiveThread safely.
    // => allows us to create a NEW OBJECT every time we connect and start a
    //    receive thread.
    // => perfectly protects us against data races. fixes all the flaky tests
    //    where .Connecting or .client would still be used by a dieing thread
    //    while attempting to use it for a new connection attempt etc.
    // => creating a fresh client state each time is the best solution against
    //    data races here!
    class ClientState : ConnectionState
    {
        public Thread receiveThread;

        // TcpClient.Connected doesn't check if socket != null, which
        // results in NullReferenceExceptions if connection was closed.
        // -> let's check it manually instead
        public bool Connected => client != null &&
                                 client.Client != null &&
                                 client.Client.Connected;

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

        // constructor always creates new TcpClient for client connection!
        public ClientState(int MaxMessageSize) : base(new TcpClient(), MaxMessageSize) {}

        // dispose all the state safely
        public void Dispose()
        {
            // close client
            client.Close();

            // wait until thread finished. this is the only way to guarantee
            // that we can call Connect() again immediately after Disconnect
            // -> calling .Join would sometimes wait forever, e.g. when
            //    calling Disconnect while trying to connect to a dead end
            receiveThread?.Interrupt();

            // we interrupted the receive Thread, so we can't guarantee that
            // connecting was reset. let's do it manually.
            Connecting = false;

            // clear send pipe. no need to hold on to elements.
            // (unlike receiveQueue, which is still needed to process the
            //  latest Disconnected message, etc.)
            sendPipe.Clear();

            // IMPORTANT: DO NOT CLEAR RECEIVE PIPE.
            // we still want to process disconnect messages in Tick()!

            // let go of this client completely. the thread ended, no one uses
            // it anymore and this way Connected is false again immediately.
            client = null;
        }
    }

    public class Client : Common
    {
        // events to hook into
        // => OnData uses ArraySegment for allocation free receives later
        public Action OnConnected;
        public Action<ArraySegment<byte>> OnData;
        public Action OnDisconnected;

        // all client state wrapped into an object that is passed to ReceiveThread
        // => we create a new one each time we connect to avoid data races with
        //    old dieing threads still using the previous object!
        ClientState state;

        // Connected & Connecting
        public bool Connected => state != null && state.Connected;
        public bool Connecting => state != null && state.Connecting;

        // constructor
        public Client(int MaxMessageSize) : base(MaxMessageSize) {}

        // the thread function
        // STATIC to avoid sharing state!
        // => pass ClientState object. a new one is created for each new thread!
        // => avoids data races where an old dieing thread might still modify
        //    the current thread's state :/
        static void ReceiveThreadFunction(ClientState state, string ip, int port, int MaxMessageSize, bool NoDelay, int SendTimeout, int QueueLimit)
        {
            Thread sendThread = null;

            // absolutely must wrap with try/catch, otherwise thread
            // exceptions are silent
            try
            {
                // connect (blocking)
                state.client.Connect(ip, port);
                state.Connecting = false; // volatile!

                // set socket options after the socket was created in Connect()
                // (not after the constructor because we clear the socket there)
                state.client.NoDelay = NoDelay;
                state.client.SendTimeout = SendTimeout;

                // start send thread only after connected
                // IMPORTANT: DO NOT SHARE STATE ACROSS MULTIPLE THREADS!
                sendThread = new Thread(() => { ThreadFunctions.SendLoop(0, state.client, state.sendPipe, state.sendPending, MaxMessageSize); });
                sendThread.IsBackground = true;
                sendThread.Start();

                // run the receive loop
                // IMPORTANT: DO NOT SHARE STATE ACROSS MULTIPLE THREADS!
                ThreadFunctions.ReceiveLoop(0, state.client, MaxMessageSize, state.receivePipe, QueueLimit);
            }
            catch (SocketException exception)
            {
                // this happens if (for example) the ip address is correct
                // but there is no server running on that ip/port
                Log.Info("Client Recv: failed to connect to ip=" + ip + " port=" + port + " reason=" + exception);

                // set 'Disconnected' event to receive pipe so that the caller
                // knows that the Connect failed. otherwise they will never know
                state.receivePipe.SetDisconnected();
            }
            catch (ThreadInterruptedException)
            {
                // expected if Disconnect() aborts it
            }
            catch (ThreadAbortException)
            {
                // expected if Disconnect() aborts it
            }
            catch (ObjectDisposedException)
            {
                // expected if Disconnect() aborts it and disposed the client
                // while ReceiveThread is in a blocking Connect() call
            }
            catch (Exception exception)
            {
                // something went wrong. probably important.
                Log.Error("Client Recv Exception: " + exception);
            }

            // sendthread might be waiting on ManualResetEvent,
            // so let's make sure to end it if the connection
            // closed.
            // otherwise the send thread would only end if it's
            // actually sending data while the connection is
            // closed.
            sendThread?.Interrupt();

            // Connect might have failed. thread might have been closed.
            // let's reset connecting state no matter what.
            state.Connecting = false;

            // if we got here then we are done. ReceiveLoop cleans up already,
            // but we may never get there if connect fails. so let's clean up
            // here too.
            state.client?.Close();
        }

        public void Connect(string ip, int port)
        {
            // not if already started
            if (Connecting || Connected)
            {
                Log.Warning("Telepathy Client can not create connection because an existing connection is connecting or connected");
                return;
            }

            // overwrite old thread's state object. create a new one to avoid
            // data races where an old dieing thread might still modify the
            // current state! fixes all the flaky tests!
            state = new ClientState(MaxMessageSize);

            // We are connecting from now until Connect succeeds or fails
            state.Connecting = true;

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
            state.client.Client = null; // clear internal IPv4 socket until Connect()

            // client.Connect(ip, port) is blocking. let's call it in the thread
            // and return immediately.
            // -> this way the application doesn't hang for 30s if connect takes
            //    too long, which is especially good in games
            // -> this way we don't async client.BeginConnect, which seems to
            //    fail sometimes if we connect too many clients too fast
            state.receiveThread = new Thread(() => {
                ReceiveThreadFunction(state, ip, port, MaxMessageSize, NoDelay, SendTimeout, QueueLimit);
            });
            state.receiveThread.IsBackground = true;
            state.receiveThread.Start();
        }

        public void Disconnect()
        {
            // only if started
            if (Connecting || Connected)
            {
                // dispose all the state safely
                state.Dispose();

                // IMPORTANT: DO NOT set state = null!
                // we still want to process the pipe's disconnect message etc.!
            }
        }

        // send message to server using socket connection.
        // arraysegment for allocation free sends later.
        // -> the segment's array is only used until Send() returns!
        public bool Send(ArraySegment<byte> message)
        {
            if (Connected)
            {
                // respect max message size to avoid allocation attacks.
                if (message.Count <= MaxMessageSize)
                {
                    // check send pipe limit
                    if (state.sendPipe.Count < QueueLimit)
                    {
                        // add to thread safe send pipe and return immediately.
                        // calling Send here would be blocking (sometimes for long
                        // times if other side lags or wire was disconnected)
                        state.sendPipe.Enqueue(message);
                        state.sendPending.Set(); // interrupt SendThread WaitOne()
                        return true;
                    }
                    // disconnect if send queue gets too big.
                    // -> avoids ever growing queue memory if network is slower
                    //    than input
                    // -> avoids ever growing latency as well
                    //
                    // note: while SendThread always grabs the WHOLE send queue
                    //       immediately, it's still possible that the sending
                    //       blocks for so long that the send queue just gets
                    //       way too big. have a limit - better safe than sorry.
                    else
                    {
                        // log the reason
                        Log.Warning($"Client.Send: sendPipe reached limit of {QueueLimit}. This can happen if we call send faster than the network can process messages. Disconnecting to avoid ever growing memory & latency.");

                        // just close it. send thread will take care of the rest.
                        state.client.Close();
                        return false;
                    }
                }
                Log.Error("Client.Send: message too big: " + message.Count + ". Limit: " + MaxMessageSize);
                return false;
            }
            Log.Warning("Client.Send: not connected!");
            return false;
        }

        // tick: processes up to 'limit' messages
        // => limit parameter to avoid deadlocks / too long freezes if server or
        //    client is too slow to process network load
        // => Mirror & DOTSNET need to have a process limit anyway.
        //    might as well do it here and make life easier.
        // => returns amount of remaining messages to process, so the caller
        //    can call tick again as many times as needed (or up to a limit)
        //
        // Tick() may process multiple messages, but Mirror needs a way to stop
        // processing immediately if a scene change messages arrives. Mirror
        // can't process any other messages during a scene change.
        // (could be useful for others too)
        // => make sure to allocate the lambda only once in transports
        public int Tick(int processLimit, Func<bool> checkEnabled = null)
        {
            // only if state was created yet (after connect())
            // note: we don't check 'only if connected' because we want to still
            //       process Disconnect messages afterwards too!
            if (state == null)
                return 0;

            // always process connect FIRST before anything else
            if (processLimit > 0)
            {
                if (state.receivePipe.CheckConnected())
                {
                    OnConnected?.Invoke();
                    // it counts as a processed message
                    --processLimit;
                }
            }

            // process up to 'processLimit' messages
            for (int i = 0; i < processLimit; ++i)
            {
                // check enabled in case a Mirror scene message arrived
                if (checkEnabled != null && !checkEnabled())
                    break;

                // peek first. allows us to process the first queued entry while
                // still keeping the pooled byte[] alive by not removing anything.
                if (state.receivePipe.TryPeek(out ArraySegment<byte> message))
                {
                    OnData?.Invoke(message);

                    // IMPORTANT: now dequeue and return it to pool AFTER we are
                    //            done processing the event.
                    state.receivePipe.TryDequeue();
                }
                // no more messages. stop the loop.
                else break;
            }

            // always process disconnect AFTER anything else
            // (should never process data messages after disconnect message)
            if (processLimit > 0)
            {
                if (state.receivePipe.CheckDisconnected())
                {
                    OnDisconnected?.Invoke();
                }
            }

            // return what's left to process for next time
            return state.receivePipe.Count;
        }
    }
}
