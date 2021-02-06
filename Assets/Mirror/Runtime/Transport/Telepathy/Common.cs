// common code used by server and client
namespace Telepathy
{
    public abstract class Common
    {
        // IMPORTANT: DO NOT SHARE STATE ACROSS SEND/RECV LOOPS (DATA RACES)
        // (except receive pipe which is used for all threads)

        // thread safe pipe for received messages
        // (not a HashSet because one connection can have multiple new messages)
        protected readonly MagnificentReceivePipe receivePipe;

        // pipe count, useful for debugging / benchmarks
        public int ReceivePipeCount => receivePipe.Count;

        // warning if message queue gets too big
        // if the average message is about 20 bytes then:
        // -   1k messages are   20KB
        // -  10k messages are  200KB
        // - 100k messages are 1.95MB
        // 2MB are not that much, but it is a bad sign if the caller process
        // can't call GetNextMessage faster than the incoming messages.
        public static int messageQueueSizeWarning = 100000;

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

            // create receive pipe with max message size for pooling
            receivePipe = new MagnificentReceivePipe(MaxMessageSize);
        }
    }
}
