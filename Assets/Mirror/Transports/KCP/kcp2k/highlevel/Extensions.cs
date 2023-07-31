using System;
using System.Net;
using System.Net.Sockets;

namespace kcp2k
{
    public static class Extensions
    {
        // ArraySegment as HexString for convenience
        public static string ToHexString(this ArraySegment<byte> segment) =>
            BitConverter.ToString(segment.Array, segment.Offset, segment.Count);

        // non-blocking UDP send.
        // allows for reuse when overwriting KcpServer/Client (i.e. for relays).
        // => wrapped with Poll to avoid WouldBlock allocating new SocketException.
        // => wrapped with try-catch to ignore WouldBlock exception.
        // make sure to set socket.Blocking = false before using this!
        public static bool SendToNonBlocking(this Socket socket, ArraySegment<byte> data, EndPoint remoteEP)
        {
            try
            {
                // when using non-blocking sockets, SendTo may return WouldBlock.
                // in C#, WouldBlock throws a SocketException, which is expected.
                // unfortunately, creating the SocketException allocates in C#.
                // let's poll first to avoid the WouldBlock allocation.
                // note that this entirely to avoid allocations.
                // non-blocking UDP doesn't need Poll in other languages.
                // and the code still works without the Poll call.
                if (!socket.Poll(0, SelectMode.SelectWrite)) return false;

                // send to the the endpoint.
                // do not send to 'newClientEP', as that's always reused.
                // fixes https://github.com/MirrorNetworking/Mirror/issues/3296
                socket.SendTo(data.Array, data.Offset, data.Count, SocketFlags.None, remoteEP);
                return true;
            }
            catch (SocketException e)
            {
                // for non-blocking sockets, SendTo may throw WouldBlock.
                // in that case, simply drop the message. it's UDP, it's fine.
                if (e.SocketErrorCode == SocketError.WouldBlock) return false;

                // otherwise it's a real socket error. throw it.
                throw;
            }
        }

        // non-blocking UDP send.
        // allows for reuse when overwriting KcpServer/Client (i.e. for relays).
        // => wrapped with Poll to avoid WouldBlock allocating new SocketException.
        // => wrapped with try-catch to ignore WouldBlock exception.
        // make sure to set socket.Blocking = false before using this!
        public static bool SendNonBlocking(this Socket socket, ArraySegment<byte> data)
        {
            try
            {
                // when using non-blocking sockets, SendTo may return WouldBlock.
                // in C#, WouldBlock throws a SocketException, which is expected.
                // unfortunately, creating the SocketException allocates in C#.
                // let's poll first to avoid the WouldBlock allocation.
                // note that this entirely to avoid allocations.
                // non-blocking UDP doesn't need Poll in other languages.
                // and the code still works without the Poll call.
                if (!socket.Poll(0, SelectMode.SelectWrite)) return false;

                // SendTo allocates. we used bound Send.
                socket.Send(data.Array, data.Offset, data.Count, SocketFlags.None);
                return true;
            }
            catch (SocketException e)
            {
                // for non-blocking sockets, SendTo may throw WouldBlock.
                // in that case, simply drop the message. it's UDP, it's fine.
                if (e.SocketErrorCode == SocketError.WouldBlock) return false;

                // otherwise it's a real socket error. throw it.
                throw;
            }
        }

        // non-blocking UDP receive.
        // allows for reuse when overwriting KcpServer/Client (i.e. for relays).
        // => wrapped with Poll to avoid WouldBlock allocating new SocketException.
        // => wrapped with try-catch to ignore WouldBlock exception.
        // make sure to set socket.Blocking = false before using this!
        public static bool ReceiveFromNonBlocking(this Socket socket, byte[] recvBuffer, out ArraySegment<byte> data, ref EndPoint remoteEP)
        {
            data = default;

            try
            {
                // when using non-blocking sockets, ReceiveFrom may return WouldBlock.
                // in C#, WouldBlock throws a SocketException, which is expected.
                // unfortunately, creating the SocketException allocates in C#.
                // let's poll first to avoid the WouldBlock allocation.
                // note that this entirely to avoid allocations.
                // non-blocking UDP doesn't need Poll in other languages.
                // and the code still works without the Poll call.
                if (!socket.Poll(0, SelectMode.SelectRead)) return false;

                // NOTE: ReceiveFrom allocates.
                //   we pass our IPEndPoint to ReceiveFrom.
                //   receive from calls newClientEP.Create(socketAddr).
                //   IPEndPoint.Create always returns a new IPEndPoint.
                //   https://github.com/mono/mono/blob/f74eed4b09790a0929889ad7fc2cf96c9b6e3757/mcs/class/System/System.Net.Sockets/Socket.cs#L1761
                //
                // throws SocketException if datagram was larger than buffer.
                // https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.receive?view=net-6.0
                int size = socket.ReceiveFrom(recvBuffer, 0, recvBuffer.Length, SocketFlags.None, ref remoteEP);
                data = new ArraySegment<byte>(recvBuffer, 0, size);
                return true;
            }
            catch (SocketException e)
            {
                // for non-blocking sockets, Receive throws WouldBlock if there is
                // no message to read. that's okay. only log for other errors.
                if (e.SocketErrorCode == SocketError.WouldBlock) return false;

                // otherwise it's a real socket error. throw it.
                throw;
            }
        }

        // non-blocking UDP receive.
        // allows for reuse when overwriting KcpServer/Client (i.e. for relays).
        // => wrapped with Poll to avoid WouldBlock allocating new SocketException.
        // => wrapped with try-catch to ignore WouldBlock exception.
        // make sure to set socket.Blocking = false before using this!
        public static bool ReceiveNonBlocking(this Socket socket, byte[] recvBuffer, out ArraySegment<byte> data)
        {
            data = default;

            try
            {
                // when using non-blocking sockets, ReceiveFrom may return WouldBlock.
                // in C#, WouldBlock throws a SocketException, which is expected.
                // unfortunately, creating the SocketException allocates in C#.
                // let's poll first to avoid the WouldBlock allocation.
                // note that this entirely to avoid allocations.
                // non-blocking UDP doesn't need Poll in other languages.
                // and the code still works without the Poll call.
                if (!socket.Poll(0, SelectMode.SelectRead)) return false;

                // ReceiveFrom allocates. we used bound Receive.
                // returns amount of bytes written into buffer.
                // throws SocketException if datagram was larger than buffer.
                // https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.receive?view=net-6.0
                //
                // throws SocketException if datagram was larger than buffer.
                // https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.receive?view=net-6.0
                int size = socket.Receive(recvBuffer, 0, recvBuffer.Length, SocketFlags.None);
                data = new ArraySegment<byte>(recvBuffer, 0, size);
                return true;
            }
            catch (SocketException e)
            {
                // for non-blocking sockets, Receive throws WouldBlock if there is
                // no message to read. that's okay. only log for other errors.
                if (e.SocketErrorCode == SocketError.WouldBlock) return false;

                // otherwise it's a real socket error. throw it.
                throw;
            }
        }
    }
}