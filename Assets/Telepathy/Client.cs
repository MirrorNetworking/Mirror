using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Telepathy
{
    public class Client : Common
    {
        public event Action OnConnected;
        public event Action<byte[]> ReceivedData;
        public event Action Disconnected;
        public event Action<Exception> ReceivedError;

        public TcpClient client;

        public bool NoDelay = true;
               
        public bool Connecting { get; set; }
        public bool Connected { get; set; }

        public async void Connect(string ip, int port)
        {
            // not if already started
            if (client != null)
            {
                // paul:  exceptions are better than silence
                ReceivedError?.Invoke(new Exception("Client already connected"));
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
                await client.ConnectAsync(ip, port);

                // now we are connected:
                Connected = true;
                Connecting = false;

                OnConnected?.Invoke();
                await ReceiveLoop(client);
            }
            catch (ObjectDisposedException)
            {
                // No error, the client got closed
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to connect " + ex);
                ReceivedError?.Invoke(ex);
            }
            finally
            {
                Connected = false;
                Connecting = false;
                client.Close();
                Disconnected?.Invoke();
            }
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
            if (Connecting || Connected)
            {
                // close client
                client.Close();
            }
        }

        public bool Send(byte[] data)
        {
            if (Connected)
            {
                return SendMessage(client.GetStream(), data);
            }
            Logger.LogWarning("Client.Send: not connected!");
            return false;
        }
    }
}
