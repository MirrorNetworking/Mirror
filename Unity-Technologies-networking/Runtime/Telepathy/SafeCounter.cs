// a very simple locked 'uint' counter
// (we can't do lock(int) so we need an object and since we also need a max
//  check, we might as well put it into a class here)
using System;

namespace Telepathy
{
    public class SafeCounter
    {
        int counter;

        public int Next()
        {
            lock (this)
            {
                // it's very unlikely that we reach the uint limit of 2 billion.
                // even with 1 connection per second, this would take 68 years.
                // -> but if it happens, then we should throw an exception
                //    because the caller probably should stop accepting clients.
                // -> it's hardly worth using 'bool Next(out id)' for that case
                //    because it's just so unlikely.
                if (counter == int.MaxValue)
                {
                    throw new Exception("SafeCounter limit reached: " + counter);
                }
                return counter++;
            }
        }
    }
}