// RingBuffer<T>, previously also named OpenQueue<T>.
// C#'s Queue is already a Ringbuffer, but sometimes we need it to be more open
// so that we can do for-int iterations on it, access the tail, etc.
//
// Based on C#'s Queue<T> implementation, but:
//   => with direct access to _array[T].
//   => with fixed size without runtime resizing to allow for fast iteration of
//      all elements.
//   => with (Try)Peek() and (Try)Tail() methods to access first and last
//   => with [i] access for convenience
//
using System;
using System.Collections.Generic;

namespace Mirror
{
    public sealed class RingBuffer<T>  : IReadOnlyCollection<T>
    {
        readonly T[] _array;
        int _head;       // First valid element in the queue
        int _tail;       // Last valid element in the queue
        int _size;       // Number of elements.
        int _version;

        // Creates a queue with room for capacity objects.
        // does not resize at runtime.
        public RingBuffer(int capacity)
        {
            if (capacity < 0) throw new Exception("Queue capacity cannot be negative");

            _array = new T[capacity];
            _head = 0;
            _tail = 0;
            _size = 0;
        }

        public int Count => _size;

        // Removes all Objects from the queue.
        public void Clear()
        {
            if (_head < _tail)
            {
                Array.Clear(_array, _head, _size);
            }
            else
            {
                Array.Clear(_array, _head, _array.Length - _head);
                Array.Clear(_array, 0, _tail);
            }

            _head = 0;
            _tail = 0;
            _size = 0;
            _version++;
        }

        // Adds item to the tail of the queue.
        public void Enqueue(T item) {
            if (_size == _array.Length)
                throw new InvalidOperationException("Queue is full.");

            _array[_tail] = item;
            _tail = (_tail + 1) % _array.Length;
            _size++;
            _version++;
        }

        // Removes the object at the head of the queue and returns it. If the queue
        // is empty, this method simply returns null.
        public T Dequeue() {
            if (_size == 0) throw new InvalidOperationException("Can't Dequeue from empty queue.");

            T removed = _array[_head];
            _array[_head] = default(T);
            _head = (_head + 1) % _array.Length;
            _size--;
            _version++;
            return removed;
        }

        // Returns the object at the head of the queue. The object remains in the
        // queue. If the queue is empty, this method throws an
        // InvalidOperationException.
        public T Peek() {
            if (_size == 0) throw new InvalidOperationException("Can't Peek into empty queue.");

            return _array[_head];
        }

        public bool TryPeek(out T result)
        {
            if (_size == 0)
            {
                result = default;
                return false;
            }

            result = _array[_head];
            return true;
        }

        public T Tail() {
            if (_size == 0) throw new InvalidOperationException("Can't Tail into empty queue.");

            return _array[(_tail + _array.Length - 1) % _array.Length];
        }

        public bool TryTail(out T result)
        {
            if (_size == 0)
            {
                result = default;
                return false;
            }

            result = _array[(_tail + _array.Length - 1) % _array.Length];
            return true;
        }

        internal T GetElement(int i)
        {
            if (i < 0 || i >= _size) throw new IndexOutOfRangeException($"Index {i} out of range from 0..{Count}.");
            return _array[(_head + i) % _array.Length];
        }

        internal void SetElement(int i, T value)
        {
            if (i < 0 || i >= _size) throw new IndexOutOfRangeException($"Index {i} out of range from 0..{Count}.");
            _array[(_head + i) % _array.Length] = value;
        }

        // [i] access for convenience. [i] from 0..Count gives elements in order.
        // [i] is not(!) the internal index.
        // note that indices may break after insertions and removals.
        public T this[int i]
        {
            get => GetElement(i);
            set => SetElement(i, value);
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
        public struct Enumerator : IEnumerator<T>
        {
            readonly RingBuffer<T> _q;
            int _index;   // -1 = not started, -2 = ended/disposed
            readonly int _version;
            T _currentElement;

            internal Enumerator(RingBuffer<T> q) {
                _q = q;
                _version = _q._version;
                _index = -1;
                _currentElement = default(T);
            }

            public void Dispose()
            {
                _index = -2;
                _currentElement = default(T);
            }

            public bool MoveNext() {
                if (_version != _q._version)
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

                if (_index == -2)
                    return false;

                _index++;

                if (_index == _q._size) {
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
                if (_version != _q._version) throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                _index = -1;
                _currentElement = default(T);
            }
        }
    }
}
