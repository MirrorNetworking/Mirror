// common code used by server and client
namespace Telepathy
{
    public abstract class Common
    {
        // IMPORTANT: DO NOT SHARE STATE ACROSS SEND/RECV LOOPS (DATA RACES)
        // (except receive pipe which is used for all threads)

        // NoDelay disables nagle algorithm. lowers CPU% and latency but
        // increases bandwidth
        public bool NoDelay = true;

        // Prevent allocation attacks. Each packet is prefixed with a length
        // header, so an attacker could send a fake packet with length=2GB,
        // causing the server to allocate 2GB and run out of memory quickly.
        // -> simply increase max packet size if you want to send around bigger
        //    files!
        // -> 16KB per message should be more than enough.
        public readonly int MaxMessageSize;

        // Send would stall forever if the network is cut off during a send, so
        // we need a timeout (in milliseconds)
        public int SendTimeout = 5000;

        // Default TCP receive time out can be huge (minutes).
        // That's way too much for games, let's make it configurable.
        // we need a timeout (in milliseconds)
        // => '0' means disabled
        // => disabled by default because some people might use Telepathy
        //    without Mirror and without sending pings, so timeouts are likely
        public int ReceiveTimeout = 0;

        // constructor
        protected Common(int MaxMessageSize)
        {
            this.MaxMessageSize = MaxMessageSize;
        }
    }
}
