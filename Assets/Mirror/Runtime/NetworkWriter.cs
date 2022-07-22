using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Mirror
{
    /// <summary>Network Writer for most simple types like floats, ints, buffers, structs, etc. Use NetworkWriterPool.GetReader() to avoid allocations.</summary>
    public class NetworkWriter
    {
        public const int MaxStringLength = 1024 * 32;

        // create writer immediately with it's own buffer so no one can mess with it and so that we can resize it.
        // note: BinaryWriter allocates too much, so we only use a MemoryStream
        // => 1500 bytes by default because on average, most packets will be <= MTU
        internal byte[] buffer = new byte[1500];

        /// <summary>Next position to write to the buffer</summary>
        public int Position;

        /// <summary>Reset both the position and length of the stream</summary>
        // Leaves the capacity the same so that we can reuse this writer without
        // extra allocations
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            Position = 0;
        }

        // NOTE that our runtime resizing comes at no extra cost because:
        // 1. 'has space' checks are necessary even for fixed sized writers.
        // 2. all writers will eventually be large enough to stop resizing.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnsureCapacity(int value)
        {
            if (buffer.Length < value)
            {
                int capacity = Math.Max(value, buffer.Length * 2);
                Array.Resize(ref buffer, capacity);
            }
        }

        /// <summary>Copies buffer until 'Position' to a new array.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ToArray()
        {
            byte[] data = new byte[Position];
            Array.ConstrainedCopy(buffer, 0, data, 0, Position);
            return data;
        }

        /// <summary>Returns allocation-free ArraySegment until 'Position'.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<byte> ToArraySegment()
        {
            return new ArraySegment<byte>(buffer, 0, Position);
        }

        // WriteBlittable<T> from DOTSNET.
        // this is extremely fast, but only works for blittable types.
        //
        // Benchmark:
        //   WriteQuaternion x 100k, Macbook Pro 2015 @ 2.2Ghz, Unity 2018 LTS (debug mode)
        //
        //                | Median |  Min  |  Max  |  Avg  |  Std  | (ms)
        //     before     |  30.35 | 29.86 | 48.99 | 32.54 |  4.93 |
        //     blittable* |   5.69 |  5.52 | 27.51 |  7.78 |  5.65 |
        //
        //     * without IsBlittable check
        //     => 4-6x faster!
        //
        //   WriteQuaternion x 100k, Macbook Pro 2015 @ 2.2Ghz, Unity 2020.1 (release mode)
        //
        //                | Median |  Min  |  Max  |  Avg  |  Std  | (ms)
        //     before     |   9.41 |  8.90 | 23.02 | 10.72 |  3.07 |
        //     blittable* |   1.48 |  1.40 | 16.03 |  2.60 |  2.71 |
        //
        //     * without IsBlittable check
        //     => 6x faster!
        //
        // Note:
        //   WriteBlittable assumes same endianness for server & client.
        //   All Unity 2018+ platforms are little endian.
        //   => run NetworkWriterTests.BlittableOnThisPlatform() to verify!
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
        // Note: inlining WriteBlittable is enough. don't inline WriteInt etc.
        //       we don't want WriteBlittable to be copied in place everywhere.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void WriteBlittable<T>(T value)
            where T : unmanaged
        {
            // check if blittable for safety
#if UNITY_EDITOR
            if (!UnsafeUtility.IsBlittable(typeof(T)))
            {
                Debug.LogError($"{typeof(T)} is not blittable!");
                return;
            }
#endif
            // calculate size
            //   sizeof(T) gets the managed size at compile time.
            //   Marshal.SizeOf<T> gets the unmanaged size at runtime (slow).
            // => our 1mio writes benchmark is 6x slower with Marshal.SizeOf<T>
            // => for blittable types, sizeof(T) is even recommended:
            // https://docs.microsoft.com/en-us/dotnet/standard/native-interop/best-practices
            int size = sizeof(T);

            // ensure capacity
            // NOTE that our runtime resizing comes at no extra cost because:
            // 1. 'has space' checks are necessary even for fixed sized writers.
            // 2. all writers will eventually be large enough to stop resizing.
            EnsureCapacity(Position + size);

            // write blittable
            fixed (byte* ptr = &buffer[Position])
            {
#if UNITY_ANDROID
                // on some android systems, assigning *(T*)ptr throws a NRE if
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
                T* valueBuffer = stackalloc T[1]{value};
                UnsafeUtility.MemCpy(ptr, valueBuffer, size);
#else
                // cast buffer to T* pointer, then assign value to the area
                *(T*)ptr = value;
#endif
            }
            Position += size;
        }

        // blittable'?' template for code reuse
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteBlittableNullable<T>(T? value)
            where T : unmanaged
        {
            // bool isn't blittable. write as byte.
            WriteByte((byte)(value.HasValue ? 0x01 : 0x00));

            // only write value if exists. saves bandwidth.
            if (value.HasValue)
                WriteBlittable(value.Value);
        }

        public void WriteByte(byte value) => WriteBlittable(value);

        // for byte arrays with consistent size, where the reader knows how many to read
        // (like a packet opcode that's always the same)
        public void WriteBytes(byte[] buffer, int offset, int count)
        {
            EnsureCapacity(Position + count);
            Array.ConstrainedCopy(buffer, offset, this.buffer, Position, count);
            Position += count;
        }

        /// <summary>Writes any type that mirror supports. Uses weaver populated Writer(T).write.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(T value)
        {
            Action<NetworkWriter, T> writeDelegate = Writer<T>.write;
            if (writeDelegate == null)
            {
                Debug.LogError($"No writer found for {typeof(T)}. This happens either if you are missing a NetworkWriter extension for your custom type, or if weaving failed. Try to reimport a script to weave again.");
            }
            else
            {
                writeDelegate(this, value);
            }
        }
    }

    /// <summary>Helper class that weaver populates with all writer types.</summary>
    // Note that c# creates a different static variable for each type
    // -> Weaver.ReaderWriterProcessor.InitializeReaderAndWriters() populates it
    public static class Writer<T>
    {
        public static Action<NetworkWriter, T> write;
    }
}
