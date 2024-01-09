using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Mirror.SimpleWeb;
using UnityEngine;

namespace Mirror
{
    public class TransportSecurity : MonoBehaviour, ICreateStream
    {
        public SSLSettings sslSettings;
        public CertificateSettings certificateSettings;
        public SSLSettings GetSslSettings()
        {
            return sslSettings;
        }
        public bool TryCreateStream(IConnection conn)
        {
            NetworkStream stream = conn.Client.GetStream();
            if (sslSettings.SSLEnabled)
            {
                try
                {
                    conn.Stream = CreateStream(stream);
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SWT-ServerSslHelper]: Create SSLStream Failed: {e.Message}");
                    return false;
                }
            }

            conn.Stream = stream;
            return true;
        }
        public Stream CreateStream(NetworkStream stream)
        {
            if (!sslSettings.SSLEnabled)
            {
                Debug.LogError("SSL is not enabled. Unable to create stream.");
                return null;
            }

            SslStream sslStream = new SslStream(stream, true, AcceptClient);
            sslStream.AuthenticateAsServer(certificateSettings.Certificate, false, sslSettings.SSLProtocol, false);

            return sslStream;
        }

        // Always accept client
        private bool AcceptClient(object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
