using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.KCP
{

    /// <summary>
    /// A simple queue where entries are sorted by their priority.
    /// </summary>
    /// <remarks>The priority queue is implemented as a heap,
    /// for more information on this well known data structure,
    /// refer to <see href="https://en.wikipedia.org/wiki/Binary_heap"/></remarks>
    /// <typeparam name="P">the priority of the item</typeparam>
    /// <typeparam name="V">The item</typeparam>
    public class PriorityQueue<P, V> where P : IComparable<P>
    {
        private readonly List<(P priority, V value)> items = new System.Collections.Generic.List<(P,V)>();

        public PriorityQueue()
        {

        }

        public int Count => items.Count;


        // the location of the parent in the heap
        private int Parent(int location) => (location - 1) >> 1;

        // the location of the children in the heap
        private int Children(int location) => (location << 1) + 1;

        public void Enqueue(P priority, V value)
        {
            items.Add((priority, value));
            BubbleUp(priority);
        }

        private void BubbleUp(P priority)
        {
            // the last item in the list may need to move up in order
            // to maintain the heap integrity
            int loc = items.Count - 1;
            int parent = Parent(loc);
            while (loc > 0 && items[parent].priority.CompareTo(priority) > 0)
            {
                swap(loc, parent);
                loc = parent;
                parent = Parent(loc);
            }
        }

        private void swap(int loc1, int loc2)
        {
            (items[loc1], items[loc2]) = (items[loc2], items[loc1]);
        }

        public (P priority, V value) Dequeue()
        {
            (P priority, V value) result = items[0];

            int last = items.Count - 1;
            swap(0, last);
            items.RemoveAt(last);

            if (items.Count > 0)
                BubbleDown(0);

            return result;
        }

        private void BubbleDown(int loc)
        {
            // the first item in the heap may need to bubble down in order
            // to maintain heap integrity
            int left = Children(loc);
            int right = left + 1;

            int smallest = loc;

            if (left < items.Count && items[left].priority.CompareTo(items[smallest].priority) < 0)
                smallest = left;

            if (right < items.Count && items[right].priority.CompareTo(items[smallest].priority) < 0)
                smallest = right;

            if (smallest != loc) {
                swap(smallest, loc);
                BubbleDown(smallest);
            }
        }

        public (P priority, V value) Peek() => items[0];
    }
}