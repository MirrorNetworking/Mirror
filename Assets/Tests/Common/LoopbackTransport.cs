using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace Mirror.Tests
{

    public class LoopbackTransport : Transport
    {
        public readonly Channel<IConnection> AcceptConnections = Channel.CreateSingleConsumerUnbounded<IConnection>();

        public override async UniTask<IConnection> AcceptAsync()
        {
            return await AcceptConnections.Reader.ReadAsync();
        }

        public override IEnumerable<string> Scheme => new [] { "local" };

        public override bool Supported => true;

        public override UniTask<IConnection> ConnectAsync(Uri uri)
        {
            (IConnection c1, IConnection c2) = PipeConnection.CreatePipe();
            AcceptConnections.Writer.TryWrite(c2);

            return UniTask.FromResult(c1);
        }

        public override void Disconnect()
        {
            AcceptConnections.Writer.TryWrite(null);
        }

        public override UniTask ListenAsync()
        {
            return UniTask.CompletedTask;
        }

        public override IEnumerable<Uri> ServerUri()
        {
            var builder = new UriBuilder
            {
                Scheme = Scheme.First(),
                Host = "localhost"
            };

            return new[] { builder.Uri };
        }
    }
}
