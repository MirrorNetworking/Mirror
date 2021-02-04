// both server and client need a connection state object.
// -> server needs it to keep track of multiple connections
// -> client needs it to safely create a new connection state on every new
//    connect in order to avoid data races where a dieing thread might still
//    modify the current state. can't happen if we create a new state each time!
//    (fixes all the flaky tests)
//
// ... besides, it also allows us to share code!
using System.Net.Sockets;
using System.Threading;

namespace Telepathy
{
    public class ConnectionState
    {
        public TcpClient client;

        // thread safe pipe for received messages from recv thread to main thread
        //
        // IMPORTANT: every connection needs its own receive pipe.
        //            previously we had one for all connections:
        //            - all would have to share one lock{}
        //            - no way to limit one connection's queue
        //            - one spamming connection would slow down everyone
        //              else because Tick() only ever processed the next
        //              message in the pipe
        //              (could be thousands of messages from connection A
        //               before connection B's message is ever processed)
        public readonly MagnificentReceivePipe receivePipe;

        // thread safe pipe to send messages from main thread to send thread
        public readonly MagnificentSendPipe sendPipe;

        // ManualResetEvent to wake up the send thread. better than Thread.Sleep
        // -> call Set() if everything was sent
        // -> call Reset() if there is something to send again
        // -> call WaitOne() to block until Reset was called
        public ManualResetEvent sendPending = new ManualResetEvent(false);

        public ConnectionState(TcpClient client, int MaxMessageSize)
        {
            this.client = client;

            // create send pipe with max message size for pooling
            receivePipe = new MagnificentReceivePipe(MaxMessageSize);
            sendPipe = new MagnificentSendPipe(MaxMessageSize);
        }
    }
}