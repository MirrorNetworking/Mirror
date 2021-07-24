using System;
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
    List<bool> modifiedA = new List<bool>();
    List<bool> modifiedB = new List<bool>();
    List<int> DownVector = new List<int>();
    List<int> UpVector = new List<int>();
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
        List<bool> modifiedA, List<bool> modifiedB,
        List<int> DownVector, List<int> UpVector,
        List<Item> diffs)
    {
        ArraySegment<byte> fromSegment = from.ToArraySegment();
        ArraySegment<byte> toSegment = to.ToArraySegment();

        // myers diff nonalloc
        MyersDiffX.MyersDiffX.DiffNonAlloc(
            fromSegment, toSegment,
            modifiedA, modifiedB,
            DownVector, UpVector,
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
            ComputeDeltaNonAlloc(A, B, result, modifiedA, modifiedB, DownVector, UpVector, diffs);
        }
    }
}
