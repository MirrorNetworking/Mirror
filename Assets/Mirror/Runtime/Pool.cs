// Pool to avoid allocations (from libuv2k)
using System;
using System.Collections.Generic;

namespace Mirror
{
    public class Pool<T>
    {
        // libuv uses an event loop. simple stack is fine, no need for thread
        // safe collections.
        readonly Stack<T> objects = new Stack<T>();

        // some types might need additional parameters in their constructor, so
        // we use a Func<T> generator
        readonly Func<T> objectGenerator;

        public Pool(Func<T> objectGenerator)
        {
            this.objectGenerator = objectGenerator;
        }

        // take an element from the pool, or create a new one if empty
        public T Take() => objects.Count > 0 ? objects.Pop() : objectGenerator();

        // return an element to the pool
        public void Return(T item) => objects.Push(item);
    }
}
