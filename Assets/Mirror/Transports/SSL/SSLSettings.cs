using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [Serializable]
    public class SSLSettings
    {
        [Tooltip("Enable SSL for mirror transport components? (default: false)")]
        public bool SSLEnabled;

        [Tooltip("Protocol to use for ssl (default: TLS 1.2)")]
        public SslProtocols SSLProtocol = SslProtocols.Tls12;

        public Stream CreateStream(NetworkStream stream, X509Certificate2 certificate)
        {
            if (!SSLEnabled)
            {
                Debug.LogError("SSL is not enabled. Unable to create stream.");
                return null;
            }

            SslStream sslStream = new(stream, true, AcceptClient);
            sslStream.AuthenticateAsServer(certificate, false, SSLProtocol, false);

            return sslStream;
        }

        // Always accept client
        private bool AcceptClient(object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }

#if UNITY_EDITOR
    // [CustomPropertyDrawer(typeof(SSLSettings))]
    public class SSLSettingsDrawer: PropertyDrawer
    {}
#endif
}
