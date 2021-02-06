// common code used by server and client
namespace Telepathy
{
    public abstract class Common
    {
        // IMPORTANT: DO NOT SHARE STATE ACROSS SEND/RECV LOOPS (DATA RACES)

        // disconnect if message queue gets too big.
        // -> avoids ever growing queue memory if network is slower than input
        // -> disconnecting is great for load balancing. better to disconnect
        //    one connection than risking every connection / the whole server
        // -> huge queue would introduce multiple seconds of latency anyway
        //
        // Mirror/DOTSNET use MaxMessageSize batching, so for a 16kb max size:
        //   limit =  1,000 means  16 MB of memory/connection
        //   limit = 10,000 means 160 MB of memory/connection
        public int QueueLimit = 10000;

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

        // constructor
        protected Common(int MaxMessageSize)
        {
            this.MaxMessageSize = MaxMessageSize;
        }
    }
}
