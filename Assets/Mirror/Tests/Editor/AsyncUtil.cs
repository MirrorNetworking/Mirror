using System;
using System.Collections;
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
    }
}
