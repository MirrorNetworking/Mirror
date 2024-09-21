using System;
using System.Collections;
using System.Collections.Generic;

namespace Mirror
{
    public class SyncList<T> : SyncObject, IList<T>, IReadOnlyList<T>
    {
        public enum Operation : byte
        {
            OP_ADD,
            OP_SET,
            OP_INSERT,
            OP_REMOVEAT,
            OP_CLEAR
        }

        /// <summary>This is called after the item is added with index</summary>
        public Action<int> OnAdd;

        /// <summary>This is called after the item is inserted with inedx</summary>
        public Action<int> OnInsert;

        /// <summary>This is called after the item is set with index and OLD Value</summary>
        public Action<int, T> OnSet;

        /// <summary>This is called after the item is removed with index and OLD Value</summary>
        public Action<int, T> OnRemove;

        /// <summary>
        /// This is called for all changes to the List.
        /// <para>For OP_ADD and OP_INSERT, T is the NEW value of the entry.</para>
        /// <para>For OP_SET and OP_REMOVE, T is the OLD value of the entry.</para>
        /// <para>For OP_CLEAR, T is default.</para>
        /// </summary>
        public Action<Operation, int, T> OnChange;

        /// <summary>This is called before the list is cleared so the list can be iterated</summary>
        public Action OnClear;

        readonly IList<T> objects;
        readonly IEqualityComparer<T> comparer;

        public int Count => objects.Count;
        public bool IsReadOnly => !IsWritable();

        struct Change
        {
            internal Operation operation;
            internal int index;
            internal T item;
        }

        // list of changes.
        // -> insert/delete/clear is only ONE change
        // -> changing the same slot 10x caues 10 changes.
        // -> note that this grows until next sync(!)
        readonly List<Change> changes = new List<Change>();

        // how many changes we need to ignore
        // this is needed because when we initialize the list,
        // we might later receive changes that have already been applied
        // so we need to skip them
        int changesAhead;

        public SyncList() : this(EqualityComparer<T>.Default) { }

        public SyncList(IEqualityComparer<T> comparer)
        {
            this.comparer = comparer ?? EqualityComparer<T>.Default;
            objects = new List<T>();
        }

        public SyncList(IList<T> objects, IEqualityComparer<T> comparer = null)
        {
            this.comparer = comparer ?? EqualityComparer<T>.Default;
            this.objects = objects;
        }

        // throw away all the changes
        // this should be called after a successful sync
        public override void ClearChanges() => changes.Clear();

        public override void Reset()
        {
            changes.Clear();
            changesAhead = 0;
            objects.Clear();
        }

        void AddOperation(Operation op, int itemIndex, T oldItem, T newItem, bool checkAccess)
        {
            if (checkAccess && IsReadOnly)
                throw new InvalidOperationException("Synclists can only be modified by the owner.");

            Change change = new Change
            {
                operation = op,
                index = itemIndex,
                item = newItem
            };

            if (IsRecording())
            {
                changes.Add(change);
                OnDirty?.Invoke();
            }

            switch (op)
            {
                case Operation.OP_ADD:
                    OnAdd?.Invoke(itemIndex);
                    OnChange?.Invoke(op, itemIndex, newItem);
                    break;
                case Operation.OP_INSERT:
                    OnInsert?.Invoke(itemIndex);
                    OnChange?.Invoke(op, itemIndex, newItem);
                    break;
                case Operation.OP_SET:
                    OnSet?.Invoke(itemIndex, oldItem);
                    OnChange?.Invoke(op, itemIndex, oldItem);
                    break;
                case Operation.OP_REMOVEAT:
                    OnRemove?.Invoke(itemIndex, oldItem);
                    OnChange?.Invoke(op, itemIndex, oldItem);
                    break;
                case Operation.OP_CLEAR:
                    OnClear?.Invoke();
                    OnChange?.Invoke(op, itemIndex, default);
                    break;
            }
        }

        public override void OnSerializeAll(NetworkWriter writer)
        {
            // if init,  write the full list content
            writer.WriteUInt((uint)objects.Count);

            for (int i = 0; i < objects.Count; i++)
            {
                T obj = objects[i];
                writer.Write(obj);
            }

            // all changes have been applied already
            // thus the client will need to skip all the pending changes
            // or they would be applied again.
            // So we write how many changes are pending
            writer.WriteUInt((uint)changes.Count);
        }

        public override void OnSerializeDelta(NetworkWriter writer)
        {
            // write all the queued up changes
            writer.WriteUInt((uint)changes.Count);

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
                        writer.WriteUInt((uint)change.index);
                        break;

                    case Operation.OP_INSERT:
                    case Operation.OP_SET:
                        writer.WriteUInt((uint)change.index);
                        writer.Write(change.item);
                        break;
                }
            }
        }

        public override void OnDeserializeAll(NetworkReader reader)
        {
            // if init,  write the full list content
            int count = (int)reader.ReadUInt();

            objects.Clear();
            changes.Clear();

            for (int i = 0; i < count; i++)
            {
                T obj = reader.Read<T>();
                objects.Add(obj);
            }

            // We will need to skip all these changes
            // the next time the list is synchronized
            // because they have already been applied
            changesAhead = (int)reader.ReadUInt();
        }

        public override void OnDeserializeDelta(NetworkReader reader)
        {
            int changesCount = (int)reader.ReadUInt();

            for (int i = 0; i < changesCount; i++)
            {
                Operation operation = (Operation)reader.ReadByte();

                // apply the operation only if it is a new change
                // that we have not applied yet
                bool apply = changesAhead == 0;
                int index = 0;
                T oldItem = default;
                T newItem = default;

                switch (operation)
                {
                    case Operation.OP_ADD:
                        newItem = reader.Read<T>();
                        if (apply)
                        {
                            index = objects.Count;
                            objects.Add(newItem);
                            // add dirty + changes.
                            // ClientToServer needs to set dirty in server OnDeserialize.
                            // no access check: server OnDeserialize can always
                            // write, even for ClientToServer (for broadcasting).
                            AddOperation(Operation.OP_ADD, objects.Count - 1, default, newItem, false);
                        }
                        break;

                    case Operation.OP_CLEAR:
                        if (apply)
                        {
                            // add dirty + changes.
                            // ClientToServer needs to set dirty in server OnDeserialize.
                            // no access check: server OnDeserialize can always
                            // write, even for ClientToServer (for broadcasting).
                            AddOperation(Operation.OP_CLEAR, 0, default, default, false);
                            // clear after invoking the callback so users can iterate the list
                            // and take appropriate action on the items before they are wiped.
                            objects.Clear();
                        }
                        break;

                    case Operation.OP_INSERT:
                        index = (int)reader.ReadUInt();
                        newItem = reader.Read<T>();
                        if (apply)
                        {
                            objects.Insert(index, newItem);
                            // add dirty + changes.
                            // ClientToServer needs to set dirty in server OnDeserialize.
                            // no access check: server OnDeserialize can always
                            // write, even for ClientToServer (for broadcasting).
                            AddOperation(Operation.OP_INSERT, index, default, newItem, false);
                        }
                        break;

                    case Operation.OP_REMOVEAT:
                        index = (int)reader.ReadUInt();
                        if (apply)
                        {
                            oldItem = objects[index];
                            objects.RemoveAt(index);
                            // add dirty + changes.
                            // ClientToServer needs to set dirty in server OnDeserialize.
                            // no access check: server OnDeserialize can always
                            // write, even for ClientToServer (for broadcasting).
                            AddOperation(Operation.OP_REMOVEAT, index, oldItem, default, false);
                        }
                        break;

                    case Operation.OP_SET:
                        index = (int)reader.ReadUInt();
                        newItem = reader.Read<T>();
                        if (apply)
                        {
                            oldItem = objects[index];
                            objects[index] = newItem;
                            // add dirty + changes.
                            // ClientToServer needs to set dirty in server OnDeserialize.
                            // no access check: server OnDeserialize can always
                            // write, even for ClientToServer (for broadcasting).
                            AddOperation(Operation.OP_SET, index, oldItem, newItem, false);
                        }
                        break;
                }

                if (!apply)
                {
                    // we just skipped this change
                    changesAhead--;
                }
            }
        }

        public void Add(T item)
        {
            objects.Add(item);
            AddOperation(Operation.OP_ADD, objects.Count - 1, default, item, true);
        }

        public void AddRange(IEnumerable<T> range)
        {
            foreach (T entry in range)
                Add(entry);
        }

        public void Clear()
        {
            AddOperation(Operation.OP_CLEAR, 0, default, default, true);
            // clear after invoking the callback so users can iterate the list
            // and take appropriate action on the items before they are wiped.
            objects.Clear();
        }

        public bool Contains(T item) => IndexOf(item) >= 0;

        public void CopyTo(T[] array, int index) => objects.CopyTo(array, index);

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
            List<T> results = new List<T>();
            for (int i = 0; i < objects.Count; ++i)
                if (match(objects[i]))
                    results.Add(objects[i]);
            return results;
        }

        public void Insert(int index, T item)
        {
            objects.Insert(index, item);
            AddOperation(Operation.OP_INSERT, index, default, item, true);
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
                RemoveAt(index);

            return result;
        }

        public void RemoveAt(int index)
        {
            T oldItem = objects[index];
            objects.RemoveAt(index);
            AddOperation(Operation.OP_REMOVEAT, index, oldItem, default, true);
        }

        public int RemoveAll(Predicate<T> match)
        {
            List<T> toRemove = new List<T>();
            for (int i = 0; i < objects.Count; ++i)
                if (match(objects[i]))
                    toRemove.Add(objects[i]);

            foreach (T entry in toRemove)
                Remove(entry);

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
                    AddOperation(Operation.OP_SET, i, oldItem, value, true);
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
                    return false;

                Current = list[index];
                return true;
            }

            public void Reset() => index = -1;
            object IEnumerator.Current => Current;
            public void Dispose() { }
        }
    }
}
