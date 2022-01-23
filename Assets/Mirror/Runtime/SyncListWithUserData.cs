using System;
using System.Collections;
using System.Collections.Generic;

namespace Mirror
{
    public class SyncListWithUserData<T, TUserData> : SyncObject, IList<T>, IReadOnlyList<T>
    {
        public delegate void SyncListChanged(Operation op, int itemIndex, T oldItem, T newItem, TUserData userData);

        readonly IList<T> objects;
        readonly IList<TUserData> InternalUserData;
        readonly IEqualityComparer<T> comparer;

        public int Count => objects.Count;
        public bool IsReadOnly { get; private set; }
        public event SyncListChanged Callback;

        public enum Operation : byte
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

        public SyncListWithUserData() : this(EqualityComparer<T>.Default) { }

        public SyncListWithUserData(IEqualityComparer<T> comparer)
        {
            this.comparer = comparer ?? EqualityComparer<T>.Default;
            objects = new List<T>();
            InternalUserData = new List<TUserData>();
        }

        public SyncListWithUserData(IList<T> objects, IEqualityComparer<T> comparer = null)
        {
            this.comparer = comparer ?? EqualityComparer<T>.Default;
            this.objects = objects;
            InternalUserData = new List<TUserData>();
            for (int i = 0; i < objects.Count; i++)
                InternalUserData.Add(default(TUserData));
        }

        // throw away all the changes
        // this should be called after a successful sync
        public override void ClearChanges() => changes.Clear();

        public override void Reset()
        {
            IsReadOnly = false;
            changes.Clear();
            changesAhead = 0;
            objects.Clear();
            InternalUserData.Clear();
        }

        void AddOperation(Operation op, int itemIndex, T oldItem, T newItem, TUserData userData)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("Synclists can only be modified at the server");

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

            Callback?.Invoke(op, itemIndex, oldItem, newItem, userData);
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
            // This list can now only be modified by synchronization
            IsReadOnly = true;

            // if init,  write the full list content
            int count = (int)reader.ReadUInt();

            objects.Clear();
            InternalUserData.Clear();
            changes.Clear();

            for (int i = 0; i < count; i++)
            {
                T obj = reader.Read<T>();
                objects.Add(obj);
                InternalUserData.Add(default(TUserData));
            }

            // We will need to skip all these changes
            // the next time the list is synchronized
            // because they have already been applied
            changesAhead = (int)reader.ReadUInt();
        }

        public override void OnDeserializeDelta(NetworkReader reader)
        {
            // This list can now only be modified by synchronization
            IsReadOnly = true;

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
                TUserData userData = default;

                switch (operation)
                {
                    case Operation.OP_ADD:
                        newItem = reader.Read<T>();
                        if (apply)
                        {
                            index = objects.Count;
                            objects.Add(newItem);
                            InternalUserData.Add(default);
                        }
                        break;

                    case Operation.OP_CLEAR:
                        if (apply)
                        {
                            objects.Clear();
                            InternalUserData.Clear();
                        }
                        break;

                    case Operation.OP_INSERT:
                        index = (int)reader.ReadUInt();
                        newItem = reader.Read<T>();
                        if (apply)
                        {
                            objects.Insert(index, newItem);
                            InternalUserData.Insert(index, default);
                        }
                        break;

                    case Operation.OP_REMOVEAT:
                        index = (int)reader.ReadUInt();
                        if (apply)
                        {
                            oldItem = objects[index];
                            userData = InternalUserData[index];
                            objects.RemoveAt(index);
                            InternalUserData.RemoveAt(index);
                        }
                        break;

                    case Operation.OP_SET:
                        index = (int)reader.ReadUInt();
                        newItem = reader.Read<T>();
                        if (apply)
                        {
                            oldItem = objects[index];
                            objects[index] = newItem;
                        }
                        break;
                }

                if (apply)
                    Callback?.Invoke(operation, index, oldItem, newItem, default);
                else
                    // we just skipped this change
                    changesAhead--;
            }
        }

        public void Add(T item)
        {
            objects.Add(item);
            InternalUserData.Add(default);
            AddOperation(Operation.OP_ADD, objects.Count - 1, default, item, default);
        }

        public void AddRange(IEnumerable<T> range)
        {
            foreach (T entry in range)
                Add(entry);
        }

        public void Clear()
        {
            objects.Clear();
            InternalUserData.Clear();
            AddOperation(Operation.OP_CLEAR, 0, default, default, default);
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

        public void InsertRange(int index, IEnumerable<T> range)
        {
            foreach (T entry in range)
            {
                Insert(index, entry);
                index++;
            }
        }

        public void Insert(int index, T item)
        {
            objects.Insert(index, item);
            InternalUserData.Insert(index, default);
            AddOperation(Operation.OP_INSERT, index, default, item, default);
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
            TUserData userData = InternalUserData[index];
            objects.RemoveAt(index);
            InternalUserData.RemoveAt(index);
            AddOperation(Operation.OP_REMOVEAT, index, oldItem, default, userData);
        }

        public T this[int i]
        {
            get => objects[i];
            set
            {
                if (!comparer.Equals(objects[i], value))
                {
                    T oldItem = objects[i];
                    TUserData userData = InternalUserData[i];
                    objects[i] = value;
                    AddOperation(Operation.OP_SET, i, oldItem, value, default);
                }
            }
        }

        public TUserData GetUserData(int index) => InternalUserData[index];

        public int GetIndex(TUserData userData) => InternalUserData.IndexOf(userData);

        public void SetUserData(int index, TUserData data) => InternalUserData[index] = data;

        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        public struct Enumerator : IEnumerator<T>
        {
            readonly SyncListWithUserData<T, TUserData> list;
            int index;
            public T Current { get; private set; }

            public Enumerator(SyncListWithUserData<T, TUserData> list)
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
