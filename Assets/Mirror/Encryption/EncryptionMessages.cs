namespace Mirror.Encrpytion
{
    class PublicKeyRequestMessage : MessageBase
    {
    }
    class PublicKeyResponseMessage : MessageBase
    {
        public PublicKey publicKey;
    }
    class EncryptedMessage : MessageBase
    {
        public bool encrypted;
        public byte[] data;
    }
    struct PublicKey
    {
        public byte[] modulus;
        public byte[] exponent;
    }
}
