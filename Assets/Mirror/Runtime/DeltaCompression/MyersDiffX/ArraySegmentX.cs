// high performance ArraySegment<T> for C#.
// https://github.com/vis2k/ArraySegmentX
/*
MIT License

Copyright (c) 2021, Michael W. (vis2k)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/
using System;

// put it into MyersDiffX namespace so it doesn't collide with other libs that
// might use ArraySegmentX too.
namespace MyersDiffX
{
    public readonly struct ArraySegmentX<T>
    {
        // readonly instead of property to avoid two IL calls each time.
        public readonly T[] Array;
        public readonly int Offset;
        public readonly int Count;

        public ArraySegmentX(T[] array)
        {
            if (array == null) throw new ArgumentNullException("array");

            Array = array;
            Offset = 0;
            Count = array.Length;
        }

        public ArraySegmentX(T[] array, int offset, int count)
        {
            if (array == null) throw new ArgumentNullException("array");
            if (offset < 0) throw new ArgumentOutOfRangeException("offset");
            if (count < 0) throw new ArgumentOutOfRangeException("count");
            if (array.Length - offset < count) throw new ArgumentException("Length - offset needs to be >= count");

            Array = array;
            Offset = offset;
            Count = count;
        }

        // [] indexer for convenience.
        // this requires an IL call though.
        // use segment.Array[segment.Offset + i] directly when performance matters.
        public T this[int index]
        {
            // don't allow accesing outside of segment.
            get
            {
                // make sure that [] stays within count even if array is bigger
                if (index >= Count) throw new ArgumentOutOfRangeException("index");
                return Array[Offset + index];
            }
            set
            {
                // make sure that [] stays within count even if array is bigger
                if (index >= Count) throw new ArgumentOutOfRangeException("index");
                Array[Offset + index] = value;
            }
        }

        public override int GetHashCode() =>
            // netcore version doesn't work in Unity because HashCode is internal
            //Array is null ? 0 : HashCode.Combine(Offset, Count, Array.GetHashCode());
            Array == null ? 0 : Array.GetHashCode() ^ Offset ^ Count;

        // Equals from netcore ArraySegment<T>
        public override bool Equals(object obj) =>
            obj is ArraySegmentX<T> x && Equals(x);

        public bool Equals(ArraySegmentX<T> obj) =>
            obj.Array == Array && obj.Offset == Offset && obj.Count == Count;

        // operators
        public static bool operator ==(ArraySegmentX<T> a, ArraySegmentX<T> b) => a.Equals(b);
        public static bool operator !=(ArraySegmentX<T> a, ArraySegmentX<T> b) => !(a == b);

        // implicit conversions to / from original ArraySegment for ease of use
        public static implicit operator ArraySegment<T>(ArraySegmentX<T> segment) =>
            new ArraySegment<T>(segment.Array, segment.Offset, segment.Count);

        public static implicit operator ArraySegmentX<T>(ArraySegment<T> segment) =>
            new ArraySegmentX<T>(segment.Array, segment.Offset, segment.Count);
    }
}