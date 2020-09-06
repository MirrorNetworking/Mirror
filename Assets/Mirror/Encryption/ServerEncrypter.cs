using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

namespace Mirror.Encryption
{
    public class ServerEncrypter
    {
        public event Action<byte[]> OnEncryptedMessage;

        readonly X509Certificate2 certificate;

        static ServerEncrypter instance;
        public static ServerEncrypter Instance => (instance = instance ?? new ServerEncrypter());
        private ServerEncrypter()
        {
            NetworkServer.RegisterHandler<PublicKeyRequestMessage>(PublicKeyRequestHandler, requireAuthentication: false);
            NetworkServer.RegisterHandler<EncryptedMessage>(EncryptedMessageHandler, requireAuthentication: false);

            try
            {
                /*
                To create X509Certificate2 file use:

                makecert -r -pe -n "CN=MIRROR_TEST_CERT" -sv ./mirrorTest.pvk -sky exchange ./mirrorTest.cer

                pvk2pfx.exe -pvk mirrorTest.pvk -spc mirrorTest.cer -pfx mirrorTest.pfx

                See:
                https://docs.microsoft.com/en-us/windows/win32/seccrypto/makecert
                https://www.codeproject.com/Articles/18601/An-easy-way-to-use-certificates-for-WCF-security
                https://docs.microsoft.com/en-us/windows-hardware/drivers/devtest/pvk2pfx
                 */
                certificate = new X509Certificate2("./certs/mirrorTest.pfx", "");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void PublicKeyRequestHandler(NetworkConnection conn, PublicKeyRequestMessage msg)
        {
            RSA publicKey = certificate.GetRSAPublicKey();
            RSAParameters parameters = publicKey.ExportParameters(false);

            conn.Send(new PublicKeyResponseMessage
            {
                publicKey = new PublicKey
                {
                    modulus = parameters.Modulus,
                    exponent = parameters.Exponent,
                }
            });
        }

        void EncryptedMessageHandler(NetworkConnection conn, EncryptedMessage msg)
        {
            if (msg.encrypted)
            {
                RSA privateKey = certificate.GetRSAPrivateKey();

                byte[] decrypted = privateKey.Decrypt(msg.data, RSAEncryptionPadding.Pkcs1);

                OnEncryptedMessage?.Invoke(decrypted);
            }
            else
            {
                OnEncryptedMessage?.Invoke(msg.data);
            }
        }
    }
}
