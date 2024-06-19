using System;
using System.IO;
using Mirror.BouncyCastle.Asn1.Pkcs;
using Mirror.BouncyCastle.Asn1.X509;
using Mirror.BouncyCastle.Crypto;
using Mirror.BouncyCastle.Crypto.Digests;
using Mirror.BouncyCastle.Crypto.Generators;
using Mirror.BouncyCastle.X509;
using Mirror.BouncyCastle.Crypto.Parameters;
using Mirror.BouncyCastle.Pkcs;
using Mirror.BouncyCastle.Security;
using UnityEngine;

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
        public string PublicKeyFingerprint;

        EncryptionCredentials() {}

        // TODO: load from file
        public static EncryptionCredentials Generate()
        {
            var generator = new ECKeyPairGenerator();
            generator.Init(new KeyGenerationParameters(new SecureRandom(), PrivateKeyBits));
            AsymmetricCipherKeyPair keyPair = generator.GenerateKeyPair();
            var serialized = SerializePublicKey((ECPublicKeyParameters)keyPair.Public);
            return new EncryptionCredentials
            {
                // see fields above
                // PublicKey = (ECPublicKeyParameters)keyPair.Public,
                PublicKeySerialized = serialized,
                PublicKeyFingerprint = PubKeyFingerprint(new ArraySegment<byte>(serialized)),
                PrivateKey = (ECPrivateKeyParameters)keyPair.Private
            };
        }

        public static byte[] SerializePublicKey(AsymmetricKeyParameter publicKey)
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

        public static byte[] SerializePrivateKey(AsymmetricKeyParameter privateKey)
        {
            // Serialize privateKey as a DER
            PrivateKeyInfo privateKeyInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(privateKey);
            return privateKeyInfo.ToAsn1Object().GetDerEncoded();
        }

        public static AsymmetricKeyParameter DeserializePrivateKey(ArraySegment<byte> privateKey)
        {
            // And then we do this to deserialize from the DER (from above)
            // the "new MemoryStream" actually saves an allocation, since otherwise the ArraySegment would be converted
            // to a byte[] first and then shoved through a MemoryStream
            return PrivateKeyFactory.CreateKey(new MemoryStream(privateKey.Array, privateKey.Offset, privateKey.Count, false));
        }

        public static string PubKeyFingerprint(ArraySegment<byte> publicKeyBytes)
        {
            Sha256Digest digest = new Sha256Digest();
            byte[] hash = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(publicKeyBytes.Array, publicKeyBytes.Offset, publicKeyBytes.Count);
            digest.DoFinal(hash, 0);

            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public void SaveToFile(string path)
        {
            string json = JsonUtility.ToJson(new SerializedPair
            {
                PublicKeyFingerprint = PublicKeyFingerprint,
                PublicKey = Convert.ToBase64String(PublicKeySerialized),
                PrivateKey= Convert.ToBase64String(SerializePrivateKey(PrivateKey)),
            });
            File.WriteAllText(path, json);
        }

        public static EncryptionCredentials LoadFromFile(string path)
        {
            string json = File.ReadAllText(path);
            SerializedPair serializedPair = JsonUtility.FromJson<SerializedPair>(json);

            byte[] publicKeyBytes =  Convert.FromBase64String(serializedPair.PublicKey);
            byte[] privateKeyBytes = Convert.FromBase64String(serializedPair.PrivateKey);

            if (serializedPair.PublicKeyFingerprint != PubKeyFingerprint(new ArraySegment<byte>(publicKeyBytes)))
            {
                throw new Exception("Saved public key fingerprint does not match public key.");
            }
            return new EncryptionCredentials
            {
                PublicKeySerialized = publicKeyBytes,
                PublicKeyFingerprint = serializedPair.PublicKeyFingerprint,
                PrivateKey = (ECPrivateKeyParameters) DeserializePrivateKey(new ArraySegment<byte>(privateKeyBytes))
            };
        }

        private class SerializedPair
        {
            public string PublicKeyFingerprint;
            public string PublicKey;
            public string PrivateKey;
        }
    }
}
