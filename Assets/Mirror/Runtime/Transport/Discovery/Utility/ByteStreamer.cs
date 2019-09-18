using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Assets.Scripts.Utility.Serialisation
{
    // I have not included all my streaming utilities as I have encyrption, CLZF2 compression and a custom binary formatter that handles Unity vectors.
    // I wanted to keep the sample small!
    static class ByteStreamer
    {
        public static byte[] StreamToBytes(Object target)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                binaryFormatter.Serialize(memoryStream, target);
                return memoryStream.ToArray();
            }
        }

        public static object StreamFromBytes(byte[] data)
        {
            using (MemoryStream memoryStream = new MemoryStream(data))
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                return binaryFormatter.Deserialize(memoryStream);
            }
        }
    }
}
