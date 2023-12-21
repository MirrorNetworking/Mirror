using System;
using System.IO;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

namespace Mirror
{
    public class Certificates : MonoBehaviour
    {
        public CertificateSettings certificateSettings;
    }

    [Serializable]
    public class CertificateSettings
    {
        [Tooltip("Enable SSL for Mirror Transport components? (default: false)")]
        public bool SSLEnabled;

        [Tooltip("Protocol to use for the certificate (default: TLS 1.2)")]
        public SslProtocols CertificateProtocol = SslProtocols.Tls12;

        [Tooltip("Path to the certificate file (pass in either a relative path or an absolute path)")]
        public string CertificatePath = "";

        [Tooltip("Is the certificate private key file password protected? (default: false)")]
        public bool CertificatePasswordProtected;

        [Tooltip("Path to a file that just contains the password for the certificate file (pass in either a relative path or an absolute path)")]
        public string PasswordFilePath = "";

        public X509Certificate2 Certificate
        {
            get
            {
                if (SSLEnabled == false)
                {
                    Debug.LogError("SSL is not enabled but a certificate is being requested");
                    return null;
                }
                if (CertificatePasswordProtected)
                {
                    return NewPasswordProtectedCertificate();
                }
                return NewCertificate();
            }
        }

        X509Certificate2 NewPasswordProtectedCertificate()
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

        X509Certificate2 NewCertificate()
        {
            if (!ValidateCertificatePath(CertificatePath))
            {
                Debug.LogError("Certificate path is invalid (" + CertificatePath + "). Unable to create certificate.");
                return null;
            }
            return new X509Certificate2(CertificatePath);
        }

        static bool ValidatePasswordFilePath(string passwordFilePath)
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

        static bool ValidateCertificatePath(string certificatePath)
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
