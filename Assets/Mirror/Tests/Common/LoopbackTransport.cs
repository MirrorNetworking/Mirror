using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mirror.Tests
{

    public class LoopbackTransport : Transport
    {
        public readonly AsyncQueue<IConnection> AcceptConnections = new AsyncQueue<IConnection>();

        public override async Task<IConnection> AcceptAsync()
        {
            return await AcceptConnections.DequeueAsync();
        }

        public readonly AsyncQueue<IConnection> CoonnectedConnections = new AsyncQueue<IConnection>();

        public override IEnumerable<string> Scheme => new [] { "local" };

        public override bool Supported => true;

        public override Task<IConnection> ConnectAsync(Uri uri)
        {
            (IConnection c1, IConnection c2) = PipeConnection.CreatePipe();
            AcceptConnections.Enqueue(c2);

            return Task.FromResult(c1);
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
            return new UriBuilder
            {
                Scheme = Scheme.First(),
                Host = "localhost"
            }.Uri;
        }
    }
}
