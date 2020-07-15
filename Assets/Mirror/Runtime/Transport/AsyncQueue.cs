using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mirror
{
    public class AsyncQueue<T>
    {
        private readonly Queue<T> queue = new Queue<T>();
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(0);

        public void Enqueue(T v)
        {
            queue.Enqueue(v);
            semaphore.Release();
        }

        public async Task<T> DequeueAsync()
        {
            await semaphore.WaitAsync();
            return queue.Dequeue();
        }

        public int Count => queue.Count;

    }
}
