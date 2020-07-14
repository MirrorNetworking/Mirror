using System;
using System.Threading.Tasks;

namespace Mirror.Tests
{

    public class MockTransport : Transport
    {
        public readonly AsyncQueue<IConnection> AcceptConnections = new AsyncQueue<IConnection>();

        public override async Task<IConnection> AcceptAsync()
        {
            return await AcceptConnections.DequeueAsync();
        }

        public readonly AsyncQueue<IConnection> ConnectConnections = new AsyncQueue<IConnection>();

        public override string Scheme => "tcp4";

        public override bool Supported => true;

        public override async Task<IConnection> ConnectAsync(Uri uri)
        {
            return await ConnectConnections.DequeueAsync();
        }

        public override void Disconnect()
        {
            AcceptConnections.Enqueue(null);
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
