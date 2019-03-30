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

        readonly ISet<T> m_Objects;

        public int Count => m_Objects.Count;
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

        readonly List<Change> Changes = new List<Change>();
        // how many changes we need to ignore
        // this is needed because when we initialize the list,
        // we might later receive changes that have already been applied
        // so we need to skip them
        int changesAhead = 0;

        protected SyncSet(ISet<T> objects)
        {
            this.m_Objects = objects;
        }

        protected virtual void SerializeItem(NetworkWriter writer, T item) { }
        protected virtual T DeserializeItem(NetworkReader reader) => default;

        public bool IsDirty => Changes.Count > 0;

        // throw away all the changes
        // this should be called after a successfull sync
        public void Flush() => Changes.Clear();

        void AddOperation(Operation op, T item)
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException("Synclists can only be modified at the server");
            }

            Change change = new Change
            {
                operation = op,
                item = item
            };

            Changes.Add(change);

            Callback?.Invoke(op, item);
        }

        void AddOperation(Operation op) => AddOperation(op, default(T));

        public void OnSerializeAll(NetworkWriter writer)
        {
            // if init,  write the full list content
            writer.Write(m_Objects.Count);

            foreach (T obj in m_Objects)
            {
                SerializeItem(writer, obj);
            }

            // all changes have been applied already
            // thus the client will need to skip all the pending changes
            // or they would be applied again.
            // So we write how many changes are pending
            writer.Write(Changes.Count);
        }

        public void OnSerializeDelta(NetworkWriter writer)
        {
            // write all the queued up changes
            writer.Write(Changes.Count);

            for (int i = 0; i < Changes.Count; i++)
            {
                Change change = Changes[i];
                writer.Write((byte)change.operation);

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
            int count = reader.ReadInt32();

            m_Objects.Clear();
            Changes.Clear();

            for (int i = 0; i < count; i++)
            {
                T obj = DeserializeItem(reader);
                m_Objects.Add(obj);
            }

            // We will need to skip all these changes
            // the next time the list is synchronized
            // because they have already been applied
            changesAhead = reader.ReadInt32();
        }

        public void OnDeserializeDelta(NetworkReader reader)
        {
            // This list can now only be modified by synchronization
            IsReadOnly = true;

            int changesCount = reader.ReadInt32();

            for (int i = 0; i < changesCount; i++)
            {
                Operation operation = (Operation)reader.ReadByte();

                // apply the operation only if it is a new change
                // that we have not applied yet
                bool apply = changesAhead == 0;
                T item = default(T);

                switch (operation)
                {
                    case Operation.OP_ADD:
                        item = DeserializeItem(reader);
                        if (apply)
                        {
                            m_Objects.Add(item);
                        }
                        break;

                    case Operation.OP_CLEAR:
                        if (apply)
                        {
                            m_Objects.Clear();
                        }
                        break;

                    case Operation.OP_REMOVE:
                        item = DeserializeItem(reader);
                        if (apply)
                        {
                            m_Objects.Remove(item);
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
            if (m_Objects.Add(item))
            {
                AddOperation(Operation.OP_ADD, item);
                return true;
            }
            return false;
        }

        void ICollection<T>.Add(T item)
        {
            if (m_Objects.Add(item))
            {
                AddOperation(Operation.OP_ADD, item);
            }
        }

        public void Clear()
        {
            m_Objects.Clear();
            AddOperation(Operation.OP_CLEAR);
        }

        public bool Contains(T item) => m_Objects.Contains(item);

        public void CopyTo(T[] array, int index) => m_Objects.CopyTo(array, index);

        public bool Remove(T item)
        {
            if (m_Objects.Remove(item))
            {
                AddOperation(Operation.OP_REMOVE, item);
                return true;
            }
            return false;
        }

        public IEnumerator<T> GetEnumerator() => m_Objects.GetEnumerator();

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

        private void IntersectWithSet(ISet<T> otherSet)
        {
            List<T> elements = new List<T>(m_Objects);

            foreach (T element in elements)
            {
                if (!otherSet.Contains(element))
                {
                    Remove(element);
                }
            }
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            return m_Objects.IsProperSubsetOf(other);
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            return m_Objects.IsProperSupersetOf(other);
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            return m_Objects.IsSubsetOf(other);
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            return m_Objects.IsSupersetOf(other);
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            return m_Objects.Overlaps(other);
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            return m_Objects.SetEquals(other);
        }

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
        protected SyncHashSet() : base(new HashSet<T>()) {}
    }

    public abstract class SyncSortedSet<T> : SyncSet<T>
    {
        protected SyncSortedSet() : base(new SortedSet<T>()) { }
    }
}
