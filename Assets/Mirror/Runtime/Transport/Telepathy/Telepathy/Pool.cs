// pool to avoid allocations. originally from libuv2k.
using System;
using System.Collections.Generic;

namespace Telepathy
{
    public class Pool<T>
    {
        // objects
        readonly Stack<T> objects = new Stack<T>();

        // some types might need additional parameters in their constructor, so
        // we use a Func<T> generator
        readonly Func<T> objectGenerator;

        // constructor
        public Pool(Func<T> objectGenerator)
        {
            this.objectGenerator = objectGenerator;
        }

        // take an element from the pool, or create a new one if empty
        public T Take() => objects.Count > 0 ? objects.Pop() : objectGenerator();

        // return an element to the pool
        public void Return(T item) => objects.Push(item);

        // clear the pool with the disposer function applied to each object
        public void Clear() => objects.Clear();

        // count to see how many objects are in the pool. useful for tests.
        public int Count() => objects.Count;
    }
}