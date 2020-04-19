using System;
using System.Threading.Tasks;

namespace Mirror.Tests
{

    public class MockTransport : AsyncTransport
    {
        public TaskCompletionSource<IConnection> AcceptCompletionSource ;

        public override Task<IConnection> AcceptAsync()
        {
            AcceptCompletionSource = new TaskCompletionSource<IConnection>();
            return AcceptCompletionSource.Task;
        }

        public TaskCompletionSource<IConnection> ConnectCompletionSource;

        public override string Scheme => "tcp4";

        public override Task<IConnection> ConnectAsync(Uri uri)
        {
            ConnectCompletionSource = new TaskCompletionSource<IConnection>();
            return ConnectCompletionSource.Task;
        }

        public override void Disconnect()
        {
            AcceptCompletionSource.TrySetResult(null);
        }

        public override Task ListenAsync()
        {
            return Task.CompletedTask;
        }

        public override Uri ServerUri()
        {
            return new Uri("tcp4://localhost");
        }
    }
}
