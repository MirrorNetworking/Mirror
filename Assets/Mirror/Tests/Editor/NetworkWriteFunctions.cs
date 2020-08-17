using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Mirror.Tests
{
    public static class NetworkFunctions
    {
        static readonly Dictionary<int, Delegate> writers = new Dictionary<int, Delegate>();
        static readonly Dictionary<int, Delegate> readers = new Dictionary<int, Delegate>();

        public delegate void Write<T>(NetworkWriter writer, T value);
        public delegate T Read<T>(NetworkReader reader);

        public static Write<T> GetWriter<T>()
        {
            int hash = typeof(T).FullName.GetStableHashCode();

            Delegate write = writers[hash];

            return write as Write<T>;
        }
        public static Read<T> GetReader<T>()
        {
            int hash = typeof(T).FullName.GetStableHashCode();

            Delegate read = readers[hash];

            return read as Read<T>;
        }

        static NetworkFunctions()
        {
            // Weaver would populate this with all writers
            int hash1 = getHash<int>();
            Write<int> write1 = NetworkWriterExtensions.WritePackedInt32;
            writers.Add(hash1, write1);
            Read<int> read1 = NetworkReaderExtensions.ReadPackedInt32;
            readers.Add(hash1, read1);

            int hash2 = getHash<float>();
            Write<float> write2 = NetworkWriterExtensions.WriteSingle;
            writers.Add(hash2, write2);
            Read<float> read2 = NetworkReaderExtensions.ReadSingle;
            readers.Add(hash2, read2);
        }
        static int getHash<T>()
        {
            return typeof(T).FullName.GetStableHashCode();
        }
    }

    public struct GenericMessage<T> : IMessageBase
    {
        static readonly NetworkFunctions.Write<T> write = NetworkFunctions.GetWriter<T>();
        static readonly NetworkFunctions.Read<T> read = NetworkFunctions.GetReader<T>();

        static GenericMessage()
        {
            if (write == null)
            {
                UnityEngine.Debug.LogError($"Could not find writer for {typeof(T).FullName}");
            }

            if (read == null)
            {
                UnityEngine.Debug.LogError($"Could not find read for {typeof(T).FullName}");
            }
        }


        public T someValue;

        public void Deserialize(NetworkReader reader)
        {
            someValue = read.Invoke(reader);
        }
        public void Serialize(NetworkWriter writer)
        {
            write.Invoke(writer, someValue);
        }
    }

    public class NetworkFunctionsTest
    {
        [Test]
        public void GenericValuesHaveDifferentHashs()
        {
            int hash1 = typeof(GenericMessage<int>).FullName.GetStableHashCode();
            int hash2 = typeof(GenericMessage<float>).FullName.GetStableHashCode();

            Assert.That(hash1, Is.Not.EqualTo(hash2));
        }
        [Test]
        public void GetsWriterForInt()
        {
            const int value = 10;
            GenericMessage<int> intMessage = new GenericMessage<int>
            {
                someValue = value
            };

            byte[] data = MessagePacker.Pack(intMessage);

            GenericMessage<int> unpacked = MessagePacker.Unpack<GenericMessage<int>>(data);

            Assert.That(unpacked.someValue, Is.EqualTo(value));
        }

        [Test]
        public void GetsWriterForBool()
        {
            const float value = 24.3f;
            GenericMessage<float> intMessage = new GenericMessage<float>
            {
                someValue = value
            };

            byte[] data = MessagePacker.Pack(intMessage);

            GenericMessage<float> unpacked = MessagePacker.Unpack<GenericMessage<float>>(data);

            Assert.That(unpacked.someValue, Is.EqualTo(value));
        }
    }
}
