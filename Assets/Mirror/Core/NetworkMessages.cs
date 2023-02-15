using System;
using System.Runtime.CompilerServices;
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
        public static readonly ushort Id = (ushort)(typeof(T).FullName.GetStableHashCode());
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

        // max message content size (without header) calculation for convenience
        // -> Transport.GetMaxPacketSize is the raw maximum
        // -> Every message gets serialized into <<id, content>>
        // -> Every serialized message get put into a batch with a header
        public static int MaxContentSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Transport.active.GetMaxPacketSize()
                - IdSize
                - Batcher.HeaderSize;
        }

        // automated message id from type hash.
        // platform independent via stable hashcode.
        // => convenient so we don't need to track messageIds across projects
        // => addons can work with each other without knowing their ids before
        // => 2 bytes is enough to avoid collisions.
        //    registering a messageId twice will log a warning anyway.
        // Deprecated 2023-02-15
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use NetworkMessageId<T>.Id instead")]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static NetworkMessageDelegate WrapHandler<T, C>(Action<C, T, int> handler, bool requireAuthentication)
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
                        Debug.LogWarning($"Closing connection: {conn}. Received message {typeof(T)} that required authentication, but the user has not authenticated yet");
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
                    Debug.LogError($"Closed connection: {conn}. This can happen if the other side accidentally (or an attacker intentionally) sent invalid data. Reason: {exception}");
                    conn.Disconnect();
                    return;
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
                catch (Exception e)
                {
                    Debug.LogError($"Disconnecting connId={conn.connectionId} to prevent exploits from an Exception in MessageHandler: {e.GetType().Name} {e.Message}\n{e.StackTrace}");
                    conn.Disconnect();
                }
            };

        // version for handlers without channelId
        // TODO obsolete this some day to always use the channelId version.
        //      all handlers in this version are wrapped with 1 extra action.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static NetworkMessageDelegate WrapHandler<T, C>(Action<C, T> handler, bool requireAuthentication)
            where T : struct, NetworkMessage
            where C : NetworkConnection
        {
            // wrap action as channelId version, call original
            void Wrapped(C conn, T msg, int _) => handler(conn, msg);
            return WrapHandler((Action<C, T, int>)Wrapped, requireAuthentication);
        }
    }
}
