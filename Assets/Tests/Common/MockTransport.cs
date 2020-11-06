using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Mirror.Tests
{

    public class MockTransport : Transport
    {
        public override IEnumerable<string> Scheme => new []{"kcp"};

        public override bool Supported => true;

        public override UniTask<IConnection> ConnectAsync(Uri uri)
        {
            return UniTask.FromResult<IConnection>(default);
        }

        UniTaskCompletionSource completionSource;

        public override void Disconnect()
        {
            completionSource.TrySetResult();
        }

        public override UniTask ListenAsync()
        {
            Started.Invoke();

            completionSource = new UniTaskCompletionSource();
            return completionSource.Task;
        }

        public override IEnumerable<Uri> ServerUri()
        {
            return new[] { new Uri("kcp://localhost") };
        }
    }
}
