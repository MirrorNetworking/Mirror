// This Class implements the Difference Algorithm published in
// "An O(ND) Difference Algorithm and its Variations" by Eugene Myers
// Algorithmica Vol. 1 No. 2, 1986, p 251.
//
// vis2k: Myers Diff paper summary (read the original!):
// * Figure 1 graph explains it beautifully:
//     horizontal line = insert
//     vertical line = delete
//     diagonal line = keep
// * The problem of finding a longest common subsequence (LCS) is equivalent to
//   finding a path from (0,0) to (N,M) with the maximum number of diagonal
//   edges.
// * The problem of finding a shortest edit script (SES) is equivalent to
//   finding a path from (0,0) to (N,M) with the minimum number of non-diagonal
//   edges.
// => These are dual problems as a path with the maximum number of diagonal
//    edges has the minimal number of non-diagonal edges (D+2L = M+N).
//
// * Consider adding a weight or cost to every edge. Give diagonal edges weight
//   0 and non-diagonal edges weight 1. The LCS/SES problem is equivalent to
//   finding a minimum-cost path from (0,0) to (N,M) in the weighted edit graph
//   and is thus a special instance of the single-source shortest path problem.
//
//
// M. Hertel notes for original C# implemenation:
//
// There are many C, Java, Lisp implementations public available but they all seem to come
// from the same source (diffutils) that is under the (unfree) GNU public License
// and cannot be reused as a sourcecode for a commercial application.
// There are very old C implementations that use other (worse) algorithms.
// Microsoft also published sourcecode of a diff-tool (windiff) that uses some tree data.
// Also, a direct transfer from a C source to C# is not easy because there is a lot of pointer
// arithmetic in the typical C solutions and i need a managed solution.
// These are the reasons why I implemented the original published algorithm from the scratch and
// make it avaliable without the GNU license limitations.
// I do not need a high performance diff tool because it is used only sometimes.
// I will do some performace tweaking when needed.
//
// The algorithm itself is comparing 2 arrays of numbers so when comparing 2 text documents
// each line is converted into a (hash) number. See DiffText().
//
// Some chages to the original algorithm:
// The original algorithm was described using a recursive approach and comparing zero indexed arrays.
// Extracting sub-arrays and rejoining them is very performance and memory intensive so the same
// (readonly) data arrays are passed arround together with their lower and upper bounds.
// This circumstance makes the LCS and SMS functions more complicate.
// I added some code to the LCS function to get a fast response on sub-arrays that are identical,
// completely deleted or inserted.
//
// The result from a comparisation is stored in 2 arrays that flag for modified (deleted or inserted)
// lines in the 2 data arrays. These bits are then analysed to produce a array of Item objects.
//
// Further possible optimizations:
// (first rule: don't do it; second: don't do it yet)
// The arrays DataA and DataB are passed as parameters, but are never changed after the creation
// so they can be members of the class to avoid the paramter overhead.
// In SMS is a lot of boundary arithmetic in the for-D and for-k loops that can be done by increment
// and decrement of local variables.
// The DownVector and UpVector arrays are alywas created and destroyed each time the SMS gets called.
// It is possible to reuse tehm when transfering them to members of the class.
// See TODO: hints.
//
// diff.cs: A port of the algorythm to C#
// Copyright (c) by Matthias Hertel, http://www.mathertel.de
// This work is licensed under a BSD style license. See http://www.mathertel.de/License.aspx
//
// Changes:
// 2002.09.20 There was a "hang" in some situations.
// Now I undestand a little bit more of the SMS algorithm.
// There have been overlapping boxes; that where analyzed partial differently.
// One return-point is enough.
// A assertion was added in CreateDiffs when in debug-mode, that counts the number of equal (no modified) lines in both arrays.
// They must be identical.
//
// 2003.02.07 Out of bounds error in the Up/Down vector arrays in some situations.
// The two vetors are now accessed using different offsets that are adjusted using the start k-Line.
// A test case is added.
//
// 2006.03.05 Some documentation and a direct Diff entry point.
//
// 2006.03.08 Refactored the API to static methods on the Diff class to make usage simpler.
// 2006.03.10 using the standard Debug class for self-test now.
//            compile with: csc /target:exe /out:diffTest.exe /d:DEBUG /d:TRACE /d:SELFTEST Diff.cs
// 2007.01.06 license agreement changed to a BSD style license.
// 2007.06.03 added the Optimize method.
// 2007.09.23 UpVector and DownVector optimization by Jan Stoklasa ().
// 2008.05.31 Adjusted the testing code that failed because of the Optimize method (not a bug in the diff algorithm).
// 2008.10.08 Fixing a test case and adding a new test case.
using System;
using System.Collections.Generic;

namespace MyersDiffX
{
    public class MyersDiffX
    {
        // Find the difference in 2 arrays (bytes / strings / etc.).
        // Returns a array of Items that describe the differences.
        // result as parameter to avoid allocations (i.e. in games).
        public static List<Item> Diff<T>(T[] A, T[] B)
            // need ICompareable for <>, need IEquatable<T> to avoid .Equals boxing
            where T : struct, IComparable, IEquatable<T>
        {
            // allocate the lists.
            // already with expected capacity to avoid resizing.
            List<Item> result = new List<Item>();
            bool[] modifiedA = new bool[A.Length + 2];
            bool[] modifiedB = new bool[B.Length + 2];

            // Up/DownVector as reusable int[] that we only resize if necessary.
            // need two vectors of size 2 * MAX + 2
            int MAX = A.Length + B.Length + 1;
            int VECTOR_SIZE = 2 * MAX + 2;
            // vector for the (0,0) to (x,y) search
            int[] DownVector = new int[VECTOR_SIZE];
            // vector for the (u,v) to (N,M) search
            int[] UpVector = new int[VECTOR_SIZE];

            DiffNonAlloc(new ArraySegmentX<T>(A), new ArraySegmentX<T>(B), ref modifiedA, ref modifiedB, ref DownVector, ref UpVector, result);
            return result;
        }

        // Allocation free version where helpers are passed as parameters.
        // useful for games etc. that need to avoid runtime allocations.
        // -> A, B are the input arrays
        //    ArraySegment to avoid allocations.
        //    Mirror's NetworkWriter can use .ToArraySegment and avoid allocs.
        // -> the lists are to avoid allocations
        // -> reusable ref bool[] & ref int[] that we only resize if necessary
        //    DO NOT USE .LENGTH as it might be larger than valid data range.
        public static void DiffNonAlloc<T>(ArraySegmentX<T> A, ArraySegmentX<T> B,
                                           ref bool[] modifiedA, ref bool[] modifiedB,
                                           ref int[] DownVector, ref int[] UpVector,
                                           List<Item> result)
            // need ICompareable for <>, need IEquatable<T> to avoid .Equals boxing
            where T : struct, IComparable, IEquatable<T>
        {
            // initialize result list
            result.Clear();

            // only resize (and reallocate) modified arrays if too small
            if (modifiedA.Length < A.Count + 2) Array.Resize(ref modifiedA, A.Count + 2);
            if (modifiedB.Length < B.Count + 2) Array.Resize(ref modifiedB, B.Count + 2);

            // initialize the modified arrays.
            // TODO is this necessary, or does the algo set all values anyway?
            for (int i = 0; i < A.Count + 2; ++i) modifiedA[i] = false;
            for (int i = 0; i < B.Count + 2; ++i) modifiedB[i] = false;

            // the two vectors need to be at least of size 2 * MAX + 2.
            // only resize (and reallocate) if too small..
            int MAX = A.Count + B.Count + 1;
            int VECTOR_SIZE = 2 * MAX + 2;
            if (DownVector.Length < VECTOR_SIZE) Array.Resize(ref DownVector, VECTOR_SIZE);
            if (UpVector.Length < VECTOR_SIZE) Array.Resize(ref UpVector, VECTOR_SIZE);

            // initialize the vector arrays.
            // NOTE: only from [0..VECTOR_SIZE]. everything beyond we don't use.
            // NOTE: List<int> would be significantly slower!
            // TODO is this necessary, or does the algo set all values anyway?
            for (int i = 0; i < VECTOR_SIZE; ++i)
            {
                DownVector[i] = 0;
                UpVector[i] = 0;
            }

            LongestCommonSubsequence(A, modifiedA, 0, A.Count,
                                     B, modifiedB, 0, B.Count,
                                     DownVector, UpVector);

            // CreateDiffs need to know valid size of modified arrays
            CreateDiffs(new ArraySegmentX<bool>(modifiedA, 0, A.Count + 2),
                        new ArraySegmentX<bool>(modifiedB, 0, B.Count + 2),
                        result);
        }

        // This is the algorithm to find the Shortest Middle Snake (SMS).
        //   A[]: sequence A
        //   LowerA: lower bound of the actual range in DataA
        //   UpperA: upper bound of the actual range in DataA (exclusive)
        //   B[]: sequence B
        //   LowerB: lower bound of the actual range in DataB
        //   UpperB: upper bound of the actual range in DataB (exclusive)
        //   DownVector: a vector for the (0,0) to (x,y) search. Passed as a parameter for speed reasons.
        //   UpVector: a vector for the (u,v) to (N,M) search. Passed as a parameter for speed reasons.
        // -> reusable int[] that we only resize if necessary
        //    DO NOT USE .LENGTH as it might be larger than valid data range.
        // Returns a MiddleSnakeData record containing x,y (u,v aren't needed)
        // => return values as 'out' because burst doesn't suppor Tuple returns.
        //
        // NOTE that google uses almost the same algorithm:
        // https://github.com/google/diff-match-patch/blob/62f2e689f498f9c92dbc588c58750addec9b1654/csharp/DiffMatchPatch.cs#L448
        internal static void ShortestMiddleSnake<T>(
            ArraySegmentX<T> A, int LowerA, int UpperA,
            ArraySegmentX<T> B, int LowerB, int UpperB,
            int[] DownVector, int[] UpVector,
            out int resultX, out int resultY)
            // need ICompareable for <>, need IEquatable<T> to avoid .Equals boxing
            where T : struct, IComparable, IEquatable<T>
        {
            int MAX = A.Count + B.Count + 1;

            int DownK = LowerA - LowerB; // the k-line to start the forward search
            int UpK = UpperA - UpperB; // the k-line to start the reverse search

            int Delta = (UpperA - LowerA) - (UpperB - LowerB);
            bool oddDelta = (Delta & 1) != 0;

            // The vectors in the publication accepts negative indexes. the vectors implemented here are 0-based
            // and are access using a specific offset: UpOffset UpVector and DownOffset for DownVektor
            int DownOffset = MAX - DownK;
            int UpOffset = MAX - UpK;

            int MaxD = ((UpperA - LowerA + UpperB - LowerB) / 2) + 1;

            // Debug.Write(2, "SMS", String.Format("Search the box: A[{0}-{1}] to B[{2}-{3}]", LowerA, UpperA, LowerB, UpperB));

            // init vectors
            DownVector[DownOffset + DownK + 1] = LowerA;
            UpVector[UpOffset + UpK - 1] = UpperA;

            for (int D = 0; D <= MaxD; D++)
            {
                // Extend the forward path.
                for (int k = DownK - D; k <= DownK + D; k += 2)
                {
                    // Debug.Write(0, "SMS", "extend forward path " + k.ToString());

                    // find the only or better starting point
                    int x, y;
                    if (k == DownK - D)
                    {
                        x = DownVector[DownOffset + k + 1]; // down
                    }
                    else
                    {
                        x = DownVector[DownOffset + k - 1] + 1; // a step to the right
                        if ((k < DownK + D) && (DownVector[DownOffset + k + 1] >= x))
                            x = DownVector[DownOffset + k + 1]; // down
                    }
                    y = x - k;

                    // find the end of the furthest reaching forward D-path in diagonal k.
                    while ((x < UpperA) && (y < UpperB) && (A.Array[x].Equals(B.Array[y])))
                    {
                        x++; y++;
                    }
                    DownVector[DownOffset + k] = x;

                    // overlap ?
                    if (oddDelta && (UpK - D < k) && (k < UpK + D))
                    {
                        if (UpVector[UpOffset + k] <= DownVector[DownOffset + k])
                        {
                            resultX = DownVector[DownOffset + k];
                            resultY = DownVector[DownOffset + k] - k;
                            return;
                                    // 2002.09.20: no need for 2 points
                                    //UpVector[UpOffset + k],
                                    //UpVector[UpOffset + k] - k;
                        }
                    }
                } // for k

                // Extend the reverse path.
                for (int k = UpK - D; k <= UpK + D; k += 2)
                {
                    // Debug.Write(0, "SMS", "extend reverse path " + k.ToString());

                    // find the only or better starting point
                    int x, y;
                    if (k == UpK + D)
                    {
                        x = UpVector[UpOffset + k - 1]; // up
                    }
                    else
                    {
                        x = UpVector[UpOffset + k + 1] - 1; // left
                        if ((k > UpK - D) && (UpVector[UpOffset + k - 1] < x))
                            x = UpVector[UpOffset + k - 1]; // up
                    }
                    y = x - k;

                    while ((x > LowerA) && (y > LowerB) && (A.Array[x - 1].Equals(B.Array[y - 1])))
                    {
                      x--; y--; // diagonal
                    }
                    UpVector[UpOffset + k] = x;

                    // overlap ?
                    if (!oddDelta && (DownK - D <= k) && (k <= DownK + D))
                    {
                        if (UpVector[UpOffset + k] <= DownVector[DownOffset + k])
                        {
                            resultX = DownVector[DownOffset + k];
                            resultY = DownVector[DownOffset + k] - k;
                            return;
                                    // 2002.09.20: no need for 2 points
                                    //UpVector[UpOffset + k],
                                    //UpVector[UpOffset + k] - k;
                        } // if
                    } // if
                } // for k
            } // for D

            throw new ApplicationException("the algorithm should never come here.");
        } // SMS

        // This is the divide-and-conquer implementation of the longes common-subsequence (LCS)
        // algorithm.
        // The published algorithm passes recursively parts of the A and B sequences.
        // To avoid copying these arrays the lower and upper bounds are passed while the sequences stay constant.
        //   A: sequence A
        //   modifiedA: modified array for A (=deleted)
        //   LowerA: lower bound of the actual range in DataA
        //   UpperA: upper bound of the actual range in DataA (exclusive)
        //   B: sequence B
        //   modifiedB: modified array for B (=inserted)
        //   LowerB: lower bound of the actual range in DataB
        //   UpperB: upper bound of the actual range in DataB (exclusive)
        //   DownVector: a vector for the (0,0) to (x,y) search. Passed as a parameter for speed reasons.
        //   UpVector: a vector for the (u,v) to (N,M) search. Passed as a parameter for speed reasons.
        // -> reusable int[] & bool[] that we only resize if necessary
        //    DO NOT USE .LENGTH as it might be larger than valid data range.
        static void LongestCommonSubsequence<T>(
            ArraySegmentX<T> A, bool[] modifiedA, int LowerA, int UpperA,
            ArraySegmentX<T> B, bool[] modifiedB, int LowerB, int UpperB,
            int[] DownVector, int[] UpVector)
            // need ICompareable for <>, need IEquatable<T> to avoid .Equals boxing
            where T : struct, IComparable, IEquatable<T>
        {
            // Debug.Write(2, "LCS", String.Format("Analyse the box: A[{0}-{1}] to B[{2}-{3}]", LowerA, UpperA, LowerB, UpperB));

            // Fast walkthrough equal lines at the start
            while (LowerA < UpperA && LowerB < UpperB && A.Array[LowerA].Equals(B.Array[LowerB]))
            {
                LowerA++; LowerB++;
            }

            // Fast walkthrough equal lines at the end
            while (LowerA < UpperA && LowerB < UpperB && A.Array[UpperA - 1].Equals(B.Array[UpperB - 1]))
            {
                --UpperA; --UpperB;
            }

            if (LowerA == UpperA)
            {
                // mark as inserted lines.
                while (LowerB < UpperB)
                    modifiedB[LowerB++] = true;
            }
            else if (LowerB == UpperB)
            {
                // mark as deleted lines.
                while (LowerA < UpperA)
                    modifiedA[LowerA++] = true;
            }
            else
            {
                // Find the middle snake and length of an optimal path for A and B
                ShortestMiddleSnake(A, LowerA, UpperA, B, LowerB, UpperB, DownVector, UpVector, out int x, out int y);
                // Debug.Write(2, "MiddleSnakeData", String.Format("{0},{1}", smsrd.x, smsrd.y));

                // The path is from LowerX to (x,y) and (x,y) to UpperX
                LongestCommonSubsequence(A, modifiedA, LowerA, x,
                                         B, modifiedB, LowerB, y,
                                         DownVector, UpVector);
                LongestCommonSubsequence(A, modifiedA, x, UpperA,
                                         B, modifiedB, y, UpperB,
                                         DownVector, UpVector);  // 2002.09.20: no need for 2 points
            }
        }

        // CreateDiffs helper function to walk through A/B modified
        // for A, that means walk through deleted.
        // for B, that means walk through inserted.
        // both A and B can reuse the same function.
        //   index, length, modified are our own (a/lengthA/modifiedA or vice versa)
        //   otherIndex, otherLength are the other (b/lengthB or vice versa)
        // -> modified[]s are reusable, where length might be larger than valid
        //    data range. that's why we pass an ArraySegment with valid range.
        static int WalkModified(int index, int length, ArraySegmentX<bool> modified, int otherIndex, int otherLength)
        {
            // end of other reached yet? then jump straight to the end.
            // (check avoids deadlock, see EdgeCase_Increase test)
            if (otherIndex >= otherLength)
                return length;

            // end of other not reached yet? then walk while modified
            while (index < length && modified[index])
                ++index;

            return index;
        }

        // Scan the tables of which lines are inserted and deleted,
        // producing an edit script in forward order.
        // result as parameter to avoid allocations (i.e. in games).
        //   lengthA: length of A[]
        //   modifiedA: bool modification[] from DiffData
        //   lengthB: length of B[]
        //   modifiedB: bool modification[] from DiffData
        // -> modified[]s are reusable, where length might be larger than valid
        //    data range. that's why we pass an ArraySegment with valid range.
        internal static void CreateDiffs(ArraySegmentX<bool> modifiedA, ArraySegmentX<bool> modifiedB, List<Item> result)
        {
            // modified [] is always original length + 2.
            // calculate original length instead of passing it as parameter too.
            int lengthA = modifiedA.Count - 2;
            int lengthB = modifiedB.Count - 2;

            // indices for both arrays
            int a = 0;
            int b = 0;

            // keep going until BOTH lineA AND lineB reached the max length
            while (a < lengthA || b < lengthB)
            {
                // walk through a and b while both unmodified
                if (a < lengthA && !modifiedA[a] &&
                    b < lengthB && !modifiedB[b])
                {
                    ++a;
                    ++b;
                }
                // one or both of them modified, or at max length
                else
                {
                    // remember indices before walking
                    int StartA = a;
                    int StartB = b;

                    // walk through A modified (=deleted) entries
                    // walk through B modified (=inserted) entries
                    a = WalkModified(a, lengthA, modifiedA, b, lengthB);
                    b = WalkModified(b, lengthB, modifiedB, a, lengthA);

                    // if either of them changed: store the change
                    if (a != StartA || b != StartB)
                        result.Add(new Item(StartA, StartB, a - StartA, b - StartB));
                }
            }
        }
    }
}
