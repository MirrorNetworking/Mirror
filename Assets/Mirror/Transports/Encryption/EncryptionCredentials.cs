using System;
using System.IO;
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
        // don't actually need to store this currently
        // but we'll need to for loading/saving from file maybe?
        // public ECPublicKeyParameters PublicKey;

        // The serialized public key, in DER format
        public byte[] PublicKeySerialized;
        public ECPrivateKeyParameters PrivateKey;

        EncryptionCredentials() {}

        // TODO: load from file
        public static EncryptionCredentials Generate()
        {
            var generator = new ECKeyPairGenerator();
            generator.Init(new KeyGenerationParameters(new SecureRandom(), PrivateKeyBits));
            AsymmetricCipherKeyPair keyPair = generator.GenerateKeyPair();

            return new EncryptionCredentials
            {
                // see above
                // PublicKey = (ECPublicKeyParameters)keyPair.Public,
                PublicKeySerialized = SerializePublicKey((ECPublicKeyParameters)keyPair.Public),
                PrivateKey = (ECPrivateKeyParameters)keyPair.Private
            };
        }

        public static byte[] SerializePublicKey(ECPublicKeyParameters publicKey)
        {
            // apparently the best way to transmit this public key over the network is to serialize it as a DER
            SubjectPublicKeyInfo publicKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey);
            return publicKeyInfo.ToAsn1Object().GetDerEncoded();
        }

        public static AsymmetricKeyParameter DeserializePublicKey(ArraySegment<byte> pubKey)
        {
            // And then we do this to deserialize from the DER (from above)
            // the "new MemoryStream" actually saves an allocation, since otherwise the ArraySegment would be converted
            // to a byte[] first and then shoved through a MemoryStream
            return PublicKeyFactory.CreateKey(new MemoryStream(pubKey.Array, pubKey.Offset, pubKey.Count, false));
        }
    }
}
