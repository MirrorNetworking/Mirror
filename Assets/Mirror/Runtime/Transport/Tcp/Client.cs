using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Mirror.Tcp
{
    public class Client : Common
    {
        public event Action<byte[]> ReceivedData;
        public event Action Disconnected;
        public event Action<Exception> ReceivedError;

        public TcpClient client;

        public bool NoDelay = true;

        public bool Connecting { get; private set; }
        public bool Connected { get; private set; }

        public async Task ConnectAsync(string host, int port)
        {
            // not if already started
            if (client != null)
            {
                // paul:  exceptions are better than silence
                throw new Exception("Client already connected");
            }

            // We are connecting from now until Connect succeeds or fails
            Connecting = true;

            try
            {
                // TcpClient can only be used once. need to create a new one each
                // time.
                client = new TcpClient(AddressFamily.InterNetworkV6);
                // works with IPv6 and IPv4
                client.Client.DualMode = true;

                // NoDelay disables nagle algorithm. lowers CPU% and latency
                // but increases bandwidth
                client.NoDelay = this.NoDelay;

                await client.ConnectAsync(host, port);

                // now we are connected:
                Connected = true;
                Connecting = false;

                _ = ReceiveLoop(client);
            }
            catch (ObjectDisposedException)
            {
                // No error, the client got closed
            }
        }

        private async Task ReceiveLoop(TcpClient client)
        {
            try
            {
                using (Stream networkStream = client.GetStream())
                {
                    while (true)
                    {
                        byte[] data = await ReadMessageAsync(networkStream);
                        if (data == null)
                            break;

                        try
                        {
                            // we received some data,  raise event
                            ReceivedData?.Invoke(data);
                        }
                        catch (Exception exception)
                        {
                            ReceivedError?.Invoke(exception);
                        }
                    }
                    
                }
            }
            catch (ObjectDisposedException ex)
            {
                // client got closed while reading message async
            }
            finally
            {
                Disconnected?.Invoke();
                Disconnect();
            }
        }

        public void Disconnect()
        {
            // only if started
            if (client != null)
            {
                // close client
                client.Close();
                client = null;
                Connecting = false;
                Connected = false;
            }
        }

        // send the data or throw exception
        public async Task SendAsync(ArraySegment<byte> data)
        {
            if (client == null)
            {
                ReceivedError?.Invoke(new SocketException((int)SocketError.NotConnected));
                return;
            }

            try
            {
                await SendMessage(client.GetStream(), data);
            }
            catch (Exception ex)
            {
                Disconnect();
                ReceivedError?.Invoke(ex);
            }
        }


        public override string ToString()
        {
            if (Connected)
            {
                return $"TCP connected to {client.Client.RemoteEndPoint}";
            }
            if (Connecting)
            {
                return $"TCP connecting to {client.Client.RemoteEndPoint}";
            }
            return "";
        }
    }

}
