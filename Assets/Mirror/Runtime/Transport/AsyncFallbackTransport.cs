using System;
using System.Threading.Tasks;


namespace Mirror
{
    public class AsyncFallbackTransport : AsyncTransport
    {

        public AsyncTransport[] transports;

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
                throw new PlatformNotSupportedException("None of the transports is supported in this platform");
            }
        }

        public override async Task<IConnection> AcceptAsync()
        {
            foreach (AsyncTransport transport in transports)
            {
                try
                {
                    return await transport.AcceptAsync();
                }
                catch (PlatformNotSupportedException)
                {
                    // try the next transport
                }
            }

            throw new PlatformNotSupportedException("None of the transports is supported in this platform");
       }

        public override async Task<IConnection> ConnectAsync(Uri uri)
        {
            foreach (AsyncTransport transport in transports)
            {
                try
                {
                    return await transport.ConnectAsync(uri);
                }
                catch (PlatformNotSupportedException)
                {
                    // try the next transport
                }
            }
            throw new PlatformNotSupportedException($"No transport was able to connect to {uri}");
        }

        public override void Disconnect()
        {
            foreach (AsyncTransport transport in transports)
            {
                try
                {
                    transport.Disconnect();
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    // try the next transport
                }
            }
            throw new PlatformNotSupportedException($"No transport available in this platform");
        }

        public override async Task ListenAsync()
        {
            foreach (AsyncTransport transport in transports)
            {
                try
                {
                    await transport.ListenAsync();
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    // try the next transport
                }
            }
            throw new PlatformNotSupportedException($"No transport available in this platform");
        }

        public override Uri ServerUri()
        {
            foreach (AsyncTransport transport in transports)
            {
                try
                {
                    return transport.ServerUri();
                }
                catch (PlatformNotSupportedException)
                {
                    // try the next transport
                }
            }
            throw new PlatformNotSupportedException($"No transport available in this platform");
        }
    }
}