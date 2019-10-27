using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace Mirror
{

    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class SyncSet<T> : ISet<T>, SyncObject
    {
        public delegate void SyncSetChanged(Operation op, T item);

        protected readonly ISet<T> objects;

        public int Count => objects.Count;
        public bool IsReadOnly { get; private set; }
        public event SyncSetChanged Callback;

        public enum Operation : byte
        {
            OP_ADD,
            OP_CLEAR,
            OP_REMOVE
        }

        struct Change
        {
            internal Operation operation;
            internal T item;
        }

        readonly List<Change> changes = new List<Change>();
        // how many changes we need to ignore
        // this is needed because when we initialize the list,
        // we might later receive changes that have already been applied
        // so we need to skip them
        int changesAhead;

        protected SyncSet(ISet<T> objects)
        {
            this.objects = objects;
        }

        protected virtual void SerializeItem(NetworkWriter writer, T item) { }
        protected virtual T DeserializeItem(NetworkReader reader) => default;

        public bool IsDirty => changes.Count > 0;

        // throw away all the changes
        // this should be called after a successfull sync
        public void Flush() => changes.Clear();

        void AddOperation(Operation op, T item)
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException("SyncSets can only be modified at the server");
            }

            Change change = new Change
            {
                operation = op,
                item = item
            };

            changes.Add(change);

            Callback?.Invoke(op, item);
        }

        void AddOperation(Operation op) => AddOperation(op, default);

        public void OnSerializeAll(NetworkWriter writer)
        {
            // if init,  write the full list content
            writer.WritePackedUInt32((uint)objects.Count);

            foreach (T obj in objects)
            {
                SerializeItem(writer, obj);
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
                        SerializeItem(writer, change.item);
                        break;

                    case Operation.OP_CLEAR:
                        break;

                    case Operation.OP_REMOVE:
                        SerializeItem(writer, change.item);
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
            changes.Clear();

            for (int i = 0; i < count; i++)
            {
                T obj = DeserializeItem(reader);
                objects.Add(obj);
            }

            // We will need to skip all these changes
            // the next time the list is synchronized
            // because they have already been applied
            changesAhead = (int)reader.ReadPackedUInt32();
        }

        public void OnDeserializeDelta(NetworkReader reader)
        {
            // This list can now only be modified by synchronization
            IsReadOnly = true;

            int changesCount = (int)reader.ReadPackedUInt32();

            for (int i = 0; i < changesCount; i++)
            {
                Operation operation = (Operation)reader.ReadByte();

                // apply the operation only if it is a new change
                // that we have not applied yet
                bool apply = changesAhead == 0;
                T item = default;

                switch (operation)
                {
                    case Operation.OP_ADD:
                        item = DeserializeItem(reader);
                        if (apply)
                        {
                            objects.Add(item);
                        }
                        break;

                    case Operation.OP_CLEAR:
                        if (apply)
                        {
                            objects.Clear();
                        }
                        break;

                    case Operation.OP_REMOVE:
                        item = DeserializeItem(reader);
                        if (apply)
                        {
                            objects.Remove(item);
                        }
                        break;
                }

                if (apply)
                {
                    Callback?.Invoke(operation, item);
                }
                // we just skipped this change
                else
                {
                    changesAhead--;
                }
            }
        }

        public bool Add(T item)
        {
            if (objects.Add(item))
            {
                AddOperation(Operation.OP_ADD, item);
                return true;
            }
            return false;
        }

        void ICollection<T>.Add(T item)
        {
            if (objects.Add(item))
            {
                AddOperation(Operation.OP_ADD, item);
            }
        }

        public void Clear()
        {
            objects.Clear();
            AddOperation(Operation.OP_CLEAR);
        }

        public bool Contains(T item) => objects.Contains(item);

        public void CopyTo(T[] array, int index) => objects.CopyTo(array, index);

        public bool Remove(T item)
        {
            if (objects.Remove(item))
            {
                AddOperation(Operation.OP_REMOVE, item);
                return true;
            }
            return false;
        }

        public IEnumerator<T> GetEnumerator() => objects.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void ExceptWith(IEnumerable<T> other)
        {
            if (other == this)
            {
                Clear();
                return;
            }

            // remove every element in other from this
            foreach (T element in other)
            {
                Remove(element);
            }
        }

        public void IntersectWith(IEnumerable<T> other)
        {
            if (other is ISet<T> otherSet)
            {
                IntersectWithSet(otherSet);
            }
            else
            {
                HashSet<T> otherAsSet = new HashSet<T>(other);
                IntersectWithSet(otherAsSet);
            }
        }

        void IntersectWithSet(ISet<T> otherSet)
        {
            List<T> elements = new List<T>(objects);

            foreach (T element in elements)
            {
                if (!otherSet.Contains(element))
                {
                    Remove(element);
                }
            }
        }

        public bool IsProperSubsetOf(IEnumerable<T> other) => objects.IsProperSubsetOf(other);

        public bool IsProperSupersetOf(IEnumerable<T> other) => objects.IsProperSupersetOf(other);

        public bool IsSubsetOf(IEnumerable<T> other) => objects.IsSubsetOf(other);

        public bool IsSupersetOf(IEnumerable<T> other) => objects.IsSupersetOf(other);

        public bool Overlaps(IEnumerable<T> other) => objects.Overlaps(other);

        public bool SetEquals(IEnumerable<T> other) => objects.SetEquals(other);

        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            if (other == this)
            {
                Clear();
            }
            else
            {
                foreach (T element in other)
                {
                    if (!Remove(element))
                    {
                        Add(element);
                    }
                }
            }
        }

        public void UnionWith(IEnumerable<T> other)
        {
            if (other != this)
            {
                foreach (T element in other)
                {
                    Add(element);
                }
            }
        }
    }

    public abstract class SyncHashSet<T> : SyncSet<T>
    {
        protected SyncHashSet(IEqualityComparer<T> comparer = null) : base(new HashSet<T>(comparer ?? EqualityComparer<T>.Default)) { }

        // allocation free enumerator
        public new HashSet<T>.Enumerator GetEnumerator() => ((HashSet<T>)objects).GetEnumerator();
    }

    public abstract class SyncSortedSet<T> : SyncSet<T>
    {
        protected SyncSortedSet(IComparer<T> comparer = null) : base(new SortedSet<T>(comparer ?? Comparer<T>.Default)) { }

        // allocation free enumerator
        public new SortedSet<T>.Enumerator GetEnumerator() => ((SortedSet<T>)objects).GetEnumerator();
    }
}
