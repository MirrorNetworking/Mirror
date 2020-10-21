
using System;
using UnityEngine;

namespace Mirror
{

    public static class ActiveTransport
    {
        public static IClientTransport client;
        public static IServerTransport server;
    }

    public interface ICommonTransport
    {
        bool Available();
        int GetMaxPacketSize(int channelId = 0);
        void Shutdown();

        // from MonoBehaviour
        // TODO remove need to set enable for transports
        bool enabled { get; set; }

        /// <summary>
        /// Checks for data and invokes events
        /// <para>Should be used to invoke events on main thread, eg from LateUpdate</para>
        /// </summary>
        void CheckForEvents();
    }

    public static class TransportExtensions
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(TransportExtensions));


        // validate packet size before sending. show errors if too big/small.
        // => it's best to check this here, we can't assume that all transports
        //    would check max size and show errors internally. best to do it
        //    in one place in hlapi.
        // => it's important to log errors, so the user knows what went wrong.
        public static bool ValidatePacketSize(this ICommonTransport transport, ArraySegment<byte> segment, int channelId)
        {
            if (segment.Count > transport.GetMaxPacketSize(channelId))
            {
                logger.LogError("cannot send packet larger than " + transport.GetMaxPacketSize(channelId) + " bytes");
                return false;
            }

            if (segment.Count == 0)
            {
                // zero length packets getting into the packet queues are bad.
                logger.LogError("cannot send zero bytes");
                return false;
            }

            // good size
            return true;
        }
    }
}
