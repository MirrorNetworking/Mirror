// IMPORTANT
// force all thread functions to be STATIC.
// => Common.Send/ReceiveLoop is EXTREMELY DANGEROUS because it's too easy to
//    accidentally share Common state between threads.
// => header buffer, payload etc. were accidentally shared once after changing
//    the thread functions from static to non static
// => C# does not automatically detect data races. best we can do is move all of
//    our thread code into static functions and pass all state into them
//
// let's even keep them in a STATIC CLASS so it's 100% obvious that this should
// NOT EVER be changed to non static!
using System;
using System.Net.Sockets;
using System.Threading;

namespace Telepathy
{
    public static class ThreadFunctions
    {
        // send message (via stream) with the <size,content> message structure
        // this function is blocking sometimes!
        // (e.g. if someone has high latency or wire was cut off)
        // -> payload is of multiple <<size, content, size, content, ...> parts
        public static bool SendMessagesBlocking(NetworkStream stream, byte[] payload, int packetSize)
        {
            // stream.Write throws exceptions if client sends with high
            // frequency and the server stops
            try
            {
                // write the whole thing
                stream.Write(payload, 0, packetSize);
                return true;
            }
            catch (Exception exception)
            {
                // log as regular message because servers do shut down sometimes
                Log.Info("[Telepathy] Send: stream.Write exception: " + exception);
                return false;
            }
        }
        // read message (via stream) blocking.
        // writes into byte[] and returns bytes written to avoid allocations.
        public static bool ReadMessageBlocking(NetworkStream stream, int MaxMessageSize, byte[] headerBuffer, byte[] payloadBuffer, out int size)
        {
            size = 0;

            // buffer needs to be of Header + MaxMessageSize
            if (payloadBuffer.Length != 4 + MaxMessageSize)
            {
                Log.Error($"[Telepathy] ReadMessageBlocking: payloadBuffer needs to be of size 4 + MaxMessageSize = {4 + MaxMessageSize} instead of {payloadBuffer.Length}");
                return false;
            }

            // read exactly 4 bytes for header (blocking)
            if (!stream.ReadExactly(headerBuffer, 4))
                return false;

            // convert to int
            size = Utils.BytesToIntBigEndian(headerBuffer);

            // protect against allocation attacks. an attacker might send
            // multiple fake '2GB header' packets in a row, causing the server
            // to allocate multiple 2GB byte arrays and run out of memory.
            //
            // also protect against size <= 0 which would cause issues
            if (size > 0 && size <= MaxMessageSize)
            {
                // read exactly 'size' bytes for content (blocking)
                return stream.ReadExactly(payloadBuffer, size);
            }
            Log.Warning("[Telepathy] ReadMessageBlocking: possible header attack with a header of: " + size + " bytes.");
            return false;
        }

        // thread receive function is the same for client and server's clients
        public static void ReceiveLoop(int connectionId, TcpClient client, int MaxMessageSize, MagnificentReceivePipe receivePipe, int QueueLimit)
        {
            // get NetworkStream from client
            NetworkStream stream = client.GetStream();

            // every receive loop needs it's own receive buffer of
            // HeaderSize + MaxMessageSize
            // to avoid runtime allocations.
            //
            // IMPORTANT: DO NOT make this a member, otherwise every connection
            //            on the server would use the same buffer simulatenously
            byte[] receiveBuffer = new byte[4 + MaxMessageSize];

            // avoid header[4] allocations
            //
            // IMPORTANT: DO NOT make this a member, otherwise every connection
            //            on the server would use the same buffer simulatenously
            byte[] headerBuffer = new byte[4];

            // absolutely must wrap with try/catch, otherwise thread exceptions
            // are silent
            try
            {
                // add connected event to pipe
                receivePipe.Enqueue(connectionId, EventType.Connected, default);

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
                    if (!ReadMessageBlocking(stream, MaxMessageSize, headerBuffer, receiveBuffer, out int size))
                        // break instead of return so stream close still happens!
                        break;

                    // create arraysegment for the read message
                    ArraySegment<byte> message = new ArraySegment<byte>(receiveBuffer, 0, size);

                    // send to main thread via pipe
                    // -> it'll copy the message internally so we can reuse the
                    //    receive buffer for next read!
                    receivePipe.Enqueue(connectionId, EventType.Data, message);

                    // disconnect if receive pipe gets too big for this connectionId.
                    // -> avoids ever growing queue memory if network is slower
                    //    than input
                    // -> disconnecting is great for load balancing. better to
                    //    disconnect one connection than risking every
                    //    connection / the whole server
                    if (receivePipe.Count(connectionId) >= QueueLimit)
                    {
                        // log the reason
                        Log.Warning($"[Telepathy] ReceivePipe reached limit of {QueueLimit} for connectionId {connectionId}. This can happen if network messages come in way faster than we manage to process them. Disconnecting this connection for load balancing.");

                        // IMPORTANT: do NOT clear the whole queue. we use one
                        // queue for all connections.
                        //receivePipe.Clear();

                        // just break. the finally{} will close everything.
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                // something went wrong. the thread was interrupted or the
                // connection closed or we closed our own connection or ...
                // -> either way we should stop gracefully
                Log.Info("[Telepathy] ReceiveLoop finished receive function for connectionId=" + connectionId + " reason: " + exception);
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
                receivePipe.Enqueue(connectionId, EventType.Disconnected, default);
            }
        }
        // thread send function
        // note: we really do need one per connection, so that if one connection
        //       blocks, the rest will still continue to get sends
        public static void SendLoop(int connectionId, TcpClient client, MagnificentSendPipe sendPipe, ManualResetEvent sendPending)
        {
            // get NetworkStream from client
            NetworkStream stream = client.GetStream();

            // avoid payload[packetSize] allocations. size increases dynamically as
            // needed for batching.
            //
            // IMPORTANT: DO NOT make this a member, otherwise every connection
            //            on the server would use the same buffer simulatenously
            byte[] payload = null;

            try
            {
                while (client.Connected) // try this. client will get closed eventually.
                {
                    // reset ManualResetEvent before we do anything else. this
                    // way there is no race condition. if Send() is called again
                    // while in here then it will be properly detected next time
                    // -> otherwise Send might be called right after dequeue but
                    //    before .Reset, which would completely ignore it until
                    //    the next Send call.
                    sendPending.Reset(); // WaitOne() blocks until .Set() again

                    // dequeue & serialize all
                    // a locked{} TryDequeueAll is twice as fast as
                    // ConcurrentQueue, see SafeQueue.cs!
                    if (sendPipe.DequeueAndSerializeAll(ref payload, out int packetSize))
                    {
                        // send messages (blocking) or stop if stream is closed
                        if (!SendMessagesBlocking(stream, payload, packetSize))
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
                Log.Info("[Telepathy] SendLoop Exception: connectionId=" + connectionId + " reason: " + exception);
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