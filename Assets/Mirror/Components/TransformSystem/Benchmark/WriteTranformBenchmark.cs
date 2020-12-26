using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Mirror.TransformSyncing;
using UnityEngine;

namespace Mirror.Benchmark
{
    public class WriteTranformBenchmark : MonoBehaviour
    {
        private const int ObjectCount = 1000;
        Stopwatch sw = Stopwatch.StartNew();
        private PositionCompression compression;
        private Vector3[] positions;
        private Quaternion[] rotations;
        private Dictionary<uint, IHasPositionRotation> objects;
        private IHasPositionRotation[] objectsArray;
        private BitWriter bitWriter;
        private BitWriterUnsafeBuffer bitWriterBuffer;
        private QuaternionPacker packerLn9;
        private QuaternionPackerOptimized packerLn9op;
        private QuaternionPackerOptimizedManualInLine packerLn9opline;
        private QuaternionPacker packerLn10;

        struct FakeTranform : IHasPositionRotation
        {
            readonly uint id;
            readonly Vector3 position;
            readonly Quaternion rotation;

            public FakeTranform(uint id, Vector3 position, Quaternion rotation)
            {
                this.id = id;
                this.position = position;
                this.rotation = rotation;
            }

            public PositionRotation PositionRotation => new PositionRotation(position, rotation);

            public uint Id => id;

            public void ApplyOnClient(PositionRotation values)
            {
                throw new NotImplementedException();
            }

            public void ApplyOnServer(PositionRotation values)
            {
                throw new NotImplementedException();
            }

            public bool NeedsUpdate(float now)
            {
                return true;
            }
            public void ClearNeedsUpdate()
            {
            }
        }

        private void Start()
        {
            try
            {
                packerLn9 = new QuaternionPacker(9);
                packerLn9op = new QuaternionPackerOptimized(9);
                packerLn9opline = new QuaternionPackerOptimizedManualInLine(9);
                packerLn10 = new QuaternionPacker(10);
                bitWriter = new BitWriter();
                bitWriterBuffer = new BitWriterUnsafeBuffer(ObjectCount * 32);


                compression = new PositionCompression(Vector3.zero, new Vector3(200, 50, 200), 0.05f);
                objects = new Dictionary<uint, IHasPositionRotation>();
                objectsArray = new IHasPositionRotation[ObjectCount];
                positions = new Vector3[ObjectCount];
                rotations = new Quaternion[ObjectCount];
                for (int i = 0; i < ObjectCount; i++)
                {
                    positions[i] = new Vector3(
                        UnityEngine.Random.Range(0, 200),
                       UnityEngine.Random.Range(0, 50),
                       UnityEngine.Random.Range(0, 200));
                    rotations[i] = UnityEngine.Random.rotation;

                    objects.Add((uint)i, new FakeTranform((uint)i, positions[i], rotations[i]));
                    objectsArray[i] = new FakeTranform((uint)i, positions[i], rotations[i]);
                }

                int iterations = 10000;

                Console.WriteLine($"\n-------------\nPosition Only\n\n");
                testOne(nameof(WritePositions), WritePositions, iterations);
                testOne(nameof(PackPositions), PackPositions, iterations);
                Console.WriteLine($"\n\n\n-------------\n\n");

                Console.WriteLine($"\n-------------\nRotation Only\n\n");
                testOne(nameof(WriteRotations_Blitable), WriteRotations_Blitable, iterations);
                testOne(nameof(CompressedRotations), CompressedRotations, iterations);
                testOne(nameof(PackRotations_Length9), PackRotations_Length9, iterations);
                testOne(nameof(PackRotations_Length10), PackRotations_Length10, iterations);
                testOne(nameof(PackRotationsWithBuffer_Length9), PackRotationsWithBuffer_Length9, iterations);
                testOne(nameof(PackRotationsWithBuffer_Length10), PackRotationsWithBuffer_Length10, iterations);
                testOne(nameof(PackRotationsWithBuffer_Length9_optimized), PackRotationsWithBuffer_Length9_optimized, iterations);
                testOne(nameof(PackRotationsWithBuffer_Length9_inline), PackRotationsWithBuffer_Length9_inline, iterations);
                Console.WriteLine($"\n\n\n-------------\n\n");

                Console.WriteLine($"\n-------------\nTranform");
                Console.WriteLine($"PositionCompression bits:{compression.bitCount}");
                Console.WriteLine($"\n\n");

                WriteTranforms writeTransforms = new WriteTranforms(objects, compression);
                PackTranforms packTransforms = new PackTranforms(objects, compression);
                PackTranformsWithBuffer packTransformsWithBuffer = new PackTranformsWithBuffer(objects, compression);
                PackTranformsWithBufferOptimized packTranformsWithBufferOptimized = new PackTranformsWithBufferOptimized(objects, compression);
                OldWriteTranforms oldWriteTranforms = new OldWriteTranforms(objects, false);
                OldWriteTranforms oldWriteTranformsCompressed = new OldWriteTranforms(objects, true);
                testOne(nameof(writeTransforms), writeTransforms, iterations);
                testOne(nameof(packTransforms), packTransforms, iterations);
                testOne(nameof(packTransformsWithBuffer), packTransformsWithBuffer, iterations);
                testOne(nameof(oldWriteTranforms), oldWriteTranforms, iterations);
                testOne(nameof(oldWriteTranformsCompressed), oldWriteTranformsCompressed, iterations);
                testOne(nameof(packTranformsWithBufferOptimized), packTranformsWithBufferOptimized, iterations);

                Console.WriteLine($"");
                Console.WriteLine($"");
                Console.WriteLine($"\n\n\n-------------\n\n");
            }
            finally
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
        }
        void testOne(string name, Func<int> action, int iterations)
        {
            // warmup (atleast 10)
            for (int n = 0; n < Mathf.Max(iterations / 100, 10); n++)
            {
                action.Invoke();
            }
            long start = sw.ElapsedMilliseconds;
            int byteCount = 0;
            for (int n = 0; n < iterations; n++)
            {
                byteCount = action.Invoke();
            }
            long end = sw.ElapsedMilliseconds;
            long elapsed = end - start;
            Console.WriteLine($"{name,-50}{elapsed,10}{byteCount,10}");
        }
        void testOne(string name, ICanRun canRun, int iterations)
        {
            // warmup
            for (int n = 0; n < iterations / 100; n++)
            {
                canRun.Run();
            }
            long start = sw.ElapsedMilliseconds;
            for (int n = 0; n < iterations; n++)
            {
                canRun.Run();
            }
            long end = sw.ElapsedMilliseconds;
            long elapsed = end - start;
            Console.WriteLine($"{name,-40}{elapsed,10}{canRun.WriteCount,20}");
        }

        int WritePositions()
        {
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                for (int i = 0; i < positions.Length; i++)
                {
                    compression.Compress(writer, positions[i]);
                }
                return writer.Length;
            }
        }
        int PackPositions()
        {
            using (PooledNetworkWriter netWriter = NetworkWriterPool.GetWriter())
            {
                bitWriter.Reset(netWriter);
                for (int i = 0; i < positions.Length; i++)
                {
                    compression.Compress(bitWriter, positions[i]);
                }
                bitWriter.Flush();
                return netWriter.Length;
            }
        }

        int WriteRotations_Blitable()
        {
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                for (int i = 0; i < rotations.Length; i++)
                {
                    writer.WriteQuaternion(rotations[i]);
                }
                return writer.Length;
            }
        }
        int CompressedRotations()
        {
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                for (int i = 0; i < rotations.Length; i++)
                {
                    writer.WriteUInt32(Compression.CompressQuaternion(rotations[i]));
                }
                return writer.Length;
            }
        }
        int PackRotations_Length9()
        {
            using (PooledNetworkWriter netWriter = NetworkWriterPool.GetWriter())
            {
                bitWriter.Reset(netWriter);
                for (int i = 0; i < rotations.Length; i++)
                {
                    packerLn9.Pack(bitWriter, rotations[i]);
                }
                bitWriter.Flush();
                return netWriter.Length;
            }
        }
        int PackRotations_Length10()
        {
            using (PooledNetworkWriter netWriter = NetworkWriterPool.GetWriter())
            {
                bitWriter.Reset(netWriter);
                for (int i = 0; i < rotations.Length; i++)
                {
                    packerLn10.Pack(bitWriter, rotations[i]);
                }
                bitWriter.Flush();
                return netWriter.Length;
            }
        }
        int PackRotationsWithBuffer_Length9()
        {
            bitWriterBuffer.Reset();
            for (int i = 0; i < rotations.Length; i++)
            {
                packerLn9.Pack(bitWriterBuffer, rotations[i]);
            }
            bitWriterBuffer.Flush();
            return bitWriterBuffer.BufferCount;
        }
        int PackRotationsWithBuffer_Length9_optimized()
        {
            bitWriterBuffer.Reset();
            for (int i = 0; i < rotations.Length; i++)
            {
                packerLn9op.Pack(bitWriterBuffer, rotations[i]);
            }
            bitWriterBuffer.Flush();
            return bitWriterBuffer.BufferCount;
        }
        int PackRotationsWithBuffer_Length9_inline()
        {
            bitWriterBuffer.Reset();
            for (int i = 0; i < rotations.Length; i++)
            {
                packerLn9opline.Pack(bitWriterBuffer, rotations[i]);
            }
            bitWriterBuffer.Flush();
            return bitWriterBuffer.BufferCount;
        }
        int PackRotationsWithBuffer_Length10()
        {
            bitWriterBuffer.Reset();
            for (int i = 0; i < rotations.Length; i++)
            {
                packerLn10.Pack(bitWriterBuffer, rotations[i]);
            }
            bitWriterBuffer.Flush();
            return bitWriterBuffer.BufferCount;
        }

        interface ICanRun
        {
            int WriteCount { get; }
            void Run();
        }
        public class OldWriteTranforms : ICanRun
        {
            readonly Dictionary<uint, IHasPositionRotation> objects;
            readonly bool rotCompression;

            public int writeCount = 0;
            int ICanRun.WriteCount => writeCount;
            public OldWriteTranforms(Dictionary<uint, IHasPositionRotation> objects, bool rotCompression)
            {
                this.objects = objects;
                this.rotCompression = rotCompression;
            }
            public void Run()
            {
                using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
                {
                    foreach (KeyValuePair<uint, IHasPositionRotation> kvp in objects)
                    {
                        IHasPositionRotation behaviour = kvp.Value;
                        if (!behaviour.NeedsUpdate(0))
                            continue;

                        uint id = kvp.Key;
                        PositionRotation posRot = behaviour.PositionRotation;

                        PackedWriter.WritePacked(writer, id);
                        writer.WriteVector3(posRot.position);
                        if (rotCompression)
                        {
                            writer.WriteUInt32(Compression.CompressQuaternion(posRot.rotation));
                        }
                        else
                        {
                            writer.WriteQuaternion(posRot.rotation);
                        }
                        writer.WriteVector3(Vector3.one);//scale
                    }

                    writeCount = writer.Length;
                }
            }
        }
        public class WriteTranforms : ICanRun
        {
            readonly Dictionary<uint, IHasPositionRotation> objects;
            readonly PositionCompression compression;


            public int writeCount = 0;
            int ICanRun.WriteCount => writeCount;
            public WriteTranforms(Dictionary<uint, IHasPositionRotation> objects, PositionCompression compression)
            {
                this.objects = objects;
                this.compression = compression;
            }

            public void Run()
            {
                using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
                {
                    foreach (KeyValuePair<uint, IHasPositionRotation> kvp in objects)
                    {
                        IHasPositionRotation behaviour = kvp.Value;
                        if (!behaviour.NeedsUpdate(0))
                            continue;

                        uint id = kvp.Key;
                        PositionRotation posRot = behaviour.PositionRotation;

                        PackedWriter.WritePacked(writer, id);

                        compression.Compress(writer, posRot.position);

                        writer.WriteBlittable(Compression.CompressQuaternion(posRot.rotation));

                        behaviour.ClearNeedsUpdate();
                    }

                    writeCount = writer.Length;
                }
            }
        }
        public class PackTranforms : ICanRun
        {
            readonly BitWriter writer;
            readonly Dictionary<uint, IHasPositionRotation> objects;
            readonly PositionCompression compression;
            readonly QuaternionPacker quaternionPacker;


            public int writeCount = 0;
            int ICanRun.WriteCount => writeCount;
            public PackTranforms(Dictionary<uint, IHasPositionRotation> objects, PositionCompression compression)
            {
                this.objects = objects;
                this.compression = compression;
                writer = new BitWriter();
                quaternionPacker = new QuaternionPacker(9);
            }

            public void Run()
            {
                using (PooledNetworkWriter netWriter = NetworkWriterPool.GetWriter())
                {
                    writer.Reset(netWriter);
                    int idBitCount = PositionCompression.BitCountFromRange(ObjectCount);

                    foreach (KeyValuePair<uint, IHasPositionRotation> kvp in objects)
                    {
                        IHasPositionRotation behaviour = kvp.Value;
                        if (!behaviour.NeedsUpdate(0))
                            continue;

                        uint id = kvp.Key;
                        PositionRotation posRot = behaviour.PositionRotation;

                        writer.Write(id, idBitCount);
                        compression.Compress(writer, posRot.position);
                        quaternionPacker.Pack(writer, posRot.rotation);

                        behaviour.ClearNeedsUpdate();
                    }

                    writer.Flush();

                    writeCount = netWriter.Length;
                }
            }
        }

        public class PackTranformsWithBuffer : ICanRun
        {
            readonly BitWriterUnsafeBuffer writer;
            readonly Dictionary<uint, IHasPositionRotation> objects;
            readonly PositionCompression compression;
            readonly QuaternionPacker quaternionPacker;


            public int writeCount = 0;
            int ICanRun.WriteCount => writeCount;
            public PackTranformsWithBuffer(Dictionary<uint, IHasPositionRotation> objects, PositionCompression compression)
            {
                this.objects = objects;
                this.compression = compression;
                // 32 bytes per object is uncompressed, this will be more than enough for this
                writer = new BitWriterUnsafeBuffer(objects.Count * 32);
                quaternionPacker = new QuaternionPacker(9);
            }

            public void Run()
            {
                writer.Reset();
                int idBitCount = PositionCompression.BitCountFromRange(ObjectCount);

                foreach (KeyValuePair<uint, IHasPositionRotation> kvp in objects)
                {
                    IHasPositionRotation behaviour = kvp.Value;
                    if (!behaviour.NeedsUpdate(0))
                        continue;

                    uint id = kvp.Key;
                    PositionRotation posRot = behaviour.PositionRotation;

                    writer.Write(id, idBitCount);
                    compression.Compress(writer, posRot.position);
                    quaternionPacker.Pack(writer, posRot.rotation);

                    behaviour.ClearNeedsUpdate();
                }

                writer.Flush();

                writeCount = writer.BufferCount;
            }
        }

        public class PackTranformsWithBufferOptimized : ICanRun
        {
            readonly BitWriterUnsafeBuffer writer;
            readonly Dictionary<uint, IHasPositionRotation> objects;
            readonly PositionCompression compression;
            readonly QuaternionPackerOptimized quaternionPacker;


            public int writeCount = 0;
            int ICanRun.WriteCount => writeCount;
            public PackTranformsWithBufferOptimized(Dictionary<uint, IHasPositionRotation> objects, PositionCompression compression)
            {
                this.objects = objects;
                this.compression = compression;
                // 32 bytes per object is uncompressed, this will be more than enough for this
                writer = new BitWriterUnsafeBuffer(objects.Count * 32);
                quaternionPacker = new QuaternionPackerOptimized(9);
            }

            public void Run()
            {
                writer.Reset();
                int idBitCount = PositionCompression.BitCountFromRange(ObjectCount);

                foreach (KeyValuePair<uint, IHasPositionRotation> kvp in objects)
                {
                    IHasPositionRotation behaviour = kvp.Value;
                    if (!behaviour.NeedsUpdate(0))
                        continue;

                    uint id = kvp.Key;
                    PositionRotation posRot = behaviour.PositionRotation;

                    writer.Write(id, idBitCount);
                    compression.Compress(writer, posRot.position);
                    quaternionPacker.Pack(writer, posRot.rotation);

                    behaviour.ClearNeedsUpdate();
                }

                writer.Flush();

                writeCount = writer.BufferCount;
            }
        }
    }
    public class QuaternionPacker
    {
        const float MinValue = -1f / 1.414214f; // 1/ sqrt(2)
        const float MaxValue = 1f / 1.414214f;

        readonly int BitLength = 10;
        // same as Mathf.Pow(2, targetBitLength) - 1
        readonly uint UintRange;

        public QuaternionPacker(int quaternionBitLength)
        {
            BitLength = quaternionBitLength;
            UintRange = (1u << BitLength) - 1u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pack(BitWriter writer, Quaternion value)
        {
            // make sure value is normalized (dont trust user given value, and math here assumes normalized)
            value = value.normalized;

            int largestIndex = FindLargestIndex(value);
            Vector3 small = GetSmallerDimensions(largestIndex, value);
            // largest needs to be positive to be calculated by reader 
            // if largest is negative flip sign of others because Q = -Q
            if (value[largestIndex] < 0)
            {
                small *= -1;
            }

            uint a = Compression.ScaleToUInt(small.x, MinValue, MaxValue, 0, UintRange);
            uint b = Compression.ScaleToUInt(small.y, MinValue, MaxValue, 0, UintRange);
            uint c = Compression.ScaleToUInt(small.z, MinValue, MaxValue, 0, UintRange);

            writer.Write((uint)largestIndex, 2);
            writer.Write(a, BitLength);
            writer.Write(b, BitLength);
            writer.Write(c, BitLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pack(BitWriterUnsafeBuffer writer, Quaternion value)
        {
            // make sure value is normalized (dont trust user given value, and math here assumes normalized)
            value = value.normalized;

            int largestIndex = FindLargestIndex(value);
            Vector3 small = GetSmallerDimensions(largestIndex, value);
            // largest needs to be positive to be calculated by reader 
            // if largest is negative flip sign of others because Q = -Q
            if (value[largestIndex] < 0)
            {
                small *= -1;
            }

            uint a = Compression.ScaleToUInt(small.x, MinValue, MaxValue, 0, UintRange);
            uint b = Compression.ScaleToUInt(small.y, MinValue, MaxValue, 0, UintRange);
            uint c = Compression.ScaleToUInt(small.z, MinValue, MaxValue, 0, UintRange);

            writer.Write((uint)largestIndex, 2);
            writer.Write(a, BitLength);
            writer.Write(b, BitLength);
            writer.Write(c, BitLength);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int FindLargestIndex(Quaternion q)
        {
            int index = default;
            float current = default;

            // check each value to see which one is largest (ignoring +-)
            for (int i = 0; i < 4; i++)
            {
                float next = Mathf.Abs(q[i]);
                if (next > current)
                {
                    index = i;
                    current = next;
                }
            }

            return index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector3 GetSmallerDimensions(int largestIndex, Quaternion value)
        {
            float x = value.x;
            float y = value.y;
            float z = value.z;
            float w = value.w;

            switch (largestIndex)
            {
                case 0:
                    return new Vector3(y, z, w);
                case 1:
                    return new Vector3(x, z, w);
                case 2:
                    return new Vector3(x, y, w);
                case 3:
                    return new Vector3(x, y, z);
                default:
                    throw new IndexOutOfRangeException("Invalid Quaternion index!");
            }
        }
    }

    public class QuaternionPackerOptimized
    {
        const float MinValue = -1f / 1.414214f; // 1/ sqrt(2)
        const float MaxValue = 1f / 1.414214f;

        readonly int BitLength = 10;
        // same as Mathf.Pow(2, targetBitLength) - 1
        readonly uint UintRange;

        public QuaternionPackerOptimized(int quaternionBitLength)
        {
            BitLength = quaternionBitLength;
            UintRange = (1u << BitLength) - 1u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pack(BitWriter writer, Quaternion _value)
        {
            // make sure value is normalized (dont trust user given value, and math here assumes normalized)
            //_value = _value.normalized;
            float x = _value.x;
            float y = _value.y;
            float z = _value.z;
            float w = _value.w;

            quickNormalize(ref x, ref y, ref z, ref w);

            FindLargestIndex(x, y, z, w, out int index, out float largest);
            GetSmallerDimensions(index, x, y, z, w, out float a, out float b, out float c);
            // largest needs to be positive to be calculated by reader 
            // if largest is negative flip sign of others because Q = -Q
            if (largest < 0)
            {
                a = -a;
                b = -b;
                c = -c;
            }

            uint ua = Compression.ScaleToUInt(a, MinValue, MaxValue, 0, UintRange);
            uint ub = Compression.ScaleToUInt(b, MinValue, MaxValue, 0, UintRange);
            uint uc = Compression.ScaleToUInt(c, MinValue, MaxValue, 0, UintRange);

            writer.Write((uint)index, 2);
            writer.Write(ua, BitLength);
            writer.Write(ub, BitLength);
            writer.Write(uc, BitLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pack(BitWriterUnsafeBuffer writer, Quaternion _value)
        {
            // make sure value is normalized (dont trust user given value, and math here assumes normalized)
            //_value = _value.normalized;
            float x = _value.x;
            float y = _value.y;
            float z = _value.z;
            float w = _value.w;

            quickNormalize(ref x, ref y, ref z, ref w);

            FindLargestIndex(x, y, z, w, out int index, out float largest);
            GetSmallerDimensions(index, x, y, z, w, out float a, out float b, out float c);
            // largest needs to be positive to be calculated by reader 
            // if largest is negative flip sign of others because Q = -Q
            if (largest < 0)
            {
                a = -a;
                b = -b;
                c = -c;
            }

            uint ua = Compression.ScaleToUInt(a, MinValue, MaxValue, 0, UintRange);
            uint ub = Compression.ScaleToUInt(b, MinValue, MaxValue, 0, UintRange);
            uint uc = Compression.ScaleToUInt(c, MinValue, MaxValue, 0, UintRange);

            writer.Write((uint)index, 2);
            writer.Write(ua, BitLength);
            writer.Write(ub, BitLength);
            writer.Write(uc, BitLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void quickNormalize(ref float x, ref float y, ref float z, ref float w)
        {
            float dot = x * x + y * y + z * z + w + w;
            const float allowedEpsilon = 1E-5f;
            const float minAllowed = 1 - allowedEpsilon;
            const float maxAllowed = 1 + allowedEpsilon;
            if (minAllowed > dot || maxAllowed < dot)
            {
                float dotSqrt = (float)Math.Sqrt(dot);
                // rotation is 0
                if (dotSqrt < allowedEpsilon)
                {
                    // identity
                    x = 0;
                    y = 0;
                    z = 0;
                    w = 1;
                }
                else
                {
                    x /= dotSqrt;
                    y /= dotSqrt;
                    z /= dotSqrt;
                    w /= dotSqrt;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void FindLargestIndex(float x, float y, float z, float w, out int index, out float largest)
        {
            float x2 = x * x;
            float y2 = y * y;
            float z2 = z * z;
            float w2 = w * w;

            index = 0;
            float current = x2;
            largest = x;
            // check vs sq to avoid doing mathf.abs
            if (y2 > current)
            {
                index = 1;
                largest = y;
            }
            if (z2 > current)
            {
                index = 2;
                largest = z;
            }
            if (w2 > current)
            {
                index = 3;
                largest = w;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void GetSmallerDimensions(int largestIndex, float x, float y, float z, float w, out float a, out float b, out float c)
        {
            switch (largestIndex)
            {
                case 0:
                    a = y;
                    b = z;
                    c = w;
                    return;
                case 1:
                    a = x;
                    b = z;
                    c = w;
                    return;
                case 2:
                    a = x;
                    b = y;
                    c = w;
                    return;
                case 3:
                    a = x;
                    b = y;
                    c = z;
                    return;
                default:
                    throw new IndexOutOfRangeException("Invalid Quaternion index!");
            }
        }
    }

    public class QuaternionPackerOptimizedManualInLine
    {
        const float MinValue = -1f / 1.414214f; // 1/ sqrt(2)
        const float MaxValue = 1f / 1.414214f;

        readonly int BitLength = 10;
        // same as Mathf.Pow(2, targetBitLength) - 1
        readonly uint UintRange;

        public QuaternionPackerOptimizedManualInLine(int quaternionBitLength)
        {
            BitLength = quaternionBitLength;
            UintRange = (1u << BitLength) - 1u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pack(BitWriter writer, Quaternion _value)
        {
            // make sure value is normalized (dont trust user given value, and math here assumes normalized)
            //_value = _value.normalized;
            float x = _value.x;
            float y = _value.y;
            float z = _value.z;
            float w = _value.w;

            float x2 = x * x;
            float y2 = y * y;
            float z2 = z * z;
            float w2 = w * w;

            //quickNormalize(ref x, ref y, ref z, ref w);
            float dot = x2 + y2 + z2 + w2;
            const float allowedEpsilon = 1E-5f;
            const float minAllowed = 1 - allowedEpsilon;
            const float maxAllowed = 1 + allowedEpsilon;
            if (minAllowed > dot || maxAllowed < dot)
            {
                float dotSqrt = (float)Math.Sqrt(dot);
                // rotation is 0
                if (dotSqrt < allowedEpsilon)
                {
                    // identity
                    x = 0;
                    y = 0;
                    z = 0;
                    w = 1;
                }
                else
                {
                    x /= dotSqrt;
                    y /= dotSqrt;
                    z /= dotSqrt;
                    w /= dotSqrt;
                }
            }

            //FindLargestIndex(x, y, z, w, out int index, out float largest);
            int index = 0;
            float current = x2;
            float largest = x;
            // check vs sq to avoid doing mathf.abs
            if (y2 > current)
            {
                index = 1;
                largest = y;
            }
            if (z2 > current)
            {
                index = 2;
                largest = z;
            }
            if (w2 > current)
            {
                index = 3;
                largest = w;
            }

            //GetSmallerDimensions(index, x, y, z, w, out float a, out float b, out float c);
            float a, b, c;
            switch (index)
            {
                case 0:
                    a = y;
                    b = z;
                    c = w;
                    break;
                case 1:
                    a = x;
                    b = z;
                    c = w;
                    break;
                case 2:
                    a = x;
                    b = y;
                    c = w;
                    break;
                case 3:
                    a = x;
                    b = y;
                    c = z;
                    break;
                default:
                    throw new IndexOutOfRangeException("Invalid Quaternion index!");
            }

            // largest needs to be positive to be calculated by reader 
            // if largest is negative flip sign of others because Q = -Q
            if (largest < 0)
            {
                a = -a;
                b = -b;
                c = -c;
            }

            uint ua = Compression.ScaleToUInt(a, MinValue, MaxValue, 0, UintRange);
            uint ub = Compression.ScaleToUInt(b, MinValue, MaxValue, 0, UintRange);
            uint uc = Compression.ScaleToUInt(c, MinValue, MaxValue, 0, UintRange);

            writer.Write((uint)index, 2);
            writer.Write(ua, BitLength);
            writer.Write(ub, BitLength);
            writer.Write(uc, BitLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pack(BitWriterUnsafeBuffer writer, Quaternion _value)
        {
            // make sure value is normalized (dont trust user given value, and math here assumes normalized)
            //_value = _value.normalized;
            float x = _value.x;
            float y = _value.y;
            float z = _value.z;
            float w = _value.w;

            float x2 = x * x;
            float y2 = y * y;
            float z2 = z * z;
            float w2 = w * w;

            //quickNormalize(ref x, ref y, ref z, ref w);
            float dot = x2 + y2 + z2 + w2;
            const float allowedEpsilon = 1E-5f;
            const float minAllowed = 1 - allowedEpsilon;
            const float maxAllowed = 1 + allowedEpsilon;
            if (minAllowed > dot || maxAllowed < dot)
            {
                float dotSqrt = (float)Math.Sqrt(dot);
                // rotation is 0
                if (dotSqrt < allowedEpsilon)
                {
                    // identity
                    x = 0;
                    y = 0;
                    z = 0;
                    w = 1;
                }
                else
                {
                    x /= dotSqrt;
                    y /= dotSqrt;
                    z /= dotSqrt;
                    w /= dotSqrt;
                }
            }

            //FindLargestIndex(x, y, z, w, out int index, out float largest);
            int index = 0;
            float current = x2;
            float largest = x;
            // check vs sq to avoid doing mathf.abs
            if (y2 > current)
            {
                index = 1;
                largest = y;
            }
            if (z2 > current)
            {
                index = 2;
                largest = z;
            }
            if (w2 > current)
            {
                index = 3;
                largest = w;
            }

            //GetSmallerDimensions(index, x, y, z, w, out float a, out float b, out float c);
            float a, b, c;
            switch (index)
            {
                case 0:
                    a = y;
                    b = z;
                    c = w;
                    break;
                case 1:
                    a = x;
                    b = z;
                    c = w;
                    break;
                case 2:
                    a = x;
                    b = y;
                    c = w;
                    break;
                case 3:
                    a = x;
                    b = y;
                    c = z;
                    break;
                default:
                    throw new IndexOutOfRangeException("Invalid Quaternion index!");
            }

            // largest needs to be positive to be calculated by reader 
            // if largest is negative flip sign of others because Q = -Q
            if (largest < 0)
            {
                a = -a;
                b = -b;
                c = -c;
            }

            uint ua = Compression.ScaleToUInt(a, MinValue, MaxValue, 0, UintRange);
            uint ub = Compression.ScaleToUInt(b, MinValue, MaxValue, 0, UintRange);
            uint uc = Compression.ScaleToUInt(c, MinValue, MaxValue, 0, UintRange);

            writer.Write((uint)index, 2);
            writer.Write(ua, BitLength);
            writer.Write(ub, BitLength);
            writer.Write(uc, BitLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void quickNormalize(ref float x, ref float y, ref float z, ref float w)
        {
            float dot = x * x + y * y + z * z + w + w;
            const float allowedEpsilon = 1E-5f;
            const float minAllowed = 1 - allowedEpsilon;
            const float maxAllowed = 1 + allowedEpsilon;
            if (minAllowed > dot || maxAllowed < dot)
            {
                float dotSqrt = (float)Math.Sqrt(dot);
                // rotation is 0
                if (dotSqrt < allowedEpsilon)
                {
                    // identity
                    x = 0;
                    y = 0;
                    z = 0;
                    w = 1;
                }
                else
                {
                    x /= dotSqrt;
                    y /= dotSqrt;
                    z /= dotSqrt;
                    w /= dotSqrt;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void FindLargestIndex(float x, float y, float z, float w, out int index, out float largest)
        {
            float x2 = x * x;
            float y2 = y * y;
            float z2 = z * z;
            float w2 = w * w;

            index = 0;
            float current = x2;
            largest = x;
            // check vs sq to avoid doing mathf.abs
            if (y2 > current)
            {
                index = 1;
                largest = y;
            }
            if (z2 > current)
            {
                index = 2;
                largest = z;
            }
            if (w2 > current)
            {
                index = 3;
                largest = w;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void GetSmallerDimensions(int largestIndex, float x, float y, float z, float w, out float a, out float b, out float c)
        {
            switch (largestIndex)
            {
                case 0:
                    a = y;
                    b = z;
                    c = w;
                    return;
                case 1:
                    a = x;
                    b = z;
                    c = w;
                    return;
                case 2:
                    a = x;
                    b = y;
                    c = w;
                    return;
                case 3:
                    a = x;
                    b = y;
                    c = z;
                    return;
                default:
                    throw new IndexOutOfRangeException("Invalid Quaternion index!");
            }
        }
    }
}
