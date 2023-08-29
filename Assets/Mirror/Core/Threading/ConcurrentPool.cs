// Pool to avoid allocations (from libuv2k)
// API consistent with Microsoft's ObjectPool<T>.
// concurrent for thread safe access.
//
// currently not in use. keep it in case we need it again.
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Mirror
{
    public class ConcurrentPool<T>
    {
        // Mirror is single threaded, no need for concurrent collections
        // concurrent bag is for items who's order doesn't matter.
        // just about right for our use case here.
        readonly ConcurrentBag<T> objects = new ConcurrentBag<T>();

        // some types might need additional parameters in their constructor, so
        // we use a Func<T> generator
        readonly Func<T> objectGenerator;

        public ConcurrentPool(Func<T> objectGenerator, int initialCapacity)
        {
            this.objectGenerator = objectGenerator;

            // allocate an initial pool so we have fewer (if any)
            // allocations in the first few frames (or seconds).
            for (int i = 0; i < initialCapacity; ++i)
                objects.Add(objectGenerator());
        }

        // take an element from the pool, or create a new one if empty
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get() => objects.TryTake(out T obj) ? obj : objectGenerator();

        // return an element to the pool
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(T item) => objects.Add(item);

        // count to see how many objects are in the pool. useful for tests.
        public int Count => objects.Count;
    }
}
