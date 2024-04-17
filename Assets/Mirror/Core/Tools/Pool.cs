// Pool to avoid allocations (from libuv2k)
// API consistent with Microsoft's ObjectPool<T>.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Mirror
{
    public class Pool<T>
    {
        // Mirror is single threaded, no need for concurrent collections.
        // stack increases the chance that a reused writer remains in cache.
        readonly Stack<T> objects = new Stack<T>();

        // some types might need additional parameters in their constructor, so
        // we use a Func<T> generator
        readonly Func<T> objectGenerator;

        public Pool(Func<T> objectGenerator, int initialCapacity)
        {
            this.objectGenerator = objectGenerator;

            // allocate an initial pool so we have fewer (if any)
            // allocations in the first few frames (or seconds).
            for (int i = 0; i < initialCapacity; ++i)
                objects.Push(objectGenerator());
        }

        // take an element from the pool, or create a new one if empty
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get() => objects.Count > 0 ? objects.Pop() : objectGenerator();

        // return an element to the pool
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(T item)
        {
            // make sure we can't accidentally insert null values into the pool.
            // debugging this would be hard since it would only show on get().
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            objects.Push(item);
        }

        // count to see how many objects are in the pool. useful for tests.
        public int Count => objects.Count;
    }
}
