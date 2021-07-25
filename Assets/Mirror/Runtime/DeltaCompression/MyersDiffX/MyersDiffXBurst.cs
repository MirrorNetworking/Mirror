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
using Unity.Collections;

namespace MyersDiffX
{
    public class MyersDiffXBurst
    {
        // NativeArray version WIP
        // -> modified arrays of byte instead of bool for burst
        public static void DiffNonAlloc(NativeArray<byte> A, NativeArray<byte> B,
                NativeList<byte> modifiedA, NativeList<byte> modifiedB,
                NativeList<int> DownVector, NativeList<int> UpVector,
                NativeList<Item> result)
        {
            // initialize result list
            result.Clear();

            // initialize the modified arrays.
            // new bool[size] initializes them to 'false'.
            // we need to initialize our list manually.
            // that's the price to pay to avoid allocations.
            modifiedA.Clear();
            modifiedB.Clear();
            // TODO is this necessary, or does the algo set all values anyway?
            for (int i = 0; i < A.Length + 2; ++i) modifiedA.Add(0);
            for (int i = 0; i < B.Length + 2; ++i) modifiedB.Add(0);

            // initialize the vector arrays.
            // new int[size] initializes them to '0'.
            // we need to initialize our list manually.
            // that's the price to pay to avoid allocations.
            DownVector.Clear();
            UpVector.Clear();
            int MAX = A.Length + B.Length + 1;
            // TODO is this necessary, or does the algo set all values anyway?
            for (int i = 0; i < 2 * MAX + 2; ++i)
            {
                DownVector.Add(0);
                UpVector.Add(0);
            }

            LongestCommonSubsequence(A, modifiedA, 0, A.Length,
                B, modifiedB, 0, B.Length,
                DownVector, UpVector);

            CreateDiffs(modifiedA, modifiedB, result);
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
        // Returns a MiddleSnakeData record containing x,y (u,v aren't needed)
        internal static (int x, int y) ShortestMiddleSnake(NativeArray<byte> A, int LowerA, int UpperA, NativeArray<byte> B, int LowerB, int UpperB,
            NativeList<int> DownVector, NativeList<int> UpVector)
            // need ICompareable for <>, need IEquatable<T> to avoid .Equals boxing
            //where T : struct, IComparable, IEquatable<T>
        {
            int MAX = A.Length + B.Length + 1;

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
                    while ((x < UpperA) && (y < UpperB) && (A[x].Equals(B[y])))
                    {
                        x++; y++;
                    }
                    DownVector[DownOffset + k] = x;

                    // overlap ?
                    if (oddDelta && (UpK - D < k) && (k < UpK + D))
                    {
                        if (UpVector[UpOffset + k] <= DownVector[DownOffset + k])
                        {
                            return (DownVector[DownOffset + k],
                                    DownVector[DownOffset + k] - k);
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

                    while ((x > LowerA) && (y > LowerB) && (A[x - 1].Equals(B[y - 1])))
                    {
                      x--; y--; // diagonal
                    }
                    UpVector[UpOffset + k] = x;

                    // overlap ?
                    if (!oddDelta && (DownK - D <= k) && (k <= DownK + D))
                    {
                        if (UpVector[UpOffset + k] <= DownVector[DownOffset + k])
                        {
                            return (DownVector[DownOffset + k],
                                    DownVector[DownOffset + k] - k);
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
        static void LongestCommonSubsequence(
            NativeArray<byte> A, NativeList<byte> modifiedA, int LowerA, int UpperA,
            NativeArray<byte> B, NativeList<byte> modifiedB, int LowerB, int UpperB,
            NativeList<int> DownVector, NativeList<int> UpVector)
            // need ICompareable for <>, need IEquatable<T> to avoid .Equals boxing
            //where T : struct, IComparable, IEquatable<T>
        {
            // Debug.Write(2, "LCS", String.Format("Analyse the box: A[{0}-{1}] to B[{2}-{3}]", LowerA, UpperA, LowerB, UpperB));

            // Fast walkthrough equal lines at the start
            while (LowerA < UpperA && LowerB < UpperB && A[LowerA].Equals(B[LowerB]))
            {
                LowerA++; LowerB++;
            }

            // Fast walkthrough equal lines at the end
            while (LowerA < UpperA && LowerB < UpperB && A[UpperA - 1].Equals(B[UpperB - 1]))
            {
                --UpperA; --UpperB;
            }

            if (LowerA == UpperA)
            {
                // mark as inserted lines.
                while (LowerB < UpperB)
                    modifiedB[LowerB++] = 1; // true
            }
            else if (LowerB == UpperB)
            {
                // mark as deleted lines.
                while (LowerA < UpperA)
                    modifiedA[LowerA++] = 1; // true
            }
            else
            {
                // Find the middle snake and length of an optimal path for A and B
                (int x, int y) = ShortestMiddleSnake(A, LowerA, UpperA, B, LowerB, UpperB, DownVector, UpVector);
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
        // (modified array of byte instead of bool for burst)
        static int WalkModified(int index, int length, NativeList<byte> modified, int otherIndex, int otherLength)
        {
            // end of other reached yet? then jump straight to the end.
            // (check avoids deadlock, see EdgeCase_Increase test)
            if (otherIndex >= otherLength)
                return length;

            // end of other not reached yet? then walk while modified
            while (index < length && modified[index] != 0)
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
        internal static void CreateDiffs(NativeList<byte> modifiedA, NativeList<byte> modifiedB, NativeList<Item> result)
        {
            // modified [] is always original length + 2.
            // calculate original length instead of passing it as parameter too.
            int lengthA = modifiedA.Length - 2;
            int lengthB = modifiedB.Length - 2;

            // indices for both arrays
            int a = 0;
            int b = 0;

            // keep going until BOTH lineA AND lineB reached the max length
            while (a < lengthA || b < lengthB)
            {
                // walk through a and b while both unmodified
                // (modified arrays of byte instead of bool for burst, so check == 0)
                if (a < lengthA && modifiedA[a] == 0 &&
                    b < lengthB && modifiedB[b] == 0 )
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
