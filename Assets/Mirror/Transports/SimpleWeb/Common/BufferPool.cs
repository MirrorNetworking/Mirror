using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Mirror.SimpleWeb
{
    public interface IBufferOwner
    {
        void Return(ArrayBuffer buffer);
    }

    public sealed class ArrayBuffer : IDisposable
    {
        readonly IBufferOwner owner;

        public readonly byte[] array;

        /// <summary>
        /// number of bytes written to buffer
        /// </summary>
        public int count { get; internal set; }

        /// <summary>
        /// How many times release needs to be called before buffer is returned to pool
        /// <para>This allows the buffer to be used in multiple places at the same time</para>
        /// </summary>
        public void SetReleasesRequired(int required)
        {
            releasesRequired = required;
        }

        /// <summary>
        /// How many times release needs to be called before buffer is returned to pool
        /// <para>This allows the buffer to be used in multiple places at the same time</para>
        /// </summary>
        /// <remarks>
        /// This value is normally 0, but can be changed to require release to be called multiple times
        /// </remarks>
        int releasesRequired;

        public ArrayBuffer(IBufferOwner owner, int size)
        {
            this.owner = owner;
            array = new byte[size];
        }

        public void Release()
        {
            int newValue = Interlocked.Decrement(ref releasesRequired);
            if (newValue <= 0)
            {
                count = 0;
                owner?.Return(this);
            }
        }
        public void Dispose()
        {
            Release();
        }


        public void CopyTo(byte[] target, int offset)
        {
            if (count > (target.Length + offset)) throw new ArgumentException($"{nameof(count)} was greater than {nameof(target)}.length", nameof(target));

            Buffer.BlockCopy(array, 0, target, offset, count);
        }

        public void CopyFrom(ArraySegment<byte> segment)
        {
            CopyFrom(segment.Array, segment.Offset, segment.Count);
        }

        public void CopyFrom(byte[] source, int offset, int length)
        {
            if (length > array.Length) throw new ArgumentException($"{nameof(length)} was greater than {nameof(array)}.length", nameof(length));

            count = length;
            Buffer.BlockCopy(source, offset, array, 0, length);
        }

        public void CopyFrom(IntPtr bufferPtr, int length)
        {
            if (length > array.Length) throw new ArgumentException($"{nameof(length)} was greater than {nameof(array)}.length", nameof(length));

            count = length;
            Marshal.Copy(bufferPtr, array, 0, length);
        }

        public ArraySegment<byte> ToSegment()
        {
            return new ArraySegment<byte>(array, 0, count);
        }

        [Conditional("UNITY_ASSERTIONS")]
        internal void Validate(int arraySize)
        {
            if (array.Length != arraySize)
            {
                Log.Error("[SimpleWebTransport] Buffer that was returned had an array of the wrong size");
            }
        }
    }

    internal class BufferBucket : IBufferOwner
    {
        public readonly int arraySize;
        readonly ConcurrentQueue<ArrayBuffer> buffers;

        /// <summary>
        /// keeps track of how many arrays are taken vs returned
        /// </summary>
        internal int _current = 0;

        public BufferBucket(int arraySize)
        {
            this.arraySize = arraySize;
            buffers = new ConcurrentQueue<ArrayBuffer>();
        }

        public ArrayBuffer Take()
        {
            IncrementCreated();
            if (buffers.TryDequeue(out ArrayBuffer buffer))
            {
                return buffer;
            }
            else
            {
                Log.Verbose($"[SimpleWebTransport] BufferBucket({arraySize}) create new");
                return new ArrayBuffer(this, arraySize);
            }
        }

        public void Return(ArrayBuffer buffer)
        {
            DecrementCreated();
            buffer.Validate(arraySize);
            buffers.Enqueue(buffer);
        }

        [Conditional("DEBUG")]
        void IncrementCreated()
        {
            int next = Interlocked.Increment(ref _current);
            Log.Verbose($"[SimpleWebTransport] BufferBucket({arraySize}) count:{next}");
        }
        [Conditional("DEBUG")]
        void DecrementCreated()
        {
            int next = Interlocked.Decrement(ref _current);
            Log.Verbose($"[SimpleWebTransport] BufferBucket({arraySize}) count:{next}");
        }
    }

    /// <summary>
    /// Collection of different sized buffers
    /// </summary>
    /// <remarks>
    /// <para>
    /// Problem: <br/>
    ///     * Need to cached byte[] so that new ones aren't created each time <br/>
    ///     * Arrays sent are multiple different sizes <br/>
    ///     * Some message might be big so need buffers to cover that size <br/>
    ///     * Most messages will be small compared to max message size <br/>
    /// </para>
    /// <br/>
    /// <para>
    /// Solution: <br/>
    ///     * Create multiple groups of buffers covering the range of allowed sizes <br/>
    ///     * Split range exponentially (using math.log) so that there are more groups for small buffers <br/>
    /// </para>
    /// </remarks>
    public class BufferPool
    {
        internal readonly BufferBucket[] buckets;
        readonly int bucketCount;
        readonly int smallest;
        readonly int largest;

        public BufferPool(int bucketCount, int smallest, int largest)
        {
            if (bucketCount < 2) throw new ArgumentException("Count must be at least 2");
            if (smallest < 1) throw new ArgumentException("Smallest must be at least 1");
            if (largest < smallest) throw new ArgumentException("Largest must be greater than smallest");


            this.bucketCount = bucketCount;
            this.smallest = smallest;
            this.largest = largest;


            // split range over log scale (more buckets for smaller sizes)

            double minLog = Math.Log(this.smallest);
            double maxLog = Math.Log(this.largest);

            double range = maxLog - minLog;
            double each = range / (bucketCount - 1);

            buckets = new BufferBucket[bucketCount];

            for (int i = 0; i < bucketCount; i++)
            {
                double size = smallest * Math.Pow(Math.E, each * i);
                buckets[i] = new BufferBucket((int)Math.Ceiling(size));
            }


            Validate();

            // Example
            // 5         count  
            // 20        smallest
            // 16400     largest

            // 3.0       log 20
            // 9.7       log 16400 

            // 6.7       range 9.7 - 3
            // 1.675     each  6.7 / (5-1)

            // 20        e^ (3 + 1.675 * 0)
            // 107       e^ (3 + 1.675 * 1)
            // 572       e^ (3 + 1.675 * 2)
            // 3056      e^ (3 + 1.675 * 3)
            // 16,317    e^ (3 + 1.675 * 4)

            // precision wont be lose when using doubles
        }

        [Conditional("UNITY_ASSERTIONS")]
        void Validate()
        {
            if (buckets[0].arraySize != smallest)
            {
                Log.Error($"[SimpleWebTransport] BufferPool Failed to create bucket for smallest. bucket:{buckets[0].arraySize} smallest{smallest}");
            }

            int largestBucket = buckets[bucketCount - 1].arraySize;
            // rounded using Ceiling, so allowed to be 1 more that largest
            if (largestBucket != largest && largestBucket != largest + 1)
            {
                Log.Error($"[SimpleWebTransport] BufferPool Failed to create bucket for largest. bucket:{largestBucket} smallest{largest}");
            }
        }

        public ArrayBuffer Take(int size)
        {
            if (size > largest) { throw new ArgumentException($"Size ({size}) is greatest that largest ({largest})"); }

            for (int i = 0; i < bucketCount; i++)
            {
                if (size <= buckets[i].arraySize)
                {
                    return buckets[i].Take();
                }
            }

            throw new ArgumentException($"Size ({size}) is greatest that largest ({largest})");
        }
    }
}
