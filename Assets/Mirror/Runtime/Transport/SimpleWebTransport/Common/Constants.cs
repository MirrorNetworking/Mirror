using System.Text;

namespace Mirror.SimpleWeb
{
    /// <summary>
    /// Constant values that should never change
    /// <para>
    /// Some values are from https://tools.ietf.org/html/rfc6455
    /// </para>
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// Header is at most 4 bytes
        /// <para>
        /// If message is less than 125 then header is 2 bytes, else header is 4 bytes
        /// </para>
        /// </summary>
        public const int HeaderSize = 4;

        /// <summary>
        /// Smallest size of header
        /// <para>
        /// If message is less than 125 then header is 2 bytes, else header is 4 bytes
        /// </para>
        /// </summary>
        public const int HeaderMinSize = 2;

        /// <summary>
        /// bytes for short length
        /// </summary>
        public const int ShortLength = 2;

        /// <summary>
        /// Message mask is always 4 bytes
        /// </summary>
        public const int MaskSize = 4;

        /// <summary>
        /// Max size of a message for length to be 1 byte long
        /// <para>
        /// payload length between 0-125
        /// </para>
        /// </summary>
        public const int BytePayloadLength = 125;

        /// <summary>
        /// if payload length is 126 when next 2 bytes will be the length
        /// </summary>
        public const int UshortPayloadLength = 126;

        /// <summary>
        /// if payload length is 127 when next 8 bytes will be the length
        /// </summary>
        public const int UlongPayloadLength = 127;


        /// <summary>
        /// Guid used for WebSocket Protocol
        /// </summary>
        public const string HandshakeGUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        public static readonly int HandshakeGUIDLength = HandshakeGUID.Length;

        public static readonly byte[] HandshakeGUIDBytes = Encoding.ASCII.GetBytes(HandshakeGUID);

        /// <summary>
        /// Handshake messages will end with \r\n\r\n
        /// </summary>
        public static readonly byte[] endOfHandshake = new byte[4] { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };
    }
}
