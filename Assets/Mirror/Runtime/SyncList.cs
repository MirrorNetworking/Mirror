using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace Mirror
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class SyncList<T> : IList<T>, IReadOnlyList<T>, ISyncObject
    {
        readonly IList<T> objects;
        readonly IEqualityComparer<T> comparer;

        public int Count => objects.Count;
        public bool IsReadOnly { get; private set; }

        /// <summary>
        /// Raised when an element is added to the list.
        /// Receives index and new item
        /// </summary>
        public event Action<int, T> OnInsert;

        /// <summary>
        /// Raised when the list is cleared
        /// </summary>
        public event Action OnClear;

        /// <summary>
        /// Raised when an item is removed from the list
        /// receives the index and the old item
        /// </summary>
        public event Action<int, T> OnRemove;

        /// <summary>
        /// Raised when an item is changed in a list
        /// Receives index, old item and new item
        /// </summary>
        public event Action<int, T, T> OnSet;

        /// <summary>
        /// Raised after the list has been updated
        /// Note that if there are multiple changes
        /// this event is only raised once.
        /// </summary>
        public event Action OnChange;

        private enum Operation : byte
        {
            OP_ADD,
            OP_CLEAR,
            OP_INSERT,
            OP_REMOVEAT,
            OP_SET
        }

        struct Change
        {
            internal Operation operation;
            internal int index;
            internal T item;
        }

        readonly List<Change> changes = new List<Change>();
        // how many changes we need to ignore
        // this is needed because when we initialize the list,
        // we might later receive changes that have already been applied
        // so we need to skip them
        int changesAhead;

        internal int ChangeCount => changes.Count;

        public SyncList() : this(EqualityComparer<T>.Default)
        {
        }

        public SyncList(IEqualityComparer<T> comparer )
        {
            this.comparer = comparer ?? EqualityComparer<T>.Default;
            objects = new List<T>();
        }

        public SyncList(IList<T> objects, IEqualityComparer<T> comparer = null)
        {
            this.comparer = comparer ?? EqualityComparer<T>.Default;
            this.objects = objects;
        }

        public bool IsDirty => changes.Count > 0;

        // throw away all the changes
        // this should be called after a successfull sync
        public void Flush() => changes.Clear();

        public void Reset()
        {
            IsReadOnly = false;
            changes.Clear();
            changesAhead = 0;
            objects.Clear();
        }

        void AddOperation(Operation op, int itemIndex, T newItem)
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException("Synclists can only be modified at the server");
            }

            var change = new Change
            {
                operation = op,
                index = itemIndex,
                item = newItem
            };

            changes.Add(change);
            OnChange?.Invoke();
        }

        public void OnSerializeAll(NetworkWriter writer)
        {
            // if init,  write the full list content
            writer.WritePackedUInt32((uint)objects.Count);

            for (int i = 0; i < objects.Count; i++)
            {
                T obj = objects[i];
                writer.Write(obj);
            }

            // all changes have been applied already
            // thus the client will need to skip all the pending changes
            // or they would be applied again.
            // So we write how many changes are pending
            writer.WritePackedUInt32((uint)changes.Count);
        }

        public void OnSerializeDelta(NetworkWriter writer)
        {
            // write all the queued up changes
            writer.WritePackedUInt32((uint)changes.Count);

            for (int i = 0; i < changes.Count; i++)
            {
                Change change = changes[i];
                writer.WriteByte((byte)change.operation);

                switch (change.operation)
                {
                    case Operation.OP_ADD:
                        writer.Write(change.item);
                        break;

                    case Operation.OP_CLEAR:
                        break;

                    case Operation.OP_REMOVEAT:
                        writer.WritePackedUInt32((uint)change.index);
                        break;

                    case Operation.OP_INSERT:
                    case Operation.OP_SET:
                        writer.WritePackedUInt32((uint)change.index);
                        writer.Write(change.item);
                        break;
                }
            }
        }

        public void OnDeserializeAll(NetworkReader reader)
        {
            // This list can now only be modified by synchronization
            IsReadOnly = true;

            // if init,  write the full list content
            int count = (int)reader.ReadPackedUInt32();

            objects.Clear();
            OnClear?.Invoke();
            changes.Clear();

            for (int i = 0; i < count; i++)
            {
                T obj = reader.Read<T>();
                objects.Add(obj);
                OnInsert?.Invoke(i, obj);
            }

            // We will need to skip all these changes
            // the next time the list is synchronized
            // because they have already been applied
            changesAhead = (int)reader.ReadPackedUInt32();

            OnChange?.Invoke();
        }

        public void OnDeserializeDelta(NetworkReader reader)
        {
            // This list can now only be modified by synchronization
            IsReadOnly = true;
            bool raiseOnChange = false;

            int changesCount = (int)reader.ReadPackedUInt32();

            for (int i = 0; i < changesCount; i++)
            {
                var operation = (Operation)reader.ReadByte();

                // apply the operation only if it is a new change
                // that we have not applied yet
                bool apply = changesAhead == 0;

                switch (operation)
                {
                    case Operation.OP_ADD:
                        DeserializeAdd(reader, apply);
                        break;

                    case Operation.OP_CLEAR:
                        DeserializeClear(apply);
                        break;

                    case Operation.OP_INSERT:
                        DeserializeInsert(reader, apply);
                        break;

                    case Operation.OP_REMOVEAT:
                        DeserializeRemoveAt(reader, apply);
                        break;

                    case Operation.OP_SET:
                        DeserializeSet(reader, apply);
                        break;
                }

                if (apply)
                {
                    raiseOnChange = true;
                }
                // we just skipped this change
                else
                {
                    changesAhead--;
                }
            }

            if (raiseOnChange)
                OnChange?.Invoke();
        }

        private void DeserializeAdd(NetworkReader reader, bool apply)
        {
            T newItem = reader.Read<T>();
            if (apply)
            {
                objects.Add(newItem);
                OnInsert?.Invoke(objects.Count - 1, newItem);
            }

        }

        private void DeserializeClear(bool apply)
        {
            if (apply)
            {
                objects.Clear();
                OnClear?.Invoke();
            }
        }

        private void DeserializeInsert(NetworkReader reader, bool apply)
        {
            int index = (int)reader.ReadPackedUInt32();
            T newItem = reader.Read<T>();
            if (apply)
            {
                objects.Insert(index, newItem);
                OnInsert?.Invoke(index, newItem);
            }
        }

        private void DeserializeRemoveAt(NetworkReader reader, bool apply)
        {
            int index = (int)reader.ReadPackedUInt32();
            if (apply)
            {
                T oldItem = objects[index];
                objects.RemoveAt(index);
                OnRemove?.Invoke(index, oldItem);
            }
        }

        private void DeserializeSet(NetworkReader reader, bool apply)
        {
            int index = (int)reader.ReadPackedUInt32();
            T newItem = reader.Read<T>();
            if (apply)
            {
                T oldItem = objects[index];
                objects[index] = newItem;
                OnSet?.Invoke(index, oldItem, newItem);
            }
        }

        public void Add(T item)
        {
            objects.Add(item);
            OnInsert?.Invoke(objects.Count - 1, item);
            AddOperation(Operation.OP_ADD, objects.Count - 1, item);
        }

        public void AddRange(IEnumerable<T> range)
        {
            foreach (T entry in range)
            {
                Add(entry);
            }
        }

        public void Clear()
        {
            objects.Clear();
            OnClear?.Invoke();
            AddOperation(Operation.OP_CLEAR, 0, default);
        }

        public bool Contains(T item) => IndexOf(item) >= 0;

        public void CopyTo(T[] array, int arrayIndex) => objects.CopyTo(array, arrayIndex);

        public int IndexOf(T item)
        {
            for (int i = 0; i < objects.Count; ++i)
                if (comparer.Equals(item, objects[i]))
                    return i;
            return -1;
        }

        public int FindIndex(Predicate<T> match)
        {
            for (int i = 0; i < objects.Count; ++i)
                if (match(objects[i]))
                    return i;
            return -1;
        }

        public T Find(Predicate<T> match)
        {
            int i = FindIndex(match);
            return (i != -1) ? objects[i] : default;
        }

        public List<T> FindAll(Predicate<T> match)
        {
            var results = new List<T>();
            for (int i = 0; i < objects.Count; ++i)
                if (match(objects[i]))
                    results.Add(objects[i]);
            return results;
        }

        public void Insert(int index, T item)
        {
            objects.Insert(index, item);
            OnInsert?.Invoke(index, item);
            AddOperation(Operation.OP_INSERT, index, item);
        }

        public void InsertRange(int index, IEnumerable<T> range)
        {
            foreach (T entry in range)
            {
                Insert(index, entry);
                index++;
            }
        }

        public bool Remove(T item)
        {
            int index = IndexOf(item);
            bool result = index >= 0;
            if (result)
            {
                RemoveAt(index);
            }
            return result;
        }

        public void RemoveAt(int index)
        {
            T oldItem = objects[index];
            objects.RemoveAt(index);
            OnRemove?.Invoke(index, oldItem);
            AddOperation(Operation.OP_REMOVEAT, index, default);
        }

        public int RemoveAll(Predicate<T> match)
        {
            var toRemove = new List<T>();
            for (int i = 0; i < objects.Count; ++i)
                if (match(objects[i]))
                    toRemove.Add(objects[i]);

            foreach (T entry in toRemove)
            {
                Remove(entry);
            }

            return toRemove.Count;
        }

        public T this[int i]
        {
            get => objects[i];
            set
            {
                if (!comparer.Equals(objects[i], value))
                {
                    T oldItem = objects[i];
                    objects[i] = value;
                    OnSet?.Invoke(i, oldItem, value);
                    AddOperation(Operation.OP_SET, i, value);
                }
            }
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        // default Enumerator allocates. we need a custom struct Enumerator to
        // not allocate on the heap.
        // (System.Collections.Generic.List<T> source code does the same)
        //
        // benchmark:
        //   uMMORPG with 800 monsters, Skills.GetHealthBonus() which runs a
        //   foreach on skills SyncList:
        //      before: 81.2KB GC per frame
        //      after:     0KB GC per frame
        // => this is extremely important for MMO scale networking
        public struct Enumerator : IEnumerator<T>
        {
            readonly SyncList<T> list;
            int index;
            public T Current { get; private set; }

            public Enumerator(SyncList<T> list)
            {
                this.list = list;
                index = -1;
                Current = default;
            }

            public bool MoveNext()
            {
                if (++index >= list.Count)
                {
                    return false;
                }
                Current = list[index];
                return true;
            }

            public void Reset() => index = -1;
            object IEnumerator.Current => Current;
            public void Dispose() {
                // nothing to dispose
            }
        }
    }
}
