using System;
using System.IO;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace Mirror.Transports.Encryption
{
    public class EncryptionCredentials
    {
        const int PrivateKeyBits = 256;
        public ECPublicKeyParameters PublicKey;
        public byte[] PublicKeySerialized;
        public ECPrivateKeyParameters PrivateKey;

        EncryptionCredentials() {}

        public static EncryptionCredentials Load(string path)
        {
            throw new NotImplementedException();
        }

        public static EncryptionCredentials Generate()
        {
            var generator = new ECKeyPairGenerator();
            generator.Init(new KeyGenerationParameters(new SecureRandom(), PrivateKeyBits));
            AsymmetricCipherKeyPair keyPair = generator.GenerateKeyPair();

            return new EncryptionCredentials
            {
                PublicKey = (ECPublicKeyParameters)keyPair.Public,
                PublicKeySerialized = SerializePublicKey((ECPublicKeyParameters)keyPair.Public),
                PrivateKey = (ECPrivateKeyParameters)keyPair.Private
            };
        }

        public static byte[] SerializePublicKey(ECPublicKeyParameters publicKey)
        {
            SubjectPublicKeyInfo publicKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey);
            return publicKeyInfo.ToAsn1Object().GetDerEncoded();
        }

        public static AsymmetricKeyParameter DeserializePublicKey(ArraySegment<byte> pubKey)
        {
            return PublicKeyFactory.CreateKey(new MemoryStream(pubKey.Array, pubKey.Offset, pubKey.Count, false));
        }
    }
}
