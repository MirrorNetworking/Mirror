// common code used by server and client
using System;
using System.Net.Sockets;

namespace Telepathy
{
    public abstract class Common
    {
        // common code /////////////////////////////////////////////////////////
        // incoming message queue of <connectionId, message>
        // (not a HashSet because one connection can have multiple new messages)
        protected SafeQueue<Message> messageQueue = new SafeQueue<Message>();

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
            return messageQueue.TryDequeue(out message);
        }

        // static helper functions /////////////////////////////////////////////
        // fast ushort to byte[] conversion and vice versa
        // -> test with 100k conversions:
        //    BitConverter.GetBytes(ushort): 144ms
        //    bit shifting: 11ms
        // -> 10x speed improvement makes this optimization actually worth it
        // -> this way we don't need to allocate BinaryWriter/Reader either
        static byte[] UShortToBytes(ushort value)
        {
            return new byte[]
            {
                (byte)value,
                (byte)(value >> 8)
            };
        }
        static ushort BytesToUShort(byte[] bytes)
        {
            return (ushort)((bytes[1] << 8) + bytes[0]);
        }

        // send message (via stream) with the <size,content> message structure
        protected static bool SendMessage(NetworkStream stream, byte[] content)
        {
            // can we still write to this socket (not disconnected?)
            if (!stream.CanWrite)
            {
                Logger.LogWarning("Send: stream not writeable: " + stream);
                return false;
            }

            // check size
            if (content.Length > ushort.MaxValue)
            {
                Logger.LogError("Send: message too big(" + content.Length + ") max=" + ushort.MaxValue);
                return false;
            }

            // stream.Write throws exceptions if client sends with high
            // frequency and the server stops
            try
            {
                // construct header (size)
                byte[] header = UShortToBytes((ushort)content.Length);

                // write header+content at once via payload array. writing
                // header,payload separately would cause 2 TCP packets to be
                // sent if nagle's algorithm is disabled(2x TCP header overhead)
                byte[] payload = new byte[header.Length + content.Length];
                Array.Copy(header, payload, header.Length);
                Array.Copy(content, 0, payload, header.Length, content.Length);
                stream.Write(payload, 0, payload.Length);

                // flush to make sure it is being sent immediately
                stream.Flush();
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
        protected static bool ReadMessageBlocking(NetworkStream stream, out byte[] content)
        {
            content = null;

            // read exactly 2 bytes for header (blocking)
            byte[] header = new byte[2];
            if (!stream.ReadExactly(header, 2))
                return false;
            ushort size = BytesToUShort(header);

            // read exactly 'size' bytes for content (blocking)
            content = new byte[size];
            if (!stream.ReadExactly(content, size))
                return false;

            return true;
        }

        // thread receive function is the same for client and server's clients
        // (static to reduce state for maximum reliability)
        protected static void ReceiveLoop(int connectionId, TcpClient client, SafeQueue<Message> messageQueue)
        {
            // get NetworkStream from client
            NetworkStream stream = client.GetStream();

            // keep track of last message queue warning
            DateTime messageQueueLastWarning = DateTime.Now;

            // absolutely must wrap with try/catch, otherwise thread exceptions
            // are silent
            try
            {
                // add connected event to queue
                messageQueue.Enqueue(new Message(connectionId, EventType.Connected, null));

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
                    if (!ReadMessageBlocking(stream, out content))
                        break;

                    // queue it
                    messageQueue.Enqueue(new Message(connectionId, EventType.Data, content));

                    // and show a warning if the queue gets too big
                    // -> we don't want to show a warning every single time,
                    //    because then a lot of processing power gets wasted on
                    //    logging, which will make the queue pile up even more.
                    // -> instead we show it every 10s, so that the system can
                    //    use most it's processing power to hopefully process it.
                    if (messageQueue.Count > messageQueueSizeWarning)
                    {
                        TimeSpan elapsed = DateTime.Now - messageQueueLastWarning;
                        if (elapsed.TotalSeconds > 10)
                        {
                            Logger.LogWarning("ReceiveLoop: messageQueue is getting big(" + messageQueue.Count + "), try calling GetNextMessage more often. You can call it more than once per frame!");
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

            // if we got here then either the client while loop ended, or an
            // exception happened. disconnect
            messageQueue.Enqueue(new Message(connectionId, EventType.Disconnected, null));

            // clean up no matter what
            stream.Close();
            client.Close();
        }
    }
}
