using System;
using System.Collections;
using System.Collections.Generic;

namespace Mirror
{
    public class SyncSet<T> : SyncObject, ISet<T>
    {
        /// <summary>This is called after the item is added. T is the new item.</summary>
        public Action<T> OnAdd;

        /// <summary>This is called after the item is removed. T is the OLD item</summary>
        public Action<T> OnRemove;

        /// <summary>
        /// This is called for all changes to the Set.
        /// <para>For OP_ADD, T is the NEW value of the entry.</para>
        /// <para>For OP_REMOVE, T is the OLD value of the entry.</para>
        /// <para>For OP_CLEAR, T is default.</para>
        /// </summary>
        public Action<Operation, T> OnChange;

        /// <summary>This is called BEFORE the data is cleared</summary>
        public Action OnClear;

        // Deprecated 2024-03-22
        [Obsolete("Use individual Actions, which pass OLD value where appropriate, instead.")]
        public Action<Operation, T> Callback;

        protected readonly ISet<T> objects;

        public int Count => objects.Count;
        public bool IsReadOnly => !IsWritable();

        public enum Operation : byte
        {
            OP_ADD,
            OP_REMOVE,
            OP_CLEAR
        }

        struct Change
        {
            internal Operation operation;
            internal T item;
        }

        // list of changes.
        // -> insert/delete/clear is only ONE change
        // -> changing the same slot 10x caues 10 changes.
        // -> note that this grows until next sync(!)
        // TODO Dictionary<key, change> to avoid ever growing changes / redundant changes!
        readonly List<Change> changes = new List<Change>();

        // how many changes we need to ignore
        // this is needed because when we initialize the list,
        // we might later receive changes that have already been applied
        // so we need to skip them
        int changesAhead;

        public SyncSet(ISet<T> objects)
        {
            this.objects = objects;
        }

        public override void Reset()
        {
            changes.Clear();
            changesAhead = 0;
            objects.Clear();
        }

        // throw away all the changes
        // this should be called after a successful sync
        public override void ClearChanges() => changes.Clear();

        void AddOperation(Operation op, T oldItem, T newItem, bool checkAccess)
        {
            if (checkAccess && IsReadOnly)
                throw new InvalidOperationException("SyncSets can only be modified by the owner.");

            Change change = default;
            switch (op)
            {
                case Operation.OP_ADD:
                    change = new Change
                    {
                        operation = op,
                        item = newItem
                    };
                    break;
                case Operation.OP_REMOVE:
                    change = new Change
                    {
                        operation = op,
                        item = oldItem
                    };
                    break;
                case Operation.OP_CLEAR:
                    change = new Change
                    {
                        operation = op,
                        item = default
                    };
                    break;
            }

            if (IsRecording())
            {
                changes.Add(change);
                OnDirty?.Invoke();
            }

            switch (op)
            {
                case Operation.OP_ADD:
                    OnAdd?.Invoke(newItem);
                    OnChange?.Invoke(op, newItem);
#pragma warning disable CS0618 // Type or member is obsolete
                    Callback?.Invoke(op, newItem);
#pragma warning restore CS0618 // Type or member is obsolete
                    break;
                case Operation.OP_REMOVE:
                    OnRemove?.Invoke(oldItem);
                    OnChange?.Invoke(op, oldItem);
#pragma warning disable CS0618 // Type or member is obsolete
                    Callback?.Invoke(op, oldItem);
#pragma warning restore CS0618 // Type or member is obsolete
                    break;
                case Operation.OP_CLEAR:
                    OnClear?.Invoke();
                    OnChange?.Invoke(op, default);
#pragma warning disable CS0618 // Type or member is obsolete
                    Callback?.Invoke(op, default);
#pragma warning restore CS0618 // Type or member is obsolete
                    break;
            }
        }

        void AddOperation(Operation op, bool checkAccess) => AddOperation(op, default, default, checkAccess);

        public override void OnSerializeAll(NetworkWriter writer)
        {
            // if init,  write the full list content
            writer.WriteUInt((uint)objects.Count);

            foreach (T obj in objects)
                writer.Write(obj);

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
                    case Operation.OP_REMOVE:
                        writer.Write(change.item);
                        break;
                    case Operation.OP_CLEAR:
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
                T oldItem = default;
                T newItem = default;

                switch (operation)
                {
                    case Operation.OP_ADD:
                        newItem = reader.Read<T>();
                        if (apply)
                        {
                            objects.Add(newItem);
                            // add dirty + changes.
                            // ClientToServer needs to set dirty in server OnDeserialize.
                            // no access check: server OnDeserialize can always
                            // write, even for ClientToServer (for broadcasting).
                            AddOperation(Operation.OP_ADD, default, newItem, false);
                        }
                        break;

                    case Operation.OP_REMOVE:
                        oldItem = reader.Read<T>();
                        if (apply)
                        {
                            objects.Remove(oldItem);
                            // add dirty + changes.
                            // ClientToServer needs to set dirty in server OnDeserialize.
                            // no access check: server OnDeserialize can always
                            // write, even for ClientToServer (for broadcasting).
                            AddOperation(Operation.OP_REMOVE, oldItem, default, false);
                        }
                        break;

                    case Operation.OP_CLEAR:
                        if (apply)
                        {
                            // add dirty + changes.
                            // ClientToServer needs to set dirty in server OnDeserialize.
                            // no access check: server OnDeserialize can always
                            // write, even for ClientToServer (for broadcasting).
                            AddOperation(Operation.OP_CLEAR, false);
                            // clear after invoking the callback so users can iterate the set
                            // and take appropriate action on the items before they are wiped.
                            objects.Clear();
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

        public bool Add(T item)
        {
            if (objects.Add(item))
            {
                AddOperation(Operation.OP_ADD, default, item, true);
                return true;
            }
            return false;
        }

        void ICollection<T>.Add(T item)
        {
            if (objects.Add(item))
                AddOperation(Operation.OP_ADD, default, item, true);
        }

        public void Clear()
        {
            AddOperation(Operation.OP_CLEAR, true);
            // clear after invoking the callback so users can iterate the set
            // and take appropriate action on the items before they are wiped.
            objects.Clear();
        }

        public bool Contains(T item) => objects.Contains(item);

        public void CopyTo(T[] array, int index) => objects.CopyTo(array, index);

        public bool Remove(T item)
        {
            if (objects.Remove(item))
            {
                AddOperation(Operation.OP_REMOVE, item, default, true);
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
                Remove(element);
        }

        public void IntersectWith(IEnumerable<T> other)
        {
            if (other is ISet<T> otherSet)
                IntersectWithSet(otherSet);
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
                if (!otherSet.Contains(element))
                    Remove(element);
        }

        public bool IsProperSubsetOf(IEnumerable<T> other) => objects.IsProperSubsetOf(other);

        public bool IsProperSupersetOf(IEnumerable<T> other) => objects.IsProperSupersetOf(other);

        public bool IsSubsetOf(IEnumerable<T> other) => objects.IsSubsetOf(other);

        public bool IsSupersetOf(IEnumerable<T> other) => objects.IsSupersetOf(other);

        public bool Overlaps(IEnumerable<T> other) => objects.Overlaps(other);

        public bool SetEquals(IEnumerable<T> other) => objects.SetEquals(other);

        // custom implementation so we can do our own Clear/Add/Remove for delta
        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            if (other == this)
                Clear();
            else
                foreach (T element in other)
                    if (!Remove(element))
                        Add(element);
        }

        // custom implementation so we can do our own Clear/Add/Remove for delta
        public void UnionWith(IEnumerable<T> other)
        {
            if (other != this)
                foreach (T element in other)
                    Add(element);
        }
    }

    public class SyncHashSet<T> : SyncSet<T>
    {
        public SyncHashSet() : this(EqualityComparer<T>.Default) { }
        public SyncHashSet(IEqualityComparer<T> comparer) : base(new HashSet<T>(comparer ?? EqualityComparer<T>.Default)) { }

        // allocation free enumerator
        public new HashSet<T>.Enumerator GetEnumerator() => ((HashSet<T>)objects).GetEnumerator();
    }

    public class SyncSortedSet<T> : SyncSet<T>
    {
        public SyncSortedSet() : this(Comparer<T>.Default) { }
        public SyncSortedSet(IComparer<T> comparer) : base(new SortedSet<T>(comparer ?? Comparer<T>.Default)) { }

        // allocation free enumerator
        public new SortedSet<T>.Enumerator GetEnumerator() => ((SortedSet<T>)objects).GetEnumerator();
    }
}
