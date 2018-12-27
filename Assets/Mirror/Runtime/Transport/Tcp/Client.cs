using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Mirror.Transport.Tcp
{
    public class Client : Common
    {
        public event Action Connected;
        public event Action<byte[]> ReceivedData;
        public event Action Disconnected;
        public event Action<Exception> ReceivedError;

        public TcpClient client;

        public bool NoDelay = true;
               
        public bool Connecting { get; set; }
        public bool IsConnected { get; set; }

        public async void Connect(string host, int port)
        {
            // not if already started
            if (client != null)
            {
                // paul:  exceptions are better than silence
                ReceivedError?.Invoke(new Exception("Client already connected"));
                return;
            }

            // We are connecting from now until Connect succeeds or fails
            Connecting = true;

            // TcpClient can only be used once. need to create a new one each
            // time.
            client = new TcpClient();

            // NoDelay disables nagle algorithm. lowers CPU% and latency
            // but increases bandwidth
            client.NoDelay = this.NoDelay;

            try
            {
                IPAddress ipAddress = await ResolveAsync(host);

                await client.ConnectAsync(ipAddress, port);

                // now we are connected:
                IsConnected = true;
                Connecting = false;

                Connected?.Invoke();
                await ReceiveLoop(client);
            }
            catch (ObjectDisposedException)
            {
                // No error, the client got closed
            }
            catch (Exception ex)
            {
                ReceivedError?.Invoke(ex);
            }
            finally
            {
                Disconnect();
                Disconnected?.Invoke();
            }
        }

        // resolve the host to an ip address
        private async Task<IPAddress> ResolveAsync(string host)
        {
            IPHostEntry ipHostInfo = await Dns.GetHostEntryAsync(host);
            IPAddress ipAddress = ipHostInfo.AddressList.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork);
            if (ipAddress == null)
                throw new SocketException((int)SocketError.AddressFamilyNotSupported);

            return ipAddress;
        }

        private async Task ReceiveLoop(TcpClient client)
        {
            using (NetworkStream networkStream = client.GetStream())
            {
                while (true)
                {
                    byte[] data = await ReadMessageAsync(networkStream);

                    if (data == null)
                        break;

                    // we received some data,  raise event
                    ReceivedData?.Invoke(data);
                }
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
                IsConnected = false;
            }
        }

        // send the data or throw exception
        public async void Send(byte[] data)
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
    }
}
