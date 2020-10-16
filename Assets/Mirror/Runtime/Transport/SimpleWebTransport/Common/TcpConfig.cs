using System.Net.Sockets;

namespace Mirror.SimpleWeb
{
    public struct TcpConfig
    {
        public readonly bool noDelay;
        public readonly int sendTimeout;
        public readonly int receiveTimeout;

        public TcpConfig(bool noDelay, int sendTimeout, int receiveTimeout)
        {
            this.noDelay = noDelay;
            this.sendTimeout = sendTimeout;
            this.receiveTimeout = receiveTimeout;
        }

        public void ApplyTo(TcpClient client)
        {
            client.SendTimeout = sendTimeout;
            client.ReceiveTimeout = receiveTimeout;
            client.NoDelay = noDelay;
        }
    }
}
