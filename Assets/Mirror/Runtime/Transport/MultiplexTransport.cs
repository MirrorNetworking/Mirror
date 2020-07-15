using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace Mirror
{
    public class MultiplexTransport : Transport
    {

        public Transport[] transports;

        private Dictionary<Task<IConnection>, Transport> Accepters;

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

        public override async Task<IConnection> AcceptAsync()
        {
            if (Accepters == null)
            {
                Accepters = new Dictionary<Task<IConnection>,  Transport>();

                foreach (Transport transport in transports)
                {
                    Task<IConnection> transportAccepter = transport.AcceptAsync();
                    Accepters[transportAccepter] = transport;
                }
            }

            // that's it nobody is left to accept
            if (Accepters.Count == 0)
                return null;

            // wait for any one of them to accept
            var task = await Task.WhenAny(Accepters.Keys);

            Transport acceptedTransport = Accepters[task];
            Accepters.Remove(task);

            IConnection value = await task;

            if (value == null)
            {
                // this transport closed. Get the next one
                return await AcceptAsync();
            }
            else
            {
                // transport may accept more connections
                task = acceptedTransport.AcceptAsync();
                Accepters[task] = acceptedTransport;

                return value;
            }
        }

        public override  Task<IConnection> ConnectAsync(Uri uri)
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

        public override async Task ListenAsync()
        {
            IEnumerable<Task> tasks = from t in transports select t.ListenAsync();
            await Task.WhenAll(tasks);
            Accepters = null;
        }

        public override IEnumerable<Uri> ServerUri() =>
            transports
                .Where(transport => transport.Supported)
                .SelectMany(transport => transport.ServerUri());
    }
}