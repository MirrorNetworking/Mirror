using System;
using Mirror.BouncyCastle.Crypto;

namespace Mirror.Transports.Encryption
{
    public struct PubKeyInfo
    {
        public string Fingerprint;
        public ArraySegment<byte> Serialized;
        public AsymmetricKeyParameter Key;
    }
}
