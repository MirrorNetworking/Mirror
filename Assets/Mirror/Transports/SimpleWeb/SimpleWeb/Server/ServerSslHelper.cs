using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Mirror.SimpleWeb
{
    public struct SslConfig
    {
        public readonly bool enabled;
        public readonly string certPath;
        public readonly string certPassword;
        public readonly SslProtocols sslProtocols;

        public SslConfig(bool enabled, string certPath, string certPassword, SslProtocols sslProtocols)
        {
            this.enabled = enabled;
            this.certPath = certPath;
            this.certPassword = certPassword;
            this.sslProtocols = sslProtocols;
        }
    }
    internal class ServerSslHelper
    {
        readonly SslConfig config;
        readonly X509Certificate2 certificate;

        public ServerSslHelper(SslConfig sslConfig)
        {
            Console.Clear();

            config = sslConfig;
            if (config.enabled)
            {
                certificate = new X509Certificate2(config.certPath, config.certPassword);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[SimpleWebTransport] SSL Certificate {certificate.Subject} loaded with expiration of {certificate.GetExpirationDateString()}");
                Console.ResetColor();
            }
        }

        internal bool TryCreateStream(Connection conn)
        {
            NetworkStream stream = conn.client.GetStream();
            if (config.enabled)
            {
                try
                {
                    conn.stream = CreateStream(stream);
                    return true;
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[SimpleWebTransport] Create SSLStream Failed: {e.Message}");
                    Console.ResetColor();

                    return false;
                }
            }
            else
            {
                conn.stream = stream;
                return true;
            }
        }

        Stream CreateStream(NetworkStream stream)
        {
            SslStream sslStream = new SslStream(stream, true, acceptClient);
            sslStream.AuthenticateAsServer(certificate, false, config.sslProtocols, false);

            return sslStream;
        }

        // always accept client
        bool acceptClient(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;
    }
}
