using System;
using System.Security.Authentication;
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
    }
}
