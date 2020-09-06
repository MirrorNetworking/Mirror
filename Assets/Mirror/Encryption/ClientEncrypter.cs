using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

namespace Mirror.Encryption
{
    public class ClientEncrypter
    {
        PublicKey? publicKeyParameters;

        static ClientEncrypter instance;
        public static ClientEncrypter Instance => (instance = instance ?? new ClientEncrypter());
        private ClientEncrypter()
        {
            NetworkClient.RegisterHandler<PublicKeyResponseMessage>(PublicKeyResponseHandler, requireAuthentication: false);
        }

        void PublicKeyResponseHandler(NetworkConnection conn, PublicKeyResponseMessage msg)
        {
            if (msg.publicKey.exponent == null || msg.publicKey.modulus == null)
            {
                Debug.LogError("Invalid public key in Reponse Message");
                return;
            }

            publicKeyParameters = msg.publicKey;
        }

        /// <summary>
        /// Requests public key from server
        /// </summary>
        public void RequestPublicKey()
        {
            if (publicKeyParameters.HasValue)
            {
                Debug.LogWarning("Public key is already set");
                return;
            }

            NetworkClient.Send(new PublicKeyRequestMessage());
        }


        /// <summary>
        /// Sends data to server encrypted by their public key
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool EncryptSend(byte[] data)
        {
            if (!publicKeyParameters.HasValue)
            {
                Debug.LogError("Public key is not set call RequestPublicKeyFromServer first");
                return false;
            }

            using (RSA publicKey = getRSA())
            {
                byte[] encrypted = publicKey.Encrypt(data, RSAEncryptionPadding.Pkcs1);

                return NetworkClient.Send(new EncryptedMessage
                {
                    encrypted = true,
                    data = encrypted,
                });
            }
        }
        /// <summary>
        /// Sends data to server encrypted by their public key
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool Send(byte[] data)
        {
            return NetworkClient.Send(new EncryptedMessage
            {
                encrypted = false,
                data = data,
            });

        }

        private RSA getRSA()
        {
            //Create a new instance of the RSA class.  
            RSA rsa = RSA.Create();

            //Create a new instance of the RSAParameters structure.  
            RSAParameters rsaKeyInfo = new RSAParameters
            {
                //Set rsaKeyInfo to the public key values.
                Modulus = publicKeyParameters.Value.modulus,
                Exponent = publicKeyParameters.Value.exponent
            };

            //Import key parameters into rsa.  
            rsa.ImportParameters(rsaKeyInfo);
            return rsa;
        }
    }
}
