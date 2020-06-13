using System.Collections.Concurrent;

namespace Mirror.Websocket
{
    public abstract class Common
    {
        public bool NoDelay = true;

        public bool enabled;

        public bool Connecting { get; set; }

        protected ConcurrentQueue<byte[]> receiveQueue = new ConcurrentQueue<byte[]>();
     
        public int ReceiveQueueCount => receiveQueue.Count;

        public bool GetNextMessage(out byte[] message)
        {
            return receiveQueue.TryDequeue(out message);
        }
    }
}
