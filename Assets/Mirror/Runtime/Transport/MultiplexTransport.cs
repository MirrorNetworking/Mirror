using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace Mirror
{
    public class MultiplexTransport : Transport
    {

        public Transport[] transports;

        public override IEnumerable<string> Scheme =>
            transports
                .Where(transport => transport.Supported)
                .SelectMany(transport => transport.Scheme);

        internal Transport GetTransport()
        {
            foreach (Transport transport in transports)
            {
                if (transport.Supported)
                    return transport;
            }
            throw new PlatformNotSupportedException("None of the transports is supported in this platform");
        }

        public override bool Supported => GetTransport() != null;

        public override UniTask<IConnection> ConnectAsync(Uri uri)
        {
            foreach (Transport transport in transports)
            {
                if (transport.Supported && transport.Scheme.Contains(uri.Scheme))
                    return transport.ConnectAsync(uri);
            }
            throw new ArgumentException($"No transport was able to connect to {uri}");
        }

        public override void Disconnect()
        {
            foreach (Transport transport in transports)
                transport.Disconnect();
        }

        public void Start()
        {
            foreach (Transport t in transports)
            {
                t.Connected.AddListener(c => Connected.Invoke(c));
                t.Started.AddListener(() => Started.Invoke());
            }
        }
        
        public override UniTask ListenAsync()
        {
            return UniTask.WhenAll(transports.Select(t => t.ListenAsync()));
        }

        public override IEnumerable<Uri> ServerUri() =>
            transports
                .Where(transport => transport.Supported)
                .SelectMany(transport => transport.ServerUri());
    }
}
