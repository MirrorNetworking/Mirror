using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Mirror
{
    /// <summary>Network Reader for most simple types like floats, ints, buffers, structs, etc. Use NetworkReaderPool.GetReader() to avoid allocations.</summary>
    // Note: This class is intended to be extremely pedantic,
    // and throw exceptions whenever stuff is going slightly wrong.
    // The exceptions will be handled in NetworkServer/NetworkClient.
    //
    // Note that NetworkWriter can be passed in constructor thanks to implicit
    // ArraySegment conversion:
    //   NetworkReader reader = new NetworkReader(writer);
    public class NetworkReader
    {
        // internal buffer
        // byte[] pointer would work, but we use ArraySegment to also support
        // the ArraySegment constructor
        internal ArraySegment<byte> buffer;

        /// <summary>Next position to read from the buffer</summary>
        // 'int' is the best type for .Position. 'short' is too small if we send >32kb which would result in negative .Position
        // -> converting long to int is fine until 2GB of data (MAX_INT), so we don't have to worry about overflows here
        public int Position;

        /// <summary>Remaining bytes that can be read, for convenience.</summary>
        public int Remaining => buffer.Count - Position;

        /// <summary>Total buffer capacity, independent of reader position.</summary>
        public int Capacity => buffer.Count;

        // cache encoding for ReadString instead of creating it with each time
        // 1000 readers before:  1MB GC, 30ms
        // 1000 readers after: 0.8MB GC, 18ms
        // member(!) to avoid static state.
        //
        // throwOnInvalidBytes is true.
        // if false, it would silently ignore the invalid bytes but continue
        // with the valid ones, creating strings like "a�������".
        // instead, we want to catch it manually and return String.Empty.
        // this is safer. see test: ReadString_InvalidUTF8().
        internal readonly UTF8Encoding encoding = new UTF8Encoding(false, true);

        public NetworkReader(ArraySegment<byte> segment)
        {
            buffer = segment;
        }

#if !UNITY_2021_3_OR_NEWER
        // Unity 2019 doesn't have the implicit byte[] to segment conversion yet
        public NetworkReader(byte[] bytes)
        {
            buffer = new ArraySegment<byte>(bytes, 0, bytes.Length);
        }
#endif

        // sometimes it's useful to point a reader on another buffer instead of
        // allocating a new reader (e.g. NetworkReaderPool)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBuffer(ArraySegment<byte> segment)
        {
            buffer = segment;
            Position = 0;
        }

#if !UNITY_2021_3_OR_NEWER
        // Unity 2019 doesn't have the implicit byte[] to segment conversion yet
        public void SetBuffer(byte[] bytes)
        {
            buffer = new ArraySegment<byte>(bytes, 0, bytes.Length);
            Position = 0;
        }
#endif

        // ReadBlittable<T> from DOTSNET
        // this is extremely fast, but only works for blittable types.
        // => private to make sure nobody accidentally uses it for non-blittable
        //
        // Benchmark: see NetworkWriter.WriteBlittable!
        //
        // Note:
        //   ReadBlittable assumes same endianness for server & client.
        //   All Unity 2018+ platforms are little endian.
        //
        // This is not safe to expose to random structs.
        //   * StructLayout.Sequential is the default, which is safe.
        //     if the struct contains a reference type, it is converted to Auto.
        //     but since all structs here are unmanaged blittable, it's safe.
        //     see also: https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.layoutkind?view=netframework-4.8#system-runtime-interopservices-layoutkind-sequential
        //   * StructLayout.Pack depends on CPU word size.
        //     this may be different 4 or 8 on some ARM systems, etc.
        //     this is not safe, and would cause bytes/shorts etc. to be padded.
        //     see also: https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.structlayoutattribute.pack?view=net-6.0
        //   * If we force pack all to '1', they would have no padding which is
        //     great for bandwidth. but on some android systems, CPU can't read
        //     unaligned memory.
        //     see also: https://github.com/vis2k/Mirror/issues/3044
        //   * The only option would be to force explicit layout with multiples
        //     of word size. but this requires lots of weaver checking and is
        //     still questionable (IL2CPP etc.).
        //
        // Note: inlining ReadBlittable is enough. don't inline ReadInt etc.
        //       we don't want ReadBlittable to be copied in place everywhere.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe T ReadBlittable<T>()
            where T : unmanaged
        {
            // check if blittable for safety
#if UNITY_EDITOR
            if (!UnsafeUtility.IsBlittable(typeof(T)))
            {
                throw new ArgumentException($"{typeof(T)} is not blittable!");
            }
#endif

            // calculate size
            //   sizeof(T) gets the managed size at compile time.
            //   Marshal.SizeOf<T> gets the unmanaged size at runtime (slow).
            // => our 1mio writes benchmark is 6x slower with Marshal.SizeOf<T>
            // => for blittable types, sizeof(T) is even recommended:
            // https://docs.microsoft.com/en-us/dotnet/standard/native-interop/best-practices
            int size = sizeof(T);

            // ensure remaining
            if (Remaining < size)
            {
                throw new EndOfStreamException($"ReadBlittable<{typeof(T)}> not enough data in buffer to read {size} bytes: {ToString()}");
            }

            // read blittable
            T value;
            fixed (byte* ptr = &buffer.Array[buffer.Offset + Position])
            {
#if UNITY_ANDROID
                // on some android systems, reading *(T*)ptr throws a NRE if
                // the ptr isn't aligned (i.e. if Position is 1,2,3,5, etc.).
                // here we have to use memcpy.
                //
                // => we can't get a pointer of a struct in C# without
                //    marshalling allocations
                // => instead, we stack allocate an array of type T and use that
                // => stackalloc avoids GC and is very fast. it only works for
                //    value types, but all blittable types are anyway.
                //
                // this way, we can still support blittable reads on android.
                // see also: https://github.com/vis2k/Mirror/issues/3044
                // (solution discovered by AIIO, FakeByte, mischa)
                T* valueBuffer = stackalloc T[1];
                UnsafeUtility.MemCpy(valueBuffer, ptr, size);
                value = valueBuffer[0];
#else
                // cast buffer to a T* pointer and then read from it.
                value = *(T*)ptr;
#endif
            }
            Position += size;
            return value;
        }

        // blittable'?' template for code reuse
        // note: bool isn't blittable. need to read as byte.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T? ReadBlittableNullable<T>()
            where T : unmanaged =>
            ReadByte() != 0 ? ReadBlittable<T>() : default(T?);

        public byte ReadByte() => ReadBlittable<byte>();

        /// <summary>Read 'count' bytes into the bytes array</summary>
        // NOTE: returns byte[] because all reader functions return something.
        public byte[] ReadBytes(byte[] bytes, int count)
        {
            // user may call ReadBytes(ReadInt()). ensure positive count.
            if (count < 0) throw new ArgumentOutOfRangeException("ReadBytes requires count >= 0");

            // check if passed byte array is big enough
            if (count > bytes.Length)
            {
                throw new EndOfStreamException($"ReadBytes can't read {count} + bytes because the passed byte[] only has length {bytes.Length}");
            }
            // ensure remaining
            if (Remaining < count)
            {
                throw new EndOfStreamException($"ReadBytesSegment can't read {count} bytes because it would read past the end of the stream. {ToString()}");
            }

            Array.Copy(buffer.Array, buffer.Offset + Position, bytes, 0, count);
            Position += count;
            return bytes;
        }

        /// <summary>Read 'count' bytes allocation-free as ArraySegment that points to the internal array.</summary>
        public ArraySegment<byte> ReadBytesSegment(int count)
        {
            // user may call ReadBytes(ReadInt()). ensure positive count.
            if (count < 0) throw new ArgumentOutOfRangeException("ReadBytesSegment requires count >= 0");

            // ensure remaining
            if (Remaining < count)
            {
                throw new EndOfStreamException($"ReadBytesSegment can't read {count} bytes because it would read past the end of the stream. {ToString()}");
            }

            // return the segment
            ArraySegment<byte> result = new ArraySegment<byte>(buffer.Array, buffer.Offset + Position, count);
            Position += count;
            return result;
        }

        /// <summary>Reads any data type that mirror supports. Uses weaver populated Reader(T).read</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T>()
        {
            Func<NetworkReader, T> readerDelegate = Reader<T>.read;
            if (readerDelegate == null)
            {
                Debug.LogError($"No reader found for {typeof(T)}. Use a type supported by Mirror or define a custom reader extension for {typeof(T)}.");
                return default;
            }
            return readerDelegate(this);
        }

        // print the full buffer with position / capacity.
        public override string ToString() =>
            $"[{buffer.ToHexString()} @ {Position}/{Capacity}]";
    }

    /// <summary>Helper class that weaver populates with all reader types.</summary>
    // Note that c# creates a different static variable for each type
    // -> Weaver.ReaderWriterProcessor.InitializeReaderAndWriters() populates it
    public static class Reader<T>
    {
        public static Func<NetworkReader, T> read;
    }
}
