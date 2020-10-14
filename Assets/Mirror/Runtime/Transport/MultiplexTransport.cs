using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace Mirror
{
    public class MultiplexTransport : Transport
    {

        public Transport[] transports;

        private Dictionary<UniTask<IConnection>, Transport> Accepters;

        public override IEnumerable<string> Scheme =>
            transports
                .Where(transport => transport.Supported)
                .SelectMany(transport => transport.Scheme);

        private Transport GetTransport()
        {
            foreach (Transport transport in transports)
            {
                if (transport.Supported)
                    return transport;
            }
            throw new PlatformNotSupportedException("None of the transports is supported in this platform");
        }

        public override bool Supported => GetTransport() != null;

        public override async UniTask<IConnection> AcceptAsync()
        {
            if (Accepters == null)
            {
                Accepters = new Dictionary<UniTask<IConnection>, Transport>();

                foreach (Transport transport in transports)
                {
                    UniTask<IConnection> transportAccepter = transport.AcceptAsync();
                    Accepters[transportAccepter] = transport;
                }
            }

            // that's it nobody is left to accept
            if (Accepters.Count == 0)
                return null;

            var tasks = Accepters.Keys;

            // wait for any one of them to accept
            var (index, connection) = await UniTask.WhenAny(tasks);

            var task = tasks.ElementAt(index);

            Transport acceptedTransport = Accepters[task];
            Accepters.Remove(task);

            if (connection == null)
            {
                // this transport closed. Get the next one
                return await AcceptAsync();
            }
            else
            {
                // transport may accept more connections
                task = acceptedTransport.AcceptAsync();
                Accepters[task] = acceptedTransport;

                return connection;
            }
        }

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

        public override async UniTask ListenAsync()
        {
            IEnumerable<UniTask> tasks = from t in transports select t.ListenAsync();
            await UniTask.WhenAll(tasks);
            Accepters = null;
        }

        public override IEnumerable<Uri> ServerUri() =>
            transports
                .Where(transport => transport.Supported)
                .SelectMany(transport => transport.ServerUri());
    }
}