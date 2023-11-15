using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace Mirror
{
    // for performance, we (ab)use c# generics to cache the message id in a static field
    // this is significantly faster than doing the computation at runtime or looking up cached results via Dictionary
    // generic classes have separate static fields per type specification
    public static class NetworkMessageId<T> where T : struct, NetworkMessage
    {
        // automated message id from type hash.
        // platform independent via stable hashcode.
        // => convenient so we don't need to track messageIds across projects
        // => addons can work with each other without knowing their ids before
        // => 2 bytes is enough to avoid collisions.
        //    registering a messageId twice will log a warning anyway.
        public static readonly ushort Id = CalculateId();

        // Gets the 32bit fnv1a hash
        // To get it down to 16bit but still reduce hash collisions we cant just cast it to ushort
        // Instead we take the highest 16bits of the 32bit hash and fold them with xor into the lower 16bits
        // This will create a more uniform 16bit hash, the method is described in:
        // http://www.isthe.com/chongo/tech/comp/fnv/ in section "Changing the FNV hash size - xor-folding"
        static ushort CalculateId() => typeof(T).FullName.GetStableHashCode16();
    }

    // message packing all in one place, instead of constructing headers in all
    // kinds of different places
    //
    //   MsgType     (2 bytes)
    //   Content     (ContentSize bytes)
    public static class NetworkMessages
    {
        // size of message id header in bytes
        public const int IdSize = sizeof(ushort);

        // Id <> Type lookup for debugging, profiler, etc.
        // important when debugging messageId errors!
        public static readonly Dictionary<ushort, Type> Lookup =
            new Dictionary<ushort, Type>();

        // dump all types for debugging
        public static void LogTypes()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("NetworkMessageIds:");
            foreach (KeyValuePair<ushort, Type> kvp in Lookup)
            {
                builder.AppendLine($"  Id={kvp.Key} = {kvp.Value}");
            }
            Debug.Log(builder.ToString());
        }

        // max message content size (without header) calculation for convenience
        // -> Transport.GetMaxPacketSize is the raw maximum
        // -> Every message gets serialized into <<id, content>>
        // -> Every serialized message get put into a batch with one timestamp per batch
        // -> Every message in a batch has a varuint size header.
        //    use the worst case VarUInt size for the largest possible
        //    message size = int.max.
        public static int MaxContentSize(int channelId)
        {
            // calculate the max possible size that can fit in a batch
            int transportMax = Transport.active.GetMaxPacketSize(channelId);
            return transportMax - IdSize - Batcher.MaxMessageOverhead(transportMax);
        }

        // max message size which includes header + content.
        public static int MaxMessageSize(int channelId) =>
            MaxContentSize(channelId) + IdSize;

        // automated message id from type hash.
        // platform independent via stable hashcode.
        // => convenient so we don't need to track messageIds across projects
        // => addons can work with each other without knowing their ids before
        // => 2 bytes is enough to avoid collisions.
        //    registering a messageId twice will log a warning anyway.
        // keep this for convenience. easier to use than NetworkMessageId<T>.Id.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort GetId<T>() where T : struct, NetworkMessage =>
            NetworkMessageId<T>.Id;

        // pack message before sending
        // -> NetworkWriter passed as arg so that we can use .ToArraySegment
        //    and do an allocation free send before recycling it.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Pack<T>(T message, NetworkWriter writer)
            where T : struct, NetworkMessage
        {
            writer.WriteUShort(NetworkMessageId<T>.Id);
            writer.Write(message);
        }

        // read only the message id.
        // common function in case we ever change the header size.
        public static bool UnpackId(NetworkReader reader, out ushort messageId)
        {
            // read message type
            try
            {
                messageId = reader.ReadUShort();
                return true;
            }
            catch (System.IO.EndOfStreamException)
            {
                messageId = 0;
                return false;
            }
        }

        // version for handlers with channelId
        // inline! only exists for 20-30 messages and they call it all the time.
        internal static NetworkMessageDelegate WrapHandler<T, C>(Action<C, T, int> handler, bool requireAuthentication, bool exceptionsDisconnect)
            where T : struct, NetworkMessage
            where C : NetworkConnection
            => (conn, reader, channelId) =>
            {
                // protect against DOS attacks if attackers try to send invalid
                // data packets to crash the server/client. there are a thousand
                // ways to cause an exception in data handling:
                // - invalid headers
                // - invalid message ids
                // - invalid data causing exceptions
                // - negative ReadBytesAndSize prefixes
                // - invalid utf8 strings
                // - etc.
                //
                // let's catch them all and then disconnect that connection to avoid
                // further attacks.
                T message = default;
                // record start position for NetworkDiagnostics because reader might contain multiple messages if using batching
                int startPos = reader.Position;
                try
                {
                    if (requireAuthentication && !conn.isAuthenticated)
                    {
                        // message requires authentication, but the connection was not authenticated
                        Debug.LogWarning($"Disconnecting connection: {conn}. Received message {typeof(T)} that required authentication, but the user has not authenticated yet");
                        conn.Disconnect();
                        return;
                    }

                    //Debug.Log($"ConnectionRecv {conn} msgType:{typeof(T)} content:{BitConverter.ToString(reader.buffer.Array, reader.buffer.Offset, reader.buffer.Count)}");

                    // if it is a value type, just use default(T)
                    // otherwise allocate a new instance
                    message = reader.Read<T>();
                }
                catch (Exception exception)
                {
                    // should we disconnect on exceptions?
                    if (exceptionsDisconnect)
                    {
                        Debug.LogError($"Disconnecting connection: {conn} because reading a message of type {typeof(T)} caused an Exception. This can happen if the other side accidentally (or an attacker intentionally) sent invalid data. Reason: {exception}");
                        conn.Disconnect();
                        return;
                    }
                    // otherwise log it but allow the connection to keep playing
                    else
                    {
                        Debug.LogError($"Caught an Exception when reading a message from: {conn} of type {typeof(T)}. Reason: {exception}");
                        return;
                    }
                }
                finally
                {
                    int endPos = reader.Position;
                    // TODO: Figure out the correct channel
                    NetworkDiagnostics.OnReceive(message, channelId, endPos - startPos);
                }

                // user handler exception should not stop the whole server
                try
                {
                    // user implemented handler
                    handler((C)conn, message, channelId);
                }
                catch (Exception exception)
                {
                    // should we disconnect on exceptions?
                    if (exceptionsDisconnect)
                    {
                        Debug.LogError($"Disconnecting connection: {conn} because handling a message of type {typeof(T)} caused an Exception. This can happen if the other side accidentally (or an attacker intentionally) sent invalid data. Reason: {exception}");
                        conn.Disconnect();
                    }
                    // otherwise log it but allow the connection to keep playing
                    else
                    {
                        Debug.LogError($"Caught an Exception when handling a message from: {conn} of type {typeof(T)}. Reason: {exception}");
                    }
                }
            };

        // version for handlers without channelId
        // TODO obsolete this some day to always use the channelId version.
        //      all handlers in this version are wrapped with 1 extra action.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static NetworkMessageDelegate WrapHandler<T, C>(Action<C, T> handler, bool requireAuthentication, bool exceptionsDisconnect)
            where T : struct, NetworkMessage
            where C : NetworkConnection
        {
            // wrap action as channelId version, call original
            void Wrapped(C conn, T msg, int _) => handler(conn, msg);
            return WrapHandler((Action<C, T, int>)Wrapped, requireAuthentication, exceptionsDisconnect);
        }
    }
}
