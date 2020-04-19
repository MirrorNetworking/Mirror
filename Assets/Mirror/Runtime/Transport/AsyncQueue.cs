using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Mirror
{
    public class AsyncQueue<T>
    {
        private readonly Queue<T> queue = new Queue<T>();
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(0);


        internal void Equeue(T v)
        {
            queue.Enqueue(v);
            semaphore.Release();
        }

        internal async Task<T> DequeueAsync()
        {
            await semaphore.WaitAsync();
            return queue.Dequeue();
        }
    }
}