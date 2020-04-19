using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace Mirror
{
    public class AsyncMultiplexTransport : AsyncTransport
    {

        public AsyncTransport[] transports;

        private Dictionary<Task<IConnection>, AsyncTransport> Accepters;

        public override string Scheme
        {
            get
            {
                foreach (AsyncTransport transport in transports)
                {
                    try
                    {
                        return transport.Scheme;
                    }
                    catch (PlatformNotSupportedException)
                    {
                        // try the next transport
                    }
                }
                throw new PlatformNotSupportedException("No transport was able to provide scheme");
            }
        }

        public override async Task<IConnection> AcceptAsync()
        {
            if (Accepters == null)
            {
                Accepters = new Dictionary<Task<IConnection>, AsyncTransport>();

                foreach (AsyncTransport transport in transports)
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

            AsyncTransport acceptedTransport = Accepters[task];
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

        public override async Task<IConnection> ConnectAsync(Uri uri)
        {
            foreach (AsyncTransport transport in transports)
            {
                try
                {
                    return await transport.ConnectAsync(uri);
                }
                catch (ArgumentException)
                {
                    // try the next transport
                }
            }
            throw new ArgumentException($"No transport was able to connect to {uri}");
        }

        public override void Disconnect()
        {
            foreach (AsyncTransport transport in transports)
                transport.Disconnect();
        }

        public override async Task ListenAsync()
        {
            IEnumerable<Task> tasks = from t in transports select t.ListenAsync();
            await Task.WhenAll(tasks);
            Accepters = null;
        }

        public override Uri ServerUri()
        {
            return transports[0].ServerUri();
        }
    }
}