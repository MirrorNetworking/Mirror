using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace Mirror.Tests
{

    public class LoopbackTransport : Transport
    {
        public readonly Channel<IConnection> AcceptConnections = Cysharp.Threading.Tasks.Channel.CreateSingleConsumerUnbounded<IConnection>();

        public override IEnumerable<string> Scheme => new [] { "local" };

        public override bool Supported => true;

        public override UniTask<IConnection> ConnectAsync(Uri uri)
        {
            (IConnection c1, IConnection c2) = PipeConnection.CreatePipe();
            Connected.Invoke(c2);
            return UniTask.FromResult(c1);
        }

        UniTaskCompletionSource listenCompletionSource;

        public override void Disconnect()
        {
            listenCompletionSource?.TrySetResult();
        }

        public override UniTask ListenAsync()
        {
            Started.Invoke();
            listenCompletionSource = new UniTaskCompletionSource();
            return listenCompletionSource.Task;
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
