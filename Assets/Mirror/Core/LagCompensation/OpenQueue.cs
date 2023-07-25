// Queue<T> copied from C#'s implementation, but with direct access to _array[T].
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
        T[] _array;
        int _head;       // First valid element in the queue
        int _tail;       // Last valid element in the queue
        int _size;       // Number of elements.
        int _version;

        const int _MinimumGrow = 4;
        const int _GrowFactor = 200;  // double each time
        static T[]  _emptyArray = new T[0];

        // Creates a queue with room for capacity objects. The default initial
        // capacity and grow factor are used.
        public OpenQueue() {
            _array = _emptyArray;
        }

        // Creates a queue with room for capacity objects. The default grow factor
        // is used.
        public OpenQueue(int capacity) {
            if (capacity < 0) throw new Exception("Queue capacity cannot be negative");

            _array = new T[capacity];
            _head = 0;
            _tail = 0;
            _size = 0;
        }

        public int Count => _size;

        // Removes all Objects from the queue.
        public void Clear() {
            if (_head < _tail)
                Array.Clear(_array, _head, _size);
            else {
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
            if (_size == _array.Length) {
                int newcapacity = (int)((long)_array.Length * (long)_GrowFactor / 100);
                if (newcapacity < _array.Length + _MinimumGrow) {
                    newcapacity = _array.Length + _MinimumGrow;
                }
                SetCapacity(newcapacity);
            }

            _array[_tail] = item;
            _tail = (_tail + 1) % _array.Length;
            _size++;
            _version++;
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

        // Returns true if the queue contains at least one object equal to item.
        // Equality is determined using item.Equals().
        //
        // Exceptions: ArgumentNullException if item == null.
       public bool Contains(T item) {
            int index = _head;
            int count = _size;

            EqualityComparer<T> c = EqualityComparer<T>.Default;
            while (count-- > 0) {
                if (((Object) item) == null) {
                    if (((Object) _array[index]) == null)
                        return true;
                }
                else if (_array[index] != null && c.Equals(_array[index], item)) {
                    return true;
                }
                index = (index + 1) % _array.Length;
            }

            return false;
        }

        internal T GetElement(int i)
        {
            return _array[(_head + i) % _array.Length];
        }

        // Iterates over the objects in the queue, returning an array of the
        // objects in the Queue, or an empty array if the queue is empty.
        // The order of elements in the array is first in to last in, the same
        // order produced by successive calls to Dequeue.
        public T[] ToArray()
        {
            T[] arr = new T[_size];
            if (_size==0)
                return arr;

            if (_head < _tail) {
                Array.Copy(_array, _head, arr, 0, _size);
            } else {
                Array.Copy(_array, _head, arr, 0, _array.Length - _head);
                Array.Copy(_array, 0, arr, _array.Length - _head, _tail);
            }

            return arr;
        }


        // PRIVATE Grows or shrinks the buffer to hold capacity objects. Capacity
        // must be >= _size.
        void SetCapacity(int capacity) {
            T[] newarray = new T[capacity];
            if (_size > 0) {
                if (_head < _tail) {
                    Array.Copy(_array, _head, newarray, 0, _size);
                } else {
                    Array.Copy(_array, _head, newarray, 0, _array.Length - _head);
                    Array.Copy(_array, 0, newarray, _array.Length - _head, _tail);
                }
            }

            _array = newarray;
            _head = 0;
            _tail = (_size == capacity) ? 0 : _size;
            _version++;
        }

        public void TrimExcess() {
            int threshold = (int)(((double)_array.Length) * 0.9);
            if( _size < threshold ) {
                SetCapacity(_size);
            }
        }

        // Implements an enumerator for a Queue.  The enumerator uses the
        // internal version number of the list to ensure that no modifications are
        // made to the list while an enumeration is in progress.
        [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Justification = "not an expected scenario")]
        public struct Enumerator : IEnumerator<T>, System.Collections.IEnumerator
        {
            OpenQueue<T> _q;
            int _index;   // -1 = not started, -2 = ended/disposed
            int _version;
            T _currentElement;

            internal Enumerator(OpenQueue<T> q) {
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
