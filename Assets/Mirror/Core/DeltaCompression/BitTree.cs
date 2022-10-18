// BitTree delta compression from DOTSNET / Mirror II by vis2k/Mischa.
// modified to Mirror's resizing NetworkWriter with void returns instead of bool.
//
// essentially N-tree / octree applied to a byte stream.
// https://en.wikipedia.org/wiki/Octree
//
// << RAW >> version with unsafe pointers instead of NativeSlice is 2x faster!
//
// recursive bit flags to indicate changed areas in equal sized byte[]s.
// similarly to how we previously had dirty bits for Entity->Component->Field.
// but this can go even higher to groups of entities.
//
// this is similar to binary/octrees.
//
//          12345678ABCDEFGH
//        12345678 | ABCDEFGH
// 1|2|3|4|5|6|7|8   A|B|C|D|E|F|G|H
//
// each graph child is encoded with 1 bit 'changed'.
// each one can have their own children if size still > 4.
//
// the tree can be a binary(2)/quad(4)/oct(8) tree etc.
// => octree is ideal so that we need exactly 8 bit = 1 byte per encoding.
// => otherwise we would need bitpacking...
// => octree also reduces tree depth, compared to binary tree
using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Mirror
{
    public static class BitTree
    {
        // octree
        const int dimension = 8;

        // calculate max number of nodes for octree at level N
        // this is not for total tree nodes!
        public static int MaxNodesAtLevel(int level)
        {
            // formula works perfectly for all values, except '0'
            if (level == 0) return 0;
            return Utils.Pow(dimension, level);
        }

        // calculate max number of nodes in tree for octree with depth N
        public static int MaxNodesInTree(int depth)
        {
            // sum nodes for every level from 0..depth
            // TODO formula
            int total = 0;
            for (int i = 0; i <= depth; ++i)
                total += MaxNodesAtLevel(i);
            return total;
        }

        // calculate tree depth for octree based on original input size
        public static int TreeDepth(int inputSize)
        {
            // in Unity 2020.3 LTS, it fails for '0' too. in 2021 it works.
            if (inputSize == 0) return 0;
            // formula works perfectly for all values, except '1'..
            if (inputSize == 1) return 1;
            return Mathf.FloorToInt(Mathf.Log(inputSize-1, dimension) + 1);
        }

        // calculate max nodes in a tree generated for a given input
        public static int MaxNodesForGeneratedTree(int inputSize)
        {
            // calculate tree height for input.
            // then calculate max nodes for that tree height.
            return MaxNodesInTree(TreeDepth(inputSize));
        }

        // predict max patch data overhead for input size.
        public static int MaxOverhead(int inputSize)
        {
            // calculate amount of max nodes (=bits) in the generated tree
            int maxBits = MaxNodesForGeneratedTree(inputSize);
            return Utils.RoundBitsToFullBytes(maxBits);
        }

        // predict max patch size (input + overhead)
        public static int MaxPatchSize(int inputSize) =>
            inputSize + MaxOverhead(inputSize);

        // helper function to divide into N parts.
        // this is a little bit tricky, because we always want to have N parts.
        // each parts can have multiple entries.
        //
        // for example:
        //   [1,2,3,4,5,6,7] => [1],[2],[3],[4],[5],[6],[7],[]
        //   [1,2,3,4,5,6,7,8] => [1],[2],[3],[4],[5],[6],[7],[8]
        //   [1,2,3,4,5,6,7,8,9] => [1,2],[3],[4],[5],[6],[7],[8],[9]
        //   [1,2,3,4,5,6,7,8,9,10] => [1,2],[3,4],[5],[6],[7],[8],[9],[10]
        //
        // writes into byte* which needs to have a length of 'parts'.
        // internal for testing as this is not obvious.
        //
        // NativeSlice doesn't save .offset, so we put them into offsets/lengths separately.
        internal static unsafe void SplitRaw(int collectionLength, int parts, int* offsets, int* lengths)
        {
            // calculate bytes.Length / parts, but also get the remainder.
            // for example, 10 / 8 gives div=1, rem=2
            // meaning each part has length of '1', and '2' are remaining.
            int div = Math.DivRem(collectionLength, parts, out int rem);
            //Debug.Log($"div={div} rem={rem}");

            int sliceStart = 0;
            for (int i = 0; i < parts; ++i)
            {
                // calculate how many entries this part will have..
                // each part has 'div' entries.
                // and we need to split 'rem' remaining entries across parts.
                // -> ideally across the first parts
                // -> in fact, across the first 'rem' parts
                // so if i <= rem, we need to add one more.
                int entries = div;
                if (i < rem) ++entries;
                //Debug.Log($"part {i+1} has {entries} entries");

                // build a slice with that many entries
                // offset is only set if there's at least one entry..
                offsets[i] = entries > 0 ? sliceStart : 0;
                lengths[i] = entries;
                //result[i] = new NativeSlice<T>(slice, sliceStart, entries);
                sliceStart += entries;
            }
        }

        // pass previous.GetUnsafePtr & current.GetUnsafePtr to avoid having to
        // call GetUnsafePtr, which is quite expensive.
        static unsafe void CompressRecursively(byte* previousPtr, byte* currentPtr, int size, NetworkWriter patch)
        {
            //Debug.Log("--------");
            //Debug.Log($"previous={previous.ToHexString()}");

            // allocate 8 changed flags without GC
            bool* changed = stackalloc bool[dimension];

            // divide into 8 parts.
            int* offsets = stackalloc int[dimension];
            int* lengths = stackalloc int[dimension];
            SplitRaw(size, dimension, offsets, lengths);

            // calculate 8 new pointers (just like creating new native slices)
            byte** previousParts = stackalloc byte*[dimension];
            byte** currentParts = stackalloc byte*[dimension];
            for (int i = 0; i < dimension; ++i)
            {
                previousParts[i] = previousPtr + offsets[i];
                currentParts[i] = currentPtr + offsets[i];
            }

            // encode 8 bit = 1 byte
            // changed flags are encoded into:
            //   0b10000000
            //   0b01000000
            //   0b00100000
            //   0b00010000
            //   0b00001000
            //   0b00000100
            //   0b00000010
            //   0b00000001
            byte encoding = 0b00000000;

            // compare contents.
            // TODO we could probably get 'equals' from the children later.
            // TODO currently we would compare unequal parts over and over...
            // although if we compare here then we don't need to go deeper if same.
            for (int i = 0; i < dimension; ++i)
            {
                int partLength = lengths[i];
                changed[i] = UnsafeUtility.MemCmp(previousParts[i], currentParts[i], partLength) != 0;

                // build the encoding in the same for loop.
                // no need to do a separate loop.
                //
                // when debugging, we want the first changed bit to be on the left.
                // so let's shift from left to right
                byte flag = (byte)(changed[i] ? 0b10000000 : 0);
                byte nthBit = (byte)(flag >> i);
                encoding |= nthBit;
            }
            //Debug.Log($"encoding: {Convert.ToString(encoding, 2).PadLeft(8,'0')}");

            // print all parts for debugging
            // indicate changed/equal too.
            //string previousStr = "previous split: |";
            //string currentStr = "current split:  |";
            //for (int i = 0; i < dimension; ++i)
            //{
            //    previousStr += $"{previousParts[i].ToHexString()} | ";
            //    currentStr += $"{currentParts[i].ToHexString()}{(changed[i] ? "!" : " ")}| ";
            //}
            //Debug.Log(previousStr);
            //Debug.Log(currentStr);

            // write encoding
            patch.WriteByte(encoding);

            // are we down to the lowest level, with size <= 8 and
            // A,B,C,D,E,F,G,H being 1 byte each?
            if (size <= dimension)
            {
                // debug log
                //string bottom = "bottom: |";
                //for (int i = 0; i < dimension; ++i)
                //    bottom += $" {previousParts[i].ToHexString()} {(changed[i] ? "!=" : "==")} {currentParts[i].ToHexString()} |";
                //Debug.Log(bottom);

                // write each changed byte.
                for (int i = 0; i < dimension; ++i)
                {
                    // make sure we only write within bounds
                    byte* currentPartPtr = currentParts[i];
                    int currentPartLength = lengths[i];
                    if (currentPartLength > 0 && changed[i])
                    {
                        patch.WriteByte(currentPartPtr[0]);
                    }
                }
            }
            // continue recursively for all changed parts.
            // even if Length <= 8, we want to split them into A,B,C,D,E,F,G,H bytes.
            else
            {
                for (int i = 0; i < dimension; ++i)
                    if (changed[i])
                        CompressRecursively(previousParts[i], currentParts[i], lengths[i], patch);
            }
        }

        // delta compress 'previous' against 'current' based on block size.
        // writes patch into 'patch' writer.
        // RETURNS true if enough space, false otherwise.
        //         just like the rest of DOTSNET>
        // NOTE: respects content before 'patch.Position', for example DOTSNET
        //       has written message id to it already.
        public static unsafe void Compress(ArraySegment<byte> previous, ArraySegment<byte> current, NetworkWriter patch)
        {
            // only same sized arrays are allowed.
            // exception to indicate that this needs to be fixed immediately.
            if (previous.Count != current.Count)
                throw new ArgumentException($"BitTree.Compress: only works on same sized data. Make sure that serialized data always has the same length. Previous {previous.Count} != Current={current.Count} bytes");

            // guarantee that patch writer has enough space for max sized patch.
            // exception to indicate that this needs to be fixed immediately.
            // int maxPatchSize = MaxPatchSize(previous.Count);
            // if (patch.Space < maxPatchSize)
            //     //throw new ArgumentException($"DeltaCompression.Compress: patch writer with Position={patch.Position} Space={patch.Space} is too small for max patch size of {maxPatchSize} bytes for input of {length} bytes");
            //     return false;

            // write nothing if completely empty.
            // otherwise we would write 8 bit as soon as we go into recursion.
            if (previous.Count == 0)
                return;

            fixed (byte* previousPtr = &previous.Array[previous.Offset],
                         currentPtr = &current.Array[current.Offset])
            {

                //Debug.Log("--------");
                //Debug.Log($"BitTree Compressing...");
                CompressRecursively(previousPtr, currentPtr, previous.Count, patch);
            }
        }

        // pass previous.GetUnsafePtr & current.GetUnsafePtr to avoid having to
        // call GetUnsafePtr, which is quite expensive.
        static unsafe void DecompressRecursively(byte* previousPtr, int size, NetworkReader patch, NetworkWriter current)
        {
            //Debug.Log("--------");
            //Debug.Log($"previous={previous.ToHexString()}");

            // allocate 8 changed flags without GC
            bool* changed = stackalloc bool[dimension];

            // divide into 8 parts.
            int* offsets = stackalloc int[dimension];
            int* lengths = stackalloc int[dimension];
            SplitRaw(size, dimension, offsets, lengths);

            // calculate 8 new pointers (just like creating new native slices)
            byte** previousParts = stackalloc byte*[dimension];
            for (int i = 0; i < dimension; ++i)
                previousParts[i] = previousPtr + offsets[i];

            // read encoding
            // if (!patch.ReadByte(out byte encoding))
            //     throw new IndexOutOfRangeException($"BitTree Compression: failed to read encoding. This should never happen.");
            //Debug.Log($"encoding: {Convert.ToString(encoding, 2).PadLeft(8,'0')}");
            byte encoding = patch.ReadByte();

            // read flags, encoded as 8 bits
            for (int i = 0; i < dimension; ++i)
            {
                // when debugging, we want the first changed bit to be on the left.
                // so let's shift from left to right
                // so let's iterate backwards to make it look nicer.
                byte nthBit = (byte)(0b10000000 >> i);
                changed[i] = (encoding & nthBit) != 0;
            }

            // print all parts for debugging
            // indicate changed/equal too.
            //string previousStr = "previous split: |";
            //for (int i = 0; i < dimension; ++i)
            //    previousStr += $"{previousParts[i].ToHexString()} | ";
            //Debug.Log(previousStr);

            // are we down to the lowest level, with size <= 8 and
            // A,B,C,D,E,F,G,H being 1 byte each?
            if (size <= dimension)
            {
                // debug log
                //Debug.Log("bottom!");

                // read each byte from original or patch if changed
                for (int i = 0; i < dimension; ++i)
                {
                    // make sure we only read within bounds
                    byte* previousPartPtr = previousParts[i];
                    int previousPartLength = lengths[i];
                    if (previousPartLength > 0)
                    {
                        byte previousByte = previousPartPtr[0];
                        if (changed[i]) previousByte = patch.ReadByte();
                        current.WriteByte(previousByte);
                    }
                }
            }
            // reconstruct each part
            else
            {
                for (int i = 0; i < dimension; ++i)
                {
                    byte* previousPartPtr = previousParts[i];
                    int previousPartLength = lengths[i];

                    // previous != current: continue recursively
                    if (changed[i])
                        DecompressRecursively(previousPartPtr, previousPartLength, patch, current);
                    // previous == current: copy original
                    else if (!current.WriteBytes(previousPartPtr, 0, previousPartLength))
                        throw new IndexOutOfRangeException($"BitTree Compression.Decompress: failed to write previous {i} chunk");
                }
            }
        }

        // apply patch onto previous based on block size.
        // writes result into 'current' writer.
        // returns true if succeeded. fails if not enough space in patch/result.
        public static unsafe void Decompress(ArraySegment<byte> previous, NetworkReader patch, NetworkWriter current)
        {
            //Debug.Log("--------");
            //Debug.Log($"BitTree Decompressing...");
            //UnityEngine.Debug.Log("Decopmress: reading " + patch.Position + " bits");

            int length = previous.Count;

            // result size will be same as input.
            // make sure writer has enough space.
            // if (current.Space < length)
            //     throw new IndexOutOfRangeException($"DeltaCompression.Decompress: input with {length} bytes is too large for writer {current.Space} bytes");

            // read nothing if completely empty.
            // otherwise we would read 8 bit as soon as we go into recursion.
            if (previous.Count == 0)
                return;

            // IMPORTANT: we DON'T need to read a size header.
            // the size is always == last.size!

            fixed (byte* previousPtr = &previous.Array[previous.Offset])
            {
                DecompressRecursively(previousPtr, previous.Count, patch, current);
            }
        }
    }
}
