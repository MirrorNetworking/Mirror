using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

namespace Mirror
{
    public class Certificate : MonoBehaviour
    {
        public SSLSettings sslSettings;
        public CertificateSettings certificateSettings;
    }

    [Serializable]
    public class SSLSettings
    {
        [Tooltip("Enable SSL for mirror transport components? (default: false)")]
        public bool SSLEnabled;

        [Tooltip("Protocol to use for ssl (default: TLS 1.2)")]
        public SslProtocols SSLProtocol = SslProtocols.Tls12;

        public Stream CreateStream(NetworkStream stream, X509Certificate2 certificate)
        {
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

    [Serializable]
    public class CertificateSettings
    {
        [Tooltip("Path to the certificate file (pass in either a relative path or an absolute path)")]
        public string CertificatePath = "";

        [Tooltip("Is the certificate private key file password protected? (default: false)")]
        public bool CertificatePasswordProtected;

        [Tooltip(
            "Path to a file that just contains the password for the certificate file (pass in either a relative path or an absolute path)")]
        public string PasswordFilePath = "";

        public X509Certificate2 Certificate
        {
            get
            {
                if (CertificatePasswordProtected)
                {
                    return NewPasswordProtectedCertificate();
                }
                return NewCertificate();
            }
        }

        private X509Certificate2 NewPasswordProtectedCertificate()
        {
            if (!ValidateCertificatePath(CertificatePath))
            {
                Debug.LogError("Certificate path is invalid (" + CertificatePath + "). Unable to create certificate.");
                return null;
            }
            if (!ValidatePasswordFilePath(PasswordFilePath))
            {
                Debug.LogError("Password file path is invalid (" + PasswordFilePath + "). Unable to create certificate.");
                return null;
            }
            string password = File.ReadAllText(PasswordFilePath);
            return new X509Certificate2(CertificatePath, password);
        }

        private X509Certificate2 NewCertificate()
        {
            if (!ValidateCertificatePath(CertificatePath))
            {
                Debug.LogError("Certificate path is invalid (" + CertificatePath + "). Unable to create certificate.");
                return null;
            }
            return new X509Certificate2(CertificatePath);
        }

        private static bool ValidatePasswordFilePath(string passwordFilePath)
        {
            if (string.IsNullOrEmpty(passwordFilePath))
            {
                Debug.LogError("Password file path is empty (" + passwordFilePath + ")");
                return false;
            }
            string passwordFullPath = Path.GetFullPath(passwordFilePath);
            if (!File.Exists(passwordFilePath))
            {
                Debug.LogError("Password file does not exist (" + passwordFilePath + ")");
                return false;
            }
            return true;
        }

        private static bool ValidateCertificatePath(string certificatePath)
        {
            if (string.IsNullOrEmpty(certificatePath))
            {
                Debug.LogError("Certificate path is empty (" + certificatePath + ")");
                return false;
            }
            string certFullPath = Path.GetFullPath(certificatePath);
            if (!File.Exists(certFullPath))
            {
                Debug.LogError("Certificate file does not exist (" + certFullPath + ")");
                return false;
            }
            string certExtension = Path.GetExtension(certFullPath);
            if (certExtension != ".cer" || certExtension != ".crt" || certExtension != ".pfx")
            {
                Debug.LogError("Certificate file is not a valid certificate file (" + certFullPath + ")");
                return false;
            }
            return true;
        }
    }
}
