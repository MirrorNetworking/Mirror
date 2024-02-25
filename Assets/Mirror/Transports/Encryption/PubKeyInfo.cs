using System;
using Org.BouncyCastle.Crypto;

public struct PubKeyInfo
{
    public string Fingerprint;
    public ArraySegment<byte> Serialized;
    public AsymmetricKeyParameter Key;
}
