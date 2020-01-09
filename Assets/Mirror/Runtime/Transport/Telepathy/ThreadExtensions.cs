using System.Threading;

namespace Telepathy
{
    public static class ThreadExtensions
    {
        // helper function to abort a thread and not return until it's fully done
        public static void AbortAndJoin(this Thread thread)
        {
            // kill thread at all costs
            // -> calling .Join would sometimes wait forever
            // -> calling .Interrupt only interrupts certain states.
            // => Abort() is the better solution.
            thread.Abort();

            // wait until thread is TRULY finished. this is the only way
            // to guarantee that everything was properly cleaned up before
            // returning.
            // => this means that this function may sometimes block for a while
            //    but there is no other way to guarantee that everything is
            //    cleaned up properly by the time Stop() returns.
            //    we have to live with the wait time.
            thread.Join();
        }
    }
}