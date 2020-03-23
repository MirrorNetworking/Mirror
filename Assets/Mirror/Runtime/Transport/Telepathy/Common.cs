// common code used by server and client
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;

namespace Telepathy
{
    public abstract class Common
    {
        /////////////////////////////////////////////////////////
        // common code
        // incoming message queue of <connectionId, message>
        // (not a HashSet because one connection can have multiple new messages)
        protected ConcurrentQueue<Message> receiveQueue = new ConcurrentQueue<Message>();

        // queue count, useful for debugging / benchmarks
        public int ReceiveQueueCount => receiveQueue.Count;

        // warning if message queue gets too big
        // if the average message is about 20 bytes then:
        // -   1k messages are   20KB
        // -  10k messages are  200KB
        // - 100k messages are 1.95MB
        // 2MB are not that much, but it is a bad sign if the caller process
        // can't call GetNextMessage faster than the incoming messages.
        public static int messageQueueSizeWarning = 100000;

        // removes and returns the oldest message from the message queue.
        // (might want to call this until it doesn't return anything anymore)
        // -> Connected, Data, Disconnected events are all added here
        // -> bool return makes while (GetMessage(out Message)) easier!
        // -> no 'is client connected' check because we still want to read the
        //    Disconnected message after a disconnect
        public bool GetNextMessage(out Message message)
        {
            return receiveQueue.TryDequeue(out message);
        }

        // NoDelay disables nagle algorithm. lowers CPU% and latency but
        // increases bandwidth
        public bool NoDelay = true;

        // Prevent allocation attacks. Each packet is prefixed with a length
        // header, so an attacker could send a fake packet with length=2GB,
        // causing the server to allocate 2GB and run out of memory quickly.
        // -> simply increase max packet size if you want to send around bigger
        //    files!
        // -> 16KB per message should be more than enough.
        public int MaxMessageSize = 16 * 1024;

        // Send would stall forever if the network is cut off during a send, so
        // we need a timeout (in milliseconds)
        public int SendTimeout = 5000;

        // avoid header[4] allocations but don't use one buffer for all threads
        [ThreadStatic] static byte[] header;

        // avoid payload[packetSize] allocations but don't use one buffer for
        // all threads
        [ThreadStatic] static byte[] payload;

        /////////////////////////////////////////////
        // static helper functions
        // send message (via stream) with the <size,content> message structure
        // this function is blocking sometimes!
        // (e.g. if someone has high latency or wire was cut off)
        protected static bool SendMessagesBlocking(NetworkStream stream, byte[][] messages)
        {
            // stream.Write throws exceptions if client sends with high
            // frequency and the server stops
            try
            {
                // we might have multiple pending messages. merge into one
                // packet to avoid TCP overheads and improve performance.
                int packetSize = 0;
                for (int i = 0; i < messages.Length; ++i)
                    // header + content
                    packetSize += sizeof(int) + messages[i].Length;

                // create payload buffer if not created yet or previous one is
                // too small
                // IMPORTANT: payload.Length might be > packetSize! don't use it!
                if (payload == null || payload.Length < packetSize)
                    payload = new byte[packetSize];

                // create the packet
                int position = 0;
                for (int i = 0; i < messages.Length; ++i)
                {
                    // create header buffer if not created yet
                    if (header == null)
                        header = new byte[4];

                    // construct header (size)
                    Utils.IntToBytesBigEndianNonAlloc(messages[i].Length, header);

                    // copy header + message into buffer
                    Array.Copy(header, 0, payload, position, header.Length);
                    Array.Copy(messages[i], 0, payload, position + header.Length, messages[i].Length);
                    position += header.Length + messages[i].Length;
                }

                // write the whole thing
                stream.Write(payload, 0, packetSize);

                return true;
            }
            catch (Exception exception)
            {
                // log as regular message because servers do shut down sometimes
                Logger.Log("Send: stream.Write exception: " + exception);
                return false;
            }
        }

        // read message (via stream) with the <size,content> message structure
        protected static bool ReadMessageBlocking(NetworkStream stream, int MaxMessageSize, out byte[] content)
        {
            content = null;

            // create header buffer if not created yet
            if (header == null)
                header = new byte[4];

            // read exactly 4 bytes for header (blocking)
            if (!stream.ReadExactly(header, 4))
                return false;

            // convert to int
            int size = Utils.BytesToIntBigEndian(header);

            // protect against allocation attacks. an attacker might send
            // multiple fake '2GB header' packets in a row, causing the server
            // to allocate multiple 2GB byte arrays and run out of memory.
            if (size <= MaxMessageSize)
            {
                // read exactly 'size' bytes for content (blocking)
                content = new byte[size];
                return stream.ReadExactly(content, size);
            }
            Logger.LogWarning("ReadMessageBlocking: possible allocation attack with a header of: " + size + " bytes.");
            return false;
        }

        // thread receive function is the same for client and server's clients
        // (static to reduce state for maximum reliability)
        protected static void ReceiveLoop(int connectionId, TcpClient client, ConcurrentQueue<Message> receiveQueue, int MaxMessageSize)
        {
            // get NetworkStream from client
            NetworkStream stream = client.GetStream();

            // keep track of last message queue warning
            DateTime messageQueueLastWarning = DateTime.Now;

            // absolutely must wrap with try/catch, otherwise thread exceptions
            // are silent
            try
            {
                // add connected event to queue with ip address as data in case
                // it's needed
                receiveQueue.Enqueue(new Message(connectionId, EventType.Connected, null));

                // let's talk about reading data.
                // -> normally we would read as much as possible and then
                //    extract as many <size,content>,<size,content> messages
                //    as we received this time. this is really complicated
                //    and expensive to do though
                // -> instead we use a trick:
                //      Read(2) -> size
                //        Read(size) -> content
                //      repeat
                //    Read is blocking, but it doesn't matter since the
                //    best thing to do until the full message arrives,
                //    is to wait.
                // => this is the most elegant AND fast solution.
                //    + no resizing
                //    + no extra allocations, just one for the content
                //    + no crazy extraction logic
                while (true)
                {
                    // read the next message (blocking) or stop if stream closed
                    byte[] content;
                    if (!ReadMessageBlocking(stream, MaxMessageSize, out content))
                        // break instead of return so stream close still happens!
                        break;

                    // queue it
                    receiveQueue.Enqueue(new Message(connectionId, EventType.Data, content));

                    // and show a warning if the queue gets too big
                    // -> we don't want to show a warning every single time,
                    //    because then a lot of processing power gets wasted on
                    //    logging, which will make the queue pile up even more.
                    // -> instead we show it every 10s, so that the system can
                    //    use most it's processing power to hopefully process it.
                    if (receiveQueue.Count > messageQueueSizeWarning)
                    {
                        TimeSpan elapsed = DateTime.Now - messageQueueLastWarning;
                        if (elapsed.TotalSeconds > 10)
                        {
                            Logger.LogWarning("ReceiveLoop: messageQueue is getting big(" + receiveQueue.Count + "), try calling GetNextMessage more often. You can call it more than once per frame!");
                            messageQueueLastWarning = DateTime.Now;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                // something went wrong. the thread was interrupted or the
                // connection closed or we closed our own connection or ...
                // -> either way we should stop gracefully
                Logger.Log("ReceiveLoop: finished receive function for connectionId=" + connectionId + " reason: " + exception);
            }
            finally
            {
                // clean up no matter what
                stream.Close();
                client.Close();

                // add 'Disconnected' message after disconnecting properly.
                // -> always AFTER closing the streams to avoid a race condition
                //    where Disconnected -> Reconnect wouldn't work because
                //    Connected is still true for a short moment before the stream
                //    would be closed.
                receiveQueue.Enqueue(new Message(connectionId, EventType.Disconnected, null));
            }
        }

        // thread send function
        // note: we really do need one per connection, so that if one connection
        //       blocks, the rest will still continue to get sends
        protected static void SendLoop(int connectionId, TcpClient client, SafeQueue<byte[]> sendQueue, ManualResetEvent sendPending)
        {
            // get NetworkStream from client
            NetworkStream stream = client.GetStream();

            try
            {
                // try this. client will get closed eventually.
                while (client.Connected)
                {
                    // reset ManualResetEvent before we do anything else. this
                    // way there is no race condition. if Send() is called again
                    // while in here then it will be properly detected next time
                    // -> otherwise Send might be called right after dequeue but
                    //    before .Reset, which would completely ignore it until
                    //    the next Send call.
                    // WaitOne() blocks until .Set() again
                    sendPending.Reset();

                    // dequeue all
                    // SafeQueue.TryDequeueAll is twice as fast as
                    // ConcurrentQueue, see SafeQueue.cs!
                    byte[][] messages;
                    if (sendQueue.TryDequeueAll(out messages))
                    {
                        // send message (blocking) or stop if stream is closed
                        if (!SendMessagesBlocking(stream, messages))
                            // break instead of return so stream close still happens!
                            break;
                    }

                    // don't choke up the CPU: wait until queue not empty anymore
                    sendPending.WaitOne();
                }
            }
            catch (ThreadAbortException)
            {
                // happens on stop. don't log anything.
            }
            catch (ThreadInterruptedException)
            {
                // happens if receive thread interrupts send thread.
            }
            catch (Exception exception)
            {
                // something went wrong. the thread was interrupted or the
                // connection closed or we closed our own connection or ...
                // -> either way we should stop gracefully
                Logger.Log("SendLoop Exception: connectionId=" + connectionId + " reason: " + exception);
            }
            finally
            {
                // clean up no matter what
                // we might get SocketExceptions when sending if the 'host has
                // failed to respond' - in which case we should close the connection
                // which causes the ReceiveLoop to end and fire the Disconnected
                // message. otherwise the connection would stay alive forever even
                // though we can't send anymore.
                stream.Close();
                client.Close();
            }
        }
    }
}
