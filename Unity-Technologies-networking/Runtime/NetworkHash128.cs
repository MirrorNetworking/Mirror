#if ENABLE_UNET
using System;

namespace UnityEngine.Networking
{
    // vis2k: NetworkHash128 explanation
    // -> AssetDatabase.AssetPathToGUID returns a 32 length hex string
    // -> we take that string and save it as 16 bytes internally, so that we
    //    don't have to convert it each time we send / receive it over the network.
    // -> Unity already has a 'Hash128' type that would be a perfect fit, but
    //    NetworkWriter can't access it's internal bytes AND it isn't serialized
    //    (hence saved) in prefabs at all.
    // -> Using a size 16 byte[] array seems obvious, but unfolding it with 16
    //    separate bytes is a whole lot better:
    //    + struct.Equals works by default, would have to overwrite it for byte[]
    //    + uninitialized NetworkHash128 is always exactly 0,0,0,0...
    //      but for byte[] it could be 'null', '[]', or '[0,0,0,...]', which adds
    //      way too many edge cases when comparing or sending values
    //    + NetworkWriter/Reader always know the exact size in advance. for
    //      'null' or '[]' support we'd need WriteBytesAndSize, hence
    //      size+isNull = 3 bytes extra bandwidth each time
    //    + using byte[] would also break all prefab's serializations. this is
    //      never good, especially when going back and forth between original
    //      HLAPI and this one
    //  => this solution is actually very decent
    [Serializable]
    public struct NetworkHash128
    {
        public byte i0;
        public byte i1;
        public byte i2;
        public byte i3;
        public byte i4;
        public byte i5;
        public byte i6;
        public byte i7;
        public byte i8;
        public byte i9;
        public byte i10;
        public byte i11;
        public byte i12;
        public byte i13;
        public byte i14;
        public byte i15;

        public bool IsValid()
        {
            return (i0 | i1 | i2 | i3 | i4 | i5 | i6 | i7 | i8 | i9 | i10 | i11 | i12 | i13 | i14 | i15) != 0;
        }

        // convert 32 length hex string like "042acbef..." to byte array
        // (each byte is represented with 2 characters, e.g. 0xFF => "FF")
        public static NetworkHash128 Parse(string text)
        {
            // AssetDatabase.AssetPathToGUID always returns 32 length strings
            if (text.Length != 32)
            {
                Debug.LogError("NetworkHash128.Parse expects a 32 characters long Guid instead of: " + text);
                return new NetworkHash128();
            }

            NetworkHash128 hash;
            hash.i0 = Convert.ToByte(text.Substring(0, 2), 16);
            hash.i1 = Convert.ToByte(text.Substring(2, 2), 16);
            hash.i2 = Convert.ToByte(text.Substring(4, 2), 16);
            hash.i3 = Convert.ToByte(text.Substring(6, 2), 16);
            hash.i4 = Convert.ToByte(text.Substring(8, 2), 16);
            hash.i5 = Convert.ToByte(text.Substring(10, 2), 16);
            hash.i6 = Convert.ToByte(text.Substring(12, 2), 16);
            hash.i7 = Convert.ToByte(text.Substring(14, 2), 16);
            hash.i8 = Convert.ToByte(text.Substring(16, 2), 16);
            hash.i9 = Convert.ToByte(text.Substring(18, 2), 16);
            hash.i10 = Convert.ToByte(text.Substring(20, 2), 16);
            hash.i11 = Convert.ToByte(text.Substring(22, 2), 16);
            hash.i12 = Convert.ToByte(text.Substring(24, 2), 16);
            hash.i13 = Convert.ToByte(text.Substring(26, 2), 16);
            hash.i14 = Convert.ToByte(text.Substring(28, 2), 16);
            hash.i15 = Convert.ToByte(text.Substring(30, 2), 16);

            return hash;
        }

        public override string ToString()
        {
            return String.Format("{0:x2}{1:x2}{2:x2}{3:x2}{4:x2}{5:x2}{6:x2}{7:x2}{8:x2}{9:x2}{10:x2}{11:x2}{12:x2}{13:x2}{14:x2}{15:x2}",
                i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15);
        }
    }
}
#endif //ENABLE_UNET
