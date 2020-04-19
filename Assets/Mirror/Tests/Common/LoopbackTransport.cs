using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Mirror.Tests
{

    public class LoopbackTransport : AsyncTransport
    {
        public TaskCompletionSource<IConnection> AcceptCompletionSource ;

        public override Task<IConnection> AcceptAsync()
        {
            AcceptCompletionSource = new TaskCompletionSource<IConnection>();
            return AcceptCompletionSource.Task;
        }

        public TaskCompletionSource<IConnection> ConnectCompletionSource;

        public override string Scheme => "local";

        public override Task<IConnection> ConnectAsync(Uri uri)
        {
            (IConnection c1, IConnection c2) = PipeConnection.CreatePipe();
            if (AcceptCompletionSource == null)
                return Task.FromException<IConnection>(new SocketException((int)SocketError.ConnectionRefused));
            else
            {
                AcceptCompletionSource?.SetResult(c2);
                return Task.FromResult(c1);
            }
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
            return new UriBuilder()
            {
                Scheme = Scheme,
                Host = "localhost"
            }.Uri;
        }
    }
}
