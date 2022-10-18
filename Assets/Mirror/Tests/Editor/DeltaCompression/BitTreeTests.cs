// BitTree delta compression tests from DOTSNET / Mirror II by vis2k/mischa.
using NUnit.Framework;
using System;
using Unity.Collections;

namespace Mirror.Tests
{
    public class BitTreeTests : DeltaCompressionTest
    {
        public override int MaxPatchSize(int inputLength, int blockSize) =>
            BitTree.MaxPatchSize(inputLength);

        public override bool Compress(ArraySegment<byte> previous, ArraySegment<byte> current, int blockSize, NetworkWriter patch) =>
            BitTree.Compress(previous, current, patch);

        public override bool Decompress(ArraySegment<byte> previous, NetworkReader patch, int blockSize, NetworkWriter current) =>
            BitTree.Decompress(previous, patch, current);

        [Test]
        [TestCase(0, 0)] // level 0 has 0 nodes
        [TestCase(1, 8)] // level 1 has 8 nodes
        [TestCase(2, 64)] // level 2 has 64 nodes, tree has 8+64 in total
        [TestCase(3, 512)] // level 3 has 512 nodes, tree has 8+64+512 nodes in total
        [TestCase(4, 4096)] // level 4 has 4096 nodes, tree has 8+64+512+4096 nodes in total
        [TestCase(5, 32768)]
        [TestCase(6, 262144)]
        public void MaxNodesAtLevel(int level, int expectedNodes)
        {
            Assert.That(BitTree.MaxNodesAtLevel(level), Is.EqualTo(expectedNodes));
        }

        [Test]
        [TestCase(0, 0)] // level 0 has 0 nodes
        [TestCase(1, 8)] // level 1 has 8 nodes, tree has 0+8 in total
        [TestCase(2, 8+64)] // level 2 has 64 nodes, tree has 8+64 in total
        [TestCase(3, 8+64+512)] // level 3 has 512 nodes, tree has 8+64+512 nodes in total
        [TestCase(4, 8+64+512+4096)] // level 4 has 4096 nodes, tree has 8+64+512+4096 nodes in total
        [TestCase(5, 8+64+512+4096+32768)]
        [TestCase(6, 8+64+512+4096+32768+262144)]
        public void MaxNodesInTree(int height, int expectedNodes)
        {
            Assert.That(BitTree.MaxNodesInTree(height), Is.EqualTo(expectedNodes));
        }

        [Test]
        public void TreeDepth()
        {
            // 0 requires 0 levels
            Assert.That(BitTree.TreeDepth(0), Is.EqualTo(0));

            // 1..8 fits into 1 level
            for (int i = 1; i <= 8; ++i)
                Assert.That(BitTree.TreeDepth(i), Is.EqualTo(1));

            // 9..64 fits into 2 levels
            for (int i = 9; i <= 64; ++i)
                Assert.That(BitTree.TreeDepth(i), Is.EqualTo(2));

            // 65..512 fits into 3 levels
            for (int i = 65; i <= 512; ++i)
                Assert.That(BitTree.TreeDepth(i), Is.EqualTo(3));

            // 513..4096 fits into 4 levels
            for (int i = 513; i <= 4096; ++i)
                Assert.That(BitTree.TreeDepth(i), Is.EqualTo(4));

            // 4097..    fits into 5 levels
            for (int i = 4097; i <= 5000; ++i)
                Assert.That(BitTree.TreeDepth(i), Is.EqualTo(5));
        }

        [Test]
        public void MaxNodesForGeneratedTree()
        {
            // 0 bytes fits into 0 level, which requires 0 nodes
            Assert.That(BitTree.MaxNodesForGeneratedTree(0), Is.EqualTo(0));

            // 1..8 bytes fits into 1 level, which requires 0+8 nodes
            for (int i = 1; i <= 8; ++i)
                Assert.That(BitTree.MaxNodesForGeneratedTree(i), Is.EqualTo(0+8));

            // 9..64 bytes fits into 2 levels, which requires 0+8+64 nodes
            for (int i = 9; i <= 64; ++i)
                Assert.That(BitTree.MaxNodesForGeneratedTree(i), Is.EqualTo(0+8+64));

            // 65..512 bytes fits into 3 levels, which requires 0+8+64+512 nodes
            for (int i = 65; i <= 512; ++i)
                Assert.That(BitTree.MaxNodesForGeneratedTree(i), Is.EqualTo(0+8+64+512));

            // 513..4096 bytes fits into 4 levels, which requires 0+8+64+512+4096 nodes
            for (int i = 513; i <= 4096; ++i)
                Assert.That(BitTree.MaxNodesForGeneratedTree(i), Is.EqualTo(0+8+64+512+4096));

            // 4097..    bytes fits into 5 levels, which requires 0+8+64+512+4096+32768 nodes
            for (int i = 4097; i <= 5000; ++i)
                Assert.That(BitTree.MaxNodesForGeneratedTree(i), Is.EqualTo(0+8+64+512+4096+32768));
        }

        [Test]
        [TestCase(0, 0)]

        [TestCase(1, 1)] // 1 byte requires 8 nodes = 1 byte overhead
        [TestCase(7, 1)] // 7 byte requires 8 nodes = 1 byte overhead
        [TestCase(8, 1)] // 8 byte requires 8 nodes = 1 byte overhead

        [TestCase(9, 1+8)]  // 9 byte requires 8+64 nodes = 1+8 byte overhead
        [TestCase(63, 1+8)] // 63 byte requires 8+64 nodes = 1+8 byte overhead
        [TestCase(64, 1+8)] // 64 requires 8+64 nodes = 1+8 byte overhead

        [TestCase(65, 1+8+64)]  // 65 byte requires 8+64+512 nodes = 1+8+64 byte overhead
        [TestCase(511, 1+8+64)] // 511 byte requires 8+64+512 nodes = 1+8+64 byte overhead
        [TestCase(512, 1+8+64)] // 512 byte requires 8+64+512 nodes = 1+8+64 byte overhead

        [TestCase(513, 1+8+64+512)]  // 513 byte requires 8+64+512+4096 nodes = 1+8+64+512 byte overhead
        [TestCase(4095, 1+8+64+512)] // 4095 byte requires 8+64+512+4096 nodes = 1+8+64+512 byte overhead
        [TestCase(4096, 1+8+64+512)] // 4096 byte requires 8+64+512+4096 nodes = 1+8+64+512 byte overhead
        public void MaxOverheadTest(int inputSize, int expectedMaxSize)
        {
            Assert.That(BitTree.MaxOverhead(inputSize), Is.EqualTo(expectedMaxSize));
        }

        [Test]
        [TestCase(0, 0)] // 0 byte requires 0 nodes = 0 byte overhead + input size

        [TestCase(1, 1 + 1)] // 1 byte requires 8 nodes = 1 byte overhead + input size
        [TestCase(7, 1 + 7)] // 7 byte requires 8 nodes = 1 byte overhead + input size
        [TestCase(8, 1 + 8)] // 8 byte requires 8 nodes = 1 byte overhead + input size

        [TestCase(9, 1+8 + 9)]  // 9 byte requires 8+64 nodes = 1+8 byte overhead + input size
        [TestCase(63, 1+8 + 63)] // 63 byte requires 8+64 nodes = 1+8 byte overhead + input size
        [TestCase(64, 1+8 + 64)] // 64 requires 8+64 nodes = 1+8 byte overhead + input size

        [TestCase(65, 1+8+64 + 65)]  // 65 byte requires 8+64+512 nodes = 1+8+64 byte overhead + input size
        [TestCase(511, 1+8+64 + 511)] // 511 byte requires 8+64+512 nodes = 1+8+64 byte overhead + input size
        [TestCase(512, 1+8+64 + 512)] // 512 byte requires 8+64+512 nodes = 1+8+64 byte overhead + input size

        [TestCase(513, 1+8+64+512 + 513)]  // 513 byte requires 8+64+512+4096 nodes = 1+8+64+512 byte overhead + input size
        [TestCase(4095, 1+8+64+512 + 4095)] // 4095 byte requires 8+64+512+4096 nodes = 1+8+64+512 byte overhead + input size
        [TestCase(4096, 1+8+64+512 + 4096)] // 4096 byte requires 8+64+512+4096 nodes = 1+8+64+512 byte overhead + input size
        public void MaxPatchSizeTest(int inputSize, int expectedMaxSize)
        {
            // let's make it plenty big so it goes several levels deep into recursion
            // power of 8 for a tree that is fully filled too.
            byte[] aArray = new byte[inputSize];
            byte[] bArray = new byte[inputSize];
            for (int i = 0; i < inputSize; ++i)
            {
                aArray[i] = 0x00;
                bArray[i] = 0xFF;
            }

            // check if calculation fits
            Assert.That(BitTree.MaxPatchSize(inputSize), Is.EqualTo(expectedMaxSize));

            // actually compress
            CompressAndDecompress(aArray, bArray, compressWriter, decompressWriter, 0, expectedPatchSize: -1);

            // size should be <= expected max patch size always
            Assert.That(compressWriter.Position, Is.LessThanOrEqualTo(expectedMaxSize));
        }

        [Test]
        public unsafe void Split_0()
        {
            byte[] bytes = {};
            NativeArray<byte> array = new NativeArray<byte>(bytes, Allocator.Temp);

            int* offsets = stackalloc int[8];
            int* lengths = stackalloc int[8];
            BitTree.SplitRaw(array.Length, 8, offsets, lengths);

            // empty
            Assert.That(offsets[0], Is.EqualTo(0));
            Assert.That(lengths[0], Is.EqualTo(0));

            // empty
            Assert.That(offsets[1], Is.EqualTo(0));
            Assert.That(lengths[1], Is.EqualTo(0));

            // empty
            Assert.That(offsets[2], Is.EqualTo(0));
            Assert.That(lengths[2], Is.EqualTo(0));

            // empty
            Assert.That(offsets[3], Is.EqualTo(0));
            Assert.That(lengths[3], Is.EqualTo(0));

            // empty
            Assert.That(offsets[4], Is.EqualTo(0));
            Assert.That(lengths[4], Is.EqualTo(0));

            // empty
            Assert.That(offsets[5], Is.EqualTo(0));
            Assert.That(lengths[5], Is.EqualTo(0));

            // empty
            Assert.That(offsets[6], Is.EqualTo(0));
            Assert.That(lengths[6], Is.EqualTo(0));

            // empty
            Assert.That(offsets[7], Is.EqualTo(0));
            Assert.That(lengths[7], Is.EqualTo(0));

            array.Dispose();
        }

        [Test]
        public unsafe void Split_1()
        {
            byte[] bytes = {0x01};
            NativeArray<byte> array = new NativeArray<byte>(bytes, Allocator.Temp);

            int* offsets = stackalloc int[8];
            int* lengths = stackalloc int[8];
            BitTree.SplitRaw(array.Length, 8, offsets, lengths);

            // 0x01
            Assert.That(offsets[0], Is.EqualTo(0));
            Assert.That(lengths[0], Is.EqualTo(1));

            // empty
            Assert.That(offsets[1], Is.EqualTo(0));
            Assert.That(lengths[1], Is.EqualTo(0));

            // empty
            Assert.That(offsets[2], Is.EqualTo(0));
            Assert.That(lengths[2], Is.EqualTo(0));

            // empty
            Assert.That(offsets[3], Is.EqualTo(0));
            Assert.That(lengths[3], Is.EqualTo(0));

            // empty
            Assert.That(offsets[4], Is.EqualTo(0));
            Assert.That(lengths[4], Is.EqualTo(0));

            // empty
            Assert.That(offsets[5], Is.EqualTo(0));
            Assert.That(lengths[5], Is.EqualTo(0));

            // empty
            Assert.That(offsets[6], Is.EqualTo(0));
            Assert.That(lengths[6], Is.EqualTo(0));

            // empty
            Assert.That(offsets[7], Is.EqualTo(0));
            Assert.That(lengths[7], Is.EqualTo(0));

            array.Dispose();
        }

        [Test]
        public unsafe void Split_7()
        {
            byte[] bytes = {0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07};
            NativeArray<byte> array = new NativeArray<byte>(bytes, Allocator.Temp);

            int* offsets = stackalloc int[8];
            int* lengths = stackalloc int[8];
            BitTree.SplitRaw(array.Length, 8, offsets, lengths);

            // 0x01
            Assert.That(offsets[0], Is.EqualTo(0));
            Assert.That(lengths[0], Is.EqualTo(1));

            // 0x02
            Assert.That(offsets[1], Is.EqualTo(1));
            Assert.That(lengths[1], Is.EqualTo(1));

            // 0x03
            Assert.That(offsets[2], Is.EqualTo(2));
            Assert.That(lengths[2], Is.EqualTo(1));

            // 0x04
            Assert.That(offsets[3], Is.EqualTo(3));
            Assert.That(lengths[3], Is.EqualTo(1));

            // 0x05
            Assert.That(offsets[4], Is.EqualTo(4));
            Assert.That(lengths[4], Is.EqualTo(1));

            // 0x06
            Assert.That(offsets[5], Is.EqualTo(5));
            Assert.That(lengths[5], Is.EqualTo(1));

            // 0x07
            Assert.That(offsets[6], Is.EqualTo(6));
            Assert.That(lengths[6], Is.EqualTo(1));

            // empty
            Assert.That(offsets[7], Is.EqualTo(0));
            Assert.That(lengths[7], Is.EqualTo(0));

            array.Dispose();
        }

        [Test]
        public unsafe void Split_8()
        {
            byte[] bytes = {0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08};
            NativeArray<byte> array = new NativeArray<byte>(bytes, Allocator.Temp);

            int* offsets = stackalloc int[8];
            int* lengths = stackalloc int[8];
            BitTree.SplitRaw(array.Length, 8, offsets, lengths);

            // 0x01
            Assert.That(offsets[0], Is.EqualTo(0));
            Assert.That(lengths[0], Is.EqualTo(1));

            // 0x02
            Assert.That(offsets[1], Is.EqualTo(1));
            Assert.That(lengths[1], Is.EqualTo(1));

            // 0x03
            Assert.That(offsets[2], Is.EqualTo(2));
            Assert.That(lengths[2], Is.EqualTo(1));

            // 0x04
            Assert.That(offsets[3], Is.EqualTo(3));
            Assert.That(lengths[3], Is.EqualTo(1));

            // 0x05
            Assert.That(offsets[4], Is.EqualTo(4));
            Assert.That(lengths[4], Is.EqualTo(1));

            // 0x06
            Assert.That(offsets[5], Is.EqualTo(5));
            Assert.That(lengths[5], Is.EqualTo(1));

            // 0x07
            Assert.That(offsets[6], Is.EqualTo(6));
            Assert.That(lengths[6], Is.EqualTo(1));

            // 0x08
            Assert.That(offsets[7], Is.EqualTo(7));
            Assert.That(lengths[7], Is.EqualTo(1));


            array.Dispose();
        }

        [Test]
        public unsafe void Split_16()
        {
            byte[] bytes = {0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7, 0xA8};
            NativeArray<byte> array = new NativeArray<byte>(bytes, Allocator.Temp);

            int* offsets = stackalloc int[8];
            int* lengths = stackalloc int[8];
            BitTree.SplitRaw(array.Length, 8, offsets, lengths);

            // 0x01, 0x02
            Assert.That(offsets[0], Is.EqualTo(0));
            Assert.That(lengths[0], Is.EqualTo(2));

            // 0x03, 0x04
            Assert.That(offsets[1], Is.EqualTo(2));
            Assert.That(lengths[1], Is.EqualTo(2));

            // 0x05, 0x06
            Assert.That(offsets[2], Is.EqualTo(4));
            Assert.That(lengths[2], Is.EqualTo(2));

            // 0x07, 0x08
            Assert.That(offsets[3], Is.EqualTo(6));
            Assert.That(lengths[3], Is.EqualTo(2));

            // 0xA1, 0xA2
            Assert.That(offsets[4], Is.EqualTo(8));
            Assert.That(lengths[4], Is.EqualTo(2));

            // 0xA3, 0xA4
            Assert.That(offsets[5], Is.EqualTo(10));
            Assert.That(lengths[5], Is.EqualTo(2));

            // 0xA5, 0xA6
            Assert.That(offsets[6], Is.EqualTo(12));
            Assert.That(lengths[6], Is.EqualTo(2));

            // 0xA7, 0xA8
            Assert.That(offsets[7], Is.EqualTo(14));
            Assert.That(lengths[7], Is.EqualTo(2));

            array.Dispose();
        }

        [Test]
        public unsafe void Split_18()
        {
            byte[] bytes = {0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7, 0xA8, 0xF1, 0xF2};
            NativeArray<byte> array = new NativeArray<byte>(bytes, Allocator.Temp);

            int* offsets = stackalloc int[8];
            int* lengths = stackalloc int[8];
            BitTree.SplitRaw(array.Length, 8, offsets, lengths);

            // 0x01, 0x02, 0x03
            Assert.That(offsets[0], Is.EqualTo(0));
            Assert.That(lengths[0], Is.EqualTo(3));

            // 0x04, 0x05, 0x06
            Assert.That(offsets[1], Is.EqualTo(3));
            Assert.That(lengths[1], Is.EqualTo(3));

            // 0x07, 0x08
            Assert.That(offsets[2], Is.EqualTo(6));
            Assert.That(lengths[2], Is.EqualTo(2));

            // 0xA1, 0xA2
            Assert.That(offsets[3], Is.EqualTo(8));
            Assert.That(lengths[3], Is.EqualTo(2));

            // 0xA3, 0xA4
            Assert.That(offsets[4], Is.EqualTo(10));
            Assert.That(lengths[4], Is.EqualTo(2));

            // 0xA5, 0xA6
            Assert.That(offsets[5], Is.EqualTo(12));
            Assert.That(lengths[5], Is.EqualTo(2));

            // 0xA7, 0xA8
            Assert.That(offsets[6], Is.EqualTo(14));
            Assert.That(lengths[6], Is.EqualTo(2));

            // 0xF1, 0xF2
            Assert.That(offsets[7], Is.EqualTo(16));
            Assert.That(lengths[7], Is.EqualTo(2));

            array.Dispose();
        }

        // easy example for debugging / understanding
        //
        //                   01 02 03 04 05 06 07 08 F1 F2 F3 F4 F6 F6 F7 F8 11 12 13 14 15 16 17 18 E1 E2 E3 E4 E6 E6 E7 E8
        //                01 02 03 04 05 06 07 08 | F1 F2 F3 F4 F6 F6 F7 F8 | 11 12 13 14 15 16 17 18 | E1 E2 E3 E4 E6 E6 E7 E8
        // 01|02|03|04|05|06|07|08      F1|F2|F3|F4|F6|F6|F7|F8      11|12|13|14|15|16|17|18      E1|E2|E3|E4|E6|E6|E7|E8
        [Test]
        public void EasyExample_Same()
        {
            byte[] aArray = {0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0xF1, 0xF2, 0xF3, 0xF4, 0xF6, 0xF6, 0xF7, 0xF8, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0xE1, 0xE2, 0xE3, 0xE4, 0xE6, 0xE6, 0xE7, 0xE8};
            byte[] bArray = {0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0xF1, 0xF2, 0xF3, 0xF4, 0xF6, 0xF6, 0xF7, 0xF8, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0xE1, 0xE2, 0xE3, 0xE4, 0xE6, 0xE6, 0xE7, 0xE8};
            CompressAndDecompress(aArray, bArray, compressWriter, decompressWriter, 2, expectedPatchSize: -1);
        }

        [Test]
        public void EasyExample_Different()
        {
            byte[] aArray = {0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0xF1, 0xF2, 0xF3, 0xF4, 0xF6, 0xF6, 0xF7, 0xF8, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0xE1, 0xE2, 0xE3, 0xE4, 0xE6, 0xE6, 0xE7, 0xE8};
            byte[] bArray = {0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0xF1, 0xF2, 0xF3, 0xF4, 0xF6, 0xF6, 0xF7, 0xF8, 0x00, 0x12, 0x13, 0x00, 0x0, 0x16, 0x17, 0x18, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
            CompressAndDecompress(aArray, bArray, compressWriter, decompressWriter, 2, expectedPatchSize: -1);
        }
    }
}
