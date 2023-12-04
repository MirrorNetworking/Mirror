// HistoryBounds keeps a bounding box of all the object's bounds in the past N seconds.
// useful to decide which objects to rollback, instead of rolling back all of them.
// https://www.youtube.com/watch?v=zrIY0eIyqmI (37:00)
// standalone C# implementation to be engine (and language) agnostic.

using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    // FakeByte: gather bounds in smaller buckets.
    // for example, bucket(t0,t1,t2), bucket(t3,t4,t5), ...
    // instead of removing old bounds t0, t1, ...
    // we remove a whole bucket every 3 times: bucket(t0,t1,t2)
    // and when building total bounds, we encapsulate a few larger buckets
    // instead of many smaller bounds.
    //
    // => a bucket is encapsulate(bounds0, bounds1, bounds2) so we don't
    //    need a custom struct, simply reuse bounds but remember that each
    //    entry includes N timestamps.
    //
    // => note that simply reducing capture interval is _not_ the same.
    //    we want to capture in detail in case players run in zig-zag.
    //    but still grow larger buckets internally.
    public class HistoryBounds
    {
        // mischa: use MinMaxBounds to avoid Unity Bounds.Encapsulate conversions.
        readonly int boundsPerBucket;
        readonly Queue<MinMaxBounds> fullBuckets;

        // full bucket limit. older ones will be removed.
        readonly int bucketLimit;

        // bucket in progress, contains 0..boundsPerBucket bounds encapsulated.
        MinMaxBounds? currentBucket;
        int currentBucketSize;

        // amount of total bounds, including bounds in full buckets + current
        public int boundsCount { get; private set; }

        // total bounds encapsulating all of the bounds history.
        // totalMinMax is used for internal calculations.
        // public total is used for Unity representation.
        MinMaxBounds totalMinMax;
        public Bounds total
        {
            get
            {
                Bounds bounds = new Bounds();
                bounds.SetMinMax(totalMinMax.min, totalMinMax.max);
                return bounds;
            }
        }

        public HistoryBounds(int boundsLimit, int boundsPerBucket)
        {
            // bucketLimit via '/' cuts off remainder.
            // that's what we want, since we always have a 'currentBucket'.
            this.boundsPerBucket = boundsPerBucket;
            this.bucketLimit = (boundsLimit / boundsPerBucket);

            // initialize queue with maximum capacity to avoid runtime resizing
            // capacity +1 because it makes the code easier if we insert first, and then remove.
            fullBuckets = new Queue<MinMaxBounds>(bucketLimit + 1);
        }

        // insert new bounds into history. calculates new total bounds.
        // Queue.Dequeue() always has the oldest bounds.
        public void Insert(Bounds bounds)
        {
            // convert to MinMax representation for faster .Encapsulate()
            MinMaxBounds minmax = new MinMaxBounds
            {
                min = bounds.min,
                max = bounds.max
            };

            // initialize 'total' if not initialized yet.
            // we don't want to call (0,0).Encapsulate(bounds).
            if (boundsCount == 0)
            {
                totalMinMax = minmax;
            }

            // add to current bucket:
            // either initialize new one, or encapsulate into existing one
            if (currentBucket == null)
            {
                currentBucket = minmax;
            }
            else
            {
                currentBucket.Value.Encapsulate(minmax);
            }

            // current bucket has one more bounds.
            // total bounds increased as well.
            currentBucketSize += 1;
            boundsCount += 1;

            // always encapsulate into total immediately.
            // this is free.
            totalMinMax.Encapsulate(minmax);

            // current bucket full?
            if (currentBucketSize == boundsPerBucket)
            {
                // move it to full buckets
                fullBuckets.Enqueue(currentBucket.Value);
                currentBucket = null;
                currentBucketSize = 0;

                // full bucket capacity reached?
                if (fullBuckets.Count > bucketLimit)
                {
                    // remove oldest bucket
                    fullBuckets.Dequeue();
                    boundsCount -= boundsPerBucket;

                    // recompute total bounds
                    // instead of iterating N buckets, we iterate N / boundsPerBucket buckets.
                    // TODO technically we could reuse 'currentBucket' before clearing instead of encapsulating again
                    totalMinMax = minmax;
                    foreach (MinMaxBounds bucket in fullBuckets)
                        totalMinMax.Encapsulate(bucket);
                }
            }
        }

        public void Reset()
        {
            fullBuckets.Clear();
            currentBucket = null;
            currentBucketSize = 0;
            boundsCount = 0;
            totalMinMax = new MinMaxBounds();
        }
    }
}
