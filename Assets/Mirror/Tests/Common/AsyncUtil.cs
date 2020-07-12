using System;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Mirror.Tests
{
    public static class AsyncUtil
    {

        // Unity's nunit does not support async tests
        // so we do this boilerplate to run our async methods
        public static IEnumerator RunAsync(Func<Task> block)
        {
            Task task = block();

            while (!task.IsCompleted) { yield return null; }
            if (task.IsFaulted) { throw task.Exception; }
        }

        public static async Task WaitFor(Func<bool> predicate, float timeout = 2f)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            while (!predicate())
            {
                await Task.Delay(1);

                if (stopWatch.ElapsedMilliseconds > timeout * 1000)
                    throw new TimeoutException("Task did not complete in time");
            }
        }
    }
}
