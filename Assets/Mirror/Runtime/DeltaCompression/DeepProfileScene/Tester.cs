using System.Collections.Generic;
using Mirror;
using UnityEngine;
using MyersDiffX;

public class Tester : MonoBehaviour
{
    NetworkWriter A = new NetworkWriter();
    NetworkWriter B = new NetworkWriter();
    NetworkWriter result = new NetworkWriter();
    public int bytes = 1024;
    public int iterationsPerFrame = 10;

    // prepare caches
    bool[] modifiedA = new bool[0];
    bool[] modifiedB = new bool[0];
    int[] DownVector = new int[0];
    int[] UpVector = new int[0];
    List<Item> diffs = new List<Item>();

    // Start is called before the first frame update
    void Start()
    {
        // create a big writer
        for (int i = 0; i < bytes; ++i)
        {
            A.WriteByte((byte)i);
            // every third value is different
            B.WriteByte((byte)(i % 3 == 0 ? (i+1) : i));
        }
    }

    // NonAlloc version for benchmark
    public void ComputeDeltaNonAlloc(NetworkWriter from, NetworkWriter to, NetworkWriter result,
        ref bool[] modifiedA, ref bool[] modifiedB,
        ref int[] DownVector, ref int[] UpVector,
        List<Item> diffs)
    {
        ArraySegmentX<byte> fromSegment = from.ToArraySegment();
        ArraySegmentX<byte> toSegment = to.ToArraySegment();

        // myers diff nonalloc
        MyersDiffX.MyersDiffX.DiffNonAlloc(
            fromSegment, toSegment,
            ref modifiedA, ref modifiedB,
            ref DownVector, ref UpVector,
            diffs
        );
        //foreach (Diff.Item item in diffs)
        //    UnityEngine.Debug.Log($"item: startA={item.StartA} startB={item.StartB} deletedA={item.deletedA} insertedB={item.insertedB}");

        // make patch
        // TODO linked list etc.
        MyersDiffXPatching.MakePatch(fromSegment, toSegment, diffs, result);
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < iterationsPerFrame; ++i)
        {
            result.Position = 0;
            ComputeDeltaNonAlloc(A, B, result, ref modifiedA, ref modifiedB, ref DownVector, ref UpVector, diffs);
        }
    }
}
