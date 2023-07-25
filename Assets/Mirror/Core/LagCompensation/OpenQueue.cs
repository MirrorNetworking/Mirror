// Queue<T> copied from C#'s implementation, but:
//   => with direct access to _array[T].
//   => with fixed size without runtime resizing to allow for fast iteration of
//      all elements.
// we need this for Lag Compensation's HistoryBounds to be able to calculate
// the total bounds quickly, without enumerators and extra checks.
//
// having a few bounds in an array[T] is very cache friendly if we have direct
// access.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Mirror
{
    public sealed class OpenQueue<T>  : IReadOnlyCollection<T>
    {
        readonly T[] array;
        int head;       // First valid element in the queue
        int tail;       // Last valid element in the queue
        int size;       // Number of elements
        int version;    // To detect modifications while iterating

        // Creates a queue with room for capacity objects.
        // does not resize at runtime.
        public OpenQueue(int capacity)
        {
            if (capacity < 0) throw new Exception("Queue capacity cannot be negative");

            array = new T[capacity];
            head = 0;
            tail = 0;
            size = 0;
        }

        public int Count => size;

        // Removes all Objects from the queue.
        public void Clear()
        {
            if (head < tail)
            {
                Array.Clear(array, head, size);
            }
            else
            {
                Array.Clear(array, head, array.Length - head);
                Array.Clear(array, 0, tail);
            }

            head = 0;
            tail = 0;
            size = 0;
            version++;
        }

        // Adds item to the tail of the queue.
        public void Enqueue(T item) {
            if (size == array.Length)
                throw new InvalidOperationException("Queue is full.");

            array[tail] = item;
            tail = (tail + 1) % array.Length;
            size++;
            version++;
        }

        // Removes the object at the head of the queue and returns it. If the queue
        // is empty, this method simply returns null.
        public T Dequeue() {
            if (size == 0) throw new InvalidOperationException("Can't Dequeue from empty queue.");

            T removed = array[head];
            array[head] = default(T);
            head = (head + 1) % array.Length;
            size--;
            version++;
            return removed;
        }

        // Returns the object at the head of the queue. The object remains in the
        // queue. If the queue is empty, this method throws an
        // InvalidOperationException.
        public T Peek() {
            if (size == 0) throw new InvalidOperationException("Can't Peek into empty queue.");

            return array[head];
        }

        internal T GetElement(int i)
        {
            return array[(head + i) % array.Length];
        }

        // GetEnumerator returns an IEnumerator over this Queue.  This
        // Enumerator will support removing.
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new Enumerator(this);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        // Implements an enumerator for a Queue.  The enumerator uses the
        // internal version number of the list to ensure that no modifications are
        // made to the list while an enumeration is in progress.
        [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Justification = "not an expected scenario")]
        public struct Enumerator : IEnumerator<T>
        {
            readonly OpenQueue<T> _q;
            int _index;   // -1 = not started, -2 = ended/disposed
            readonly int _version;
            T _currentElement;

            internal Enumerator(OpenQueue<T> q) {
                _q = q;
                _version = _q.version;
                _index = -1;
                _currentElement = default(T);
            }

            public void Dispose()
            {
                _index = -2;
                _currentElement = default(T);
            }

            public bool MoveNext() {
                if (_version != _q.version)
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

                if (_index == -2)
                    return false;

                _index++;

                if (_index == _q.size) {
                    _index = -2;
                    _currentElement = default(T);
                    return false;
                }

                _currentElement = _q.GetElement(_index);
                return true;
            }

            public T Current {
                get {
                    if (_index < 0)
                    {
                        if (_index == -1)
                            throw new InvalidOperationException("Enumeration has not started. Call MoveNext.");
                        else
                            throw new InvalidOperationException("Enumeration already finished.");
                    }
                    return _currentElement;
                }
            }

            Object System.Collections.IEnumerator.Current {
                get {
                    if (_index < 0)
                    {
                        if (_index == -1)
                            throw new InvalidOperationException("Enumeration has not started. Call MoveNext.");
                        else
                            throw new InvalidOperationException("Enumeration already finished.");
                    }
                    return _currentElement;
                }
            }

            void System.Collections.IEnumerator.Reset() {
                if (_version != _q.version) throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                _index = -1;
                _currentElement = default(T);
            }
        }
    }
}
