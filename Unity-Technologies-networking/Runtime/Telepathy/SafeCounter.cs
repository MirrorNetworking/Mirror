// a very simple locked 'uint' counter
// (we can't do lock(int) so we need an object and since we also need a max
//  check, we might as well put it into a class here)
using System;

namespace Telepathy
{
    public class SafeCounter
    {
        uint counter;

        public uint Next()
        {
            lock (this)
            {
                // it's very unlikely that we reach the uint limit of 4 billion.
                // even with 1 new connection per second, this would take 136 years.
                // -> but if it happens, then we should throw an exception because
                //    the caller probably should stop accepting clients.
                // -> it's hardly worth using 'bool Next(out id)' for that case
                //    because it's just so unlikely.
                if (counter == uint.MaxValue)
                {
                    throw new Exception("SafeCounter limit reached: " + counter);
                }
                return counter++;
            }
        }
    }
}