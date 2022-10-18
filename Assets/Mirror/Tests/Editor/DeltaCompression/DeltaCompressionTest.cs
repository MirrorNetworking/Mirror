using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public abstract class DeltaCompressionTest
    {
        // array for writer
        protected NetworkWriter compressWriter;
        protected NetworkWriter decompressWriter;

        // implementation specific
        public abstract int MaxPatchSize(int inputLength, int blockSize);
        public abstract void Compress(ArraySegment<byte> previous, ArraySegment<byte> current, int blockSize, NetworkWriter patch);
        public abstract void Decompress(ArraySegment<byte> previous, NetworkReader patch, int blockSize, NetworkWriter current);

        [SetUp]
        public virtual void SetUp()
        {
            compressWriter = new NetworkWriter();
            decompressWriter = new NetworkWriter();
        }

        // helper function to compress, decompress, compare expected size & content
        public void CompressAndDecompress(
            byte[] previous, byte[] current,
            NetworkWriter compressWriter,
            NetworkWriter decompressWriter,
            int blockSize, int expectedPatchSize)
        {
            // remember writer start position.
            // it won't always start at '0', i.e. if OnData has multiple messages.
            int compressWriterStart = compressWriter.Position;

            // compress with block size into compressWriter
            Compress(previous, current, blockSize, compressWriter);
            // Assert.That(result, Is.True);

            // guarantee patch size (if given)
            int compressedSize = compressWriter.Position - compressWriterStart;
            if (expectedPatchSize != -1)
                Assert.That(compressedSize, Is.EqualTo(expectedPatchSize));

            // get patch content from writer (ignoring content before start)
            ArraySegment<byte> compressedContent = new ArraySegment<byte>(compressWriter.ToArraySegment().Array, compressWriterStart, compressedSize);

            // decompress patch against previous
            NetworkReader patch = new NetworkReader(compressedContent);
            Decompress(previous, patch, blockSize, decompressWriter);
            // Assert.That(result, Is.True);

            // make sure decompressed == current as expected
            ArraySegment<byte> decompressed = decompressWriter.ToArraySegment();
            if (!current.SequenceEqual(decompressed))
                Assert.Fail($"Decompress mismatch!\n  previous={BitConverter.ToString(previous)}\n  current={BitConverter.ToString(current)}\n  decompressed={decompressed.ToHexString()}");

            // IMPORTANT
            // make sure decompression leaves reader at the position after patch.
            // not checking this introduced a bug in run length version before,
            // where reader Position was set to the end of run length header data.
            // otherwise next decompression would start at the wrong position.
            Assert.That(patch.Position, Is.EqualTo(compressedSize));

            // log
            Debug.Log($"Compression: {current.Length} bytes => {compressedSize} bytes");
        }

        [Test]
        public void Compress_BothEmpty()
        {
            CompressAndDecompress(new byte[0], new byte[0], compressWriter, decompressWriter, 2, expectedPatchSize: -1);
        }

        [Test]
        public void Compress_Same()
        {
            byte[] aArray = {0x00, 0x01, 0x02, 0x03, 0x04, 0x05};
            CompressAndDecompress(aArray, aArray, compressWriter, decompressWriter, 2, expectedPatchSize: -1);
        }

        [Test]
        public void Compress_Different()
        {
            byte[] aArray = {0x00, 0x01, 0x02, 0x03, 0x04, 0x05};
            byte[] bArray = {0xFF, 0x01, 0x02, 0x03, 0xFF, 0x05};
            CompressAndDecompress(aArray, bArray, compressWriter, decompressWriter, 2, expectedPatchSize: -1);
        }

        // writer might already contain the message Id or similar.
        // Compress should add to it, not overwrite the start.
        [Test]
        public void Compress_RespectsPatchWriterOffset()
        {
            byte[] aArray = {0x00, 0x01, 0x02, 0x03, 0x04, 0x05};
            byte[] bArray = {0xFF, 0x01, 0x02, 0x03, 0xFF, 0x05};
            compressWriter.WriteByte(0xBB);
            CompressAndDecompress(aArray, bArray, compressWriter, decompressWriter, 2, expectedPatchSize: -1);
        }

        [Test]
        public void Compress_Different_TinyBlockSize()
        {
            byte[] aArray = {0x00, 0x01, 0x02, 0x03, 0x04, 0x05};
            byte[] bArray = {0xFF, 0x01, 0x02, 0x03, 0xFF, 0x05};
            CompressAndDecompress(aArray, bArray, compressWriter, decompressWriter, 1, expectedPatchSize: -1);
        }

        // block size == array size test just to be sure
        [Test]
        public void Compress_Different_BlockSizeEqualsArraySize()
        {
            byte[] aArray = {0x00, 0x01, 0x02, 0x03, 0x04, 0x05};
            byte[] bArray = {0xFF, 0x01, 0x02, 0x03, 0xFF, 0x05};
            CompressAndDecompress(aArray, bArray, compressWriter, decompressWriter, blockSize: aArray.Length, expectedPatchSize: -1);
        }

        // block size > array size needs to be supported.
        // for example: 16 byte block size, but a component is only 4 bytes.
        [Test]
        public void Compress_Different_BlockSizeLargerThanArraySize()
        {
            byte[] aArray = {0x00, 0x01, 0x02, 0x03, 0x04, 0x05};
            byte[] bArray = {0xFF, 0x01, 0x02, 0x03, 0xFF, 0x05};
            CompressAndDecompress(aArray, bArray, compressWriter, decompressWriter, blockSize: aArray.Length + 1, expectedPatchSize: -1);
        }

        // block size = 2, array size = 4 => rounds perfectly
        [Test]
        public void Compress_Different_RoundsToBlockSize()
        {
            byte[] aArray = {0x00, 0x01, 0x02, 0x03};
            byte[] bArray = {0xFF, 0x01, 0x02, 0x03};
            CompressAndDecompress(aArray, bArray, compressWriter, decompressWriter, 2, expectedPatchSize: -1);
        }

        // block size = 2, array size = 5 => last byte is only half a block
        [Test]
        public void Compress_Different_DoesNotRoundToBlockSize()
        {
            byte[] aArray = {0x00, 0x01, 0x02, 0x03, 0x04};
            byte[] bArray = {0xFF, 0x01, 0x02, 0x03, 0xFF};
            CompressAndDecompress(aArray, bArray, compressWriter, decompressWriter, 2, expectedPatchSize: -1);
        }

        [Test]
        public void Compress_SizeMismatch()
        {
            byte[] aArray = {0x00, 0x01, 0x02, 0x03, 0x04, 0x05};
            byte[] cArray = {0x00, 0x01};
            Assert.Throws<ArgumentException>(() =>
                Compress(aArray, cArray, 2, compressWriter)
            );
        }

        /* writers auto resize in Mirror
        [Test]
        public void Compress_WriterSmallerThanMaxPatchSize()
        {
            const int blockSize = 2;
            byte[] aArray = {0x00, 0x01, 0x02, 0x03, 0x04, 0x05};
            int maxPatchSizeBytes = MaxPatchSize(aArray.Length, blockSize);

            NetworkWriter tooSmallWriter = new NetworkWriter(maxPatchSizeBytes - 1);

            // should return false
            Assert.That(Compress(aArray, aArray, blockSize, tooSmallWriter), Is.False);
        }
        */

        // test to prevent a previous bug, where
        // - input of 7 bytes
        // - block size of 4 bytes
        // - patch size of 8 bytes,
        // it would read a full 4 bytes block,
        // and then another full 4 bytes block,
        // because remaining had more data.
        // => where it should've stopped at the 7th byte at original input size!
        [Test]
        public void Decompress_StopsAtOriginalSize()
        {
            const int blockSize = 16;
            byte[] last =   {0xA8, 0x48, 0xA8, 0xBE, 0xD7, 0x17, 0xFC, 0x64, 0x49, 0x5E, 0xC9, 0x22, 0x24, 0x4A, 0xAC, 0x25, 0x0C, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFD, 0xF7, 0xDF, 0x00, 0x00, 0x00, 0x00};
            byte[] current= {0xA8, 0x48, 0xA8, 0xBE, 0xD7, 0x17, 0xFC, 0x64, 0x49, 0x5E, 0xC9, 0x22, 0x24, 0x4A, 0xAC, 0x25, 0x0C, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFE, 0xF9, 0xE7, 0xDF, 0x00, 0x00, 0x00, 0x00};

            // compress to generate the patch
            Compress(last, current, blockSize, compressWriter);
            ArraySegment<byte> patch = compressWriter.ToArraySegment();

            // append data to the patch
            byte[] largerPatch = new byte[patch.Count + 30];
            Array.Copy(patch.ToArray(), largerPatch, patch.Count);

            NetworkReader patchReader = new NetworkReader(largerPatch);

            // lets decompress with that data
            NetworkWriter resultWriter = new NetworkWriter();

            // block size needs to be > patch size of 14
            Decompress(last, patchReader, blockSize, resultWriter);
            Debug.Log($"decompressed={resultWriter.ToArraySegment().ToHexString()}");
            Assert.That(current.SequenceEqual(resultWriter.ToArraySegment()));
        }

        /* writers auto resize in Mirror
        // exception should be thrown immediately if passed writer is too small
        [Test]
        public void Decompress_TooSmallWriter()
        {
            const int blockSize = 4;

            byte[] aArray = {0x00, 0x01, 0x02, 0x03, 0x04};
            byte[] bArray = {0xFF, 0x01, 0x02, 0x03, 0xFF};

            // result will be same size as input.
            // make writer 1 smaller than input.
            NetworkWriter tooSmallWriter = new NetworkWriter(aArray.Length - 1);

            Assert.Throws<IndexOutOfRangeException>(() =>
                CompressAndDecompress(aArray, bArray, compressWriter, tooSmallWriter, blockSize, expectedPatchSize: -1)
            );
        }

        // exact writer size should work
        [Test]
        public void Decompress_ExactSizedWriter()
        {
            const int blockSize = 4;

            byte[] aArray = {0x00, 0x01, 0x02, 0x03, 0x04};
            byte[] bArray = {0xFF, 0x01, 0x02, 0x03, 0xFF};

            // result will be same size as input.
            // make writer exactly the same size as input.
            NetworkWriter tooSmallWriter = new NetworkWriter(aArray.Length);
            CompressAndDecompress(aArray, bArray, compressWriter, tooSmallWriter, blockSize, expectedPatchSize: -1);
        }
        */

        // try a 1mb array. make sure it's still fast, doesn't deadlock etc.
        // and different block sizes to get a feeling for results
        [Test]
        [TestCase( 2)] // Blocky:  86 kb
        [TestCase( 4)] // Blocky:  74 kb
        [TestCase(16)] // Blocky: 175 kb
        [TestCase(64)] // Blocky: 673 kb
        public void Compress_LargeData_1PercentChanges(int blockSize)
        {
            byte[] largeA = new byte[1 * 1024 * 1024];
            byte[] largeB = new byte[1 * 1024 * 1024];

            int maxPatchSize = MaxPatchSize(largeA.Length, blockSize);
            NetworkWriter largeCompressWriter = new NetworkWriter();
            NetworkWriter largeDecompressWriter = new NetworkWriter();

            // change 1% of data
            for (int i = 0; i < largeB.Length; ++i)
                if (i % 100 == 0)
                    largeB[i] = 0xFF;

            CompressAndDecompress(largeA, largeB, largeCompressWriter, largeDecompressWriter, blockSize, -1);
        }

        // try a 1mb array. make sure it's still fast, doesn't deadlock etc.
        // and different block sizes to get a feeling for results
        [Test]
        [TestCase( 2)] //  Blocky:  275 kb
        [TestCase( 4)] //  Blocky:  452 kb
        [TestCase(16)] //  Blocky: 1056 kb
        [TestCase(64)] //  Blocky: 1050 kb
        public void Compress_LargeData_10PercentChanges(int blockSize)
        {
            byte[] largeA = new byte[1 * 1024 * 1024];
            byte[] largeB = new byte[1 * 1024 * 1024];

            int maxPatchSize = MaxPatchSize(largeA.Length, blockSize);
            NetworkWriter largeCompressWriter = new NetworkWriter();
            NetworkWriter largeDecompressWriter = new NetworkWriter();

            // change 10% of data
            for (int i = 0; i < largeB.Length; ++i)
                if (i % 10 == 0)
                    largeB[i] = 0xFF;

            CompressAndDecompress(largeA, largeB, largeCompressWriter, largeDecompressWriter, blockSize, -1);
        }

        // same benchmark as for FossilX. compression only (server side).
        // (FossilX needs 190ms for 10k iterations)
        //
        // NOT BURST COMPILED! use the test system for burst version!!!
        [Test]
        [TestCase(1000)]
        [TestCase(10_000)]
        public void Benchmark_1000bytes_1PercentChanged(int iterations)
        {
            // fossilx uses blocksize=16 too
            const int blockSize = 16;
            byte[] A = new byte[1000];
            byte[] B = new byte[1000];

            int maxPatchSize = MaxPatchSize(A.Length, blockSize);
            NetworkWriter writer = new NetworkWriter();

            // set actual data != 0, change 1% in B
            for (int i = 0; i < B.Length; ++i)
            {
                A[i] = (byte)i;
                B[i] = i % 100 == 0 ? (byte)0xFF : (byte)i;
            }

            // calculate delta N times.
            // only measure that part.
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            for (int i = 0; i < iterations; ++i)
            {
                writer.Position = 0;
                // use NATIVE ARRAY version!
                Compress(A, B, blockSize, writer);
            }
            watch.Stop();
            Debug.LogWarning($"=> Benchmark x {iterations} took {watch.ElapsedMilliseconds} ms");

        }

        // same benchmark as for FossilX. compression only (server side).
        // (FossilX needs 190ms for 10k iterations)
        //
        // NOT BURST COMPILED! use the test system for burst version!!!
        [Test]
        [TestCase(1000)]
        [TestCase(10_000)]
        public void Benchmark_10000bytes_1PercentChanged(int iterations)
        {
            // fossilx uses blocksize=16 too
            const int blockSize = 16;
            byte[] A = new byte[10_000];
            byte[] B = new byte[10_000];

            int maxPatchSize = MaxPatchSize(A.Length, blockSize);
            NetworkWriter writer = new NetworkWriter();

            // set actual data != 0, change 1% in B
            for (int i = 0; i < B.Length; ++i)
            {
                A[i] = (byte)i;
                B[i] = i % 100 == 0 ? (byte)0xFF : (byte)i;
            }

            // calculate delta N times.
            // only measure that part.
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            for (int i = 0; i < iterations; ++i)
            {
                writer.Position = 0;
                // use NATIVE ARRAY version!
                Compress(A, B, blockSize, writer);
            }
            watch.Stop();
            Debug.LogWarning($"=> Benchmark x {iterations} took {watch.ElapsedMilliseconds} ms");

        }

        // compression size test. for comparison with FossilX.
        // (FossilX: 70 bytes)
        [Test]
        [TestCase(1)]  // 135 byte
        [TestCase(4)]  //  72 byte
        [TestCase(8)]  //  96 byte
        [TestCase(16)] // 168 byte
        [TestCase(32)] // 324 byte
        [TestCase(64)] // 642 byte
        public void Compress_1000bytes_1PercentChanged(int blockSize)
        {
            byte[] A = new byte[1000];
            byte[] B = new byte[1000];

            int maxPatchSize = MaxPatchSize(A.Length, blockSize);
            NetworkWriter writer1 = new NetworkWriter();
            NetworkWriter writer2 = new NetworkWriter();

            // change 1%
            for (int i = 0; i < B.Length; ++i)
                if (i % 100 == 0)
                    B[i] = 0xFF;

            CompressAndDecompress(A, B, writer1, writer2, blockSize, expectedPatchSize:-1);

        }

        // compression size test. for comparison with FossilX.
        // (FossilX: 700 bytes)
        [Test]
        [TestCase(1)]  //  225 byte
        [TestCase(4)]  //  432 byte
        [TestCase(8)]  //  816 byte
        [TestCase(16)] // 1000 byte
        [TestCase(32)] // 1002 byte
        [TestCase(64)] //  996 byte
        public void Compress_1000bytes_10PercentChanged(int blockSize)
        {
            byte[] A = new byte[1000];
            byte[] B = new byte[1000];

            int maxPatchSize = MaxPatchSize(A.Length, blockSize);
            NetworkWriter writer1 = new NetworkWriter();
            NetworkWriter writer2 = new NetworkWriter();

            // change 10%
            for (int i = 0; i < B.Length; ++i)
                if (i % 10 == 0)
                    B[i] = 0xFF;

            CompressAndDecompress(A, B, writer1, writer2, blockSize, expectedPatchSize:-1);

        }
    }
}
