using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace Mirror
{
    public class SyncListString : SyncList<string>
    {
        protected override void SerializeItem(NetworkWriter writer, string item) => writer.WriteString(item);
        protected override string DeserializeItem(NetworkReader reader) => reader.ReadString();
    }

    public class SyncListFloat : SyncList<float>
    {
        protected override void SerializeItem(NetworkWriter writer, float item) => writer.WriteSingle(item);
        protected override float DeserializeItem(NetworkReader reader) => reader.ReadSingle();
    }

    public class SyncListInt : SyncList<int>
    {
        protected override void SerializeItem(NetworkWriter writer, int item) => writer.WritePackedInt32(item);
        protected override int DeserializeItem(NetworkReader reader) => reader.ReadPackedInt32();
    }

    public class SyncListUInt : SyncList<uint>
    {
        protected override void SerializeItem(NetworkWriter writer, uint item) => writer.WritePackedUInt32(item);
        protected override uint DeserializeItem(NetworkReader reader) => reader.ReadPackedUInt32();
    }

    public class SyncListBool : SyncList<bool>
    {
        protected override void SerializeItem(NetworkWriter writer, bool item) => writer.WriteBoolean(item);
        protected override bool DeserializeItem(NetworkReader reader) => reader.ReadBoolean();
    }

    // Original UNET name is SyncListStruct and original Weaver weavers anything
    // that contains the name 'SyncListStruct', without considering the name-
    // space.
    [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use SyncList<MyStruct> instead")]
    public class SyncListSTRUCT<T> : SyncList<T> where T : struct
    {
        public T GetItem(int i) => base[i];
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class SyncList<T> : IList<T>, IReadOnlyList<T>, SyncObject
    {
        public delegate void SyncListChanged(Operation op, int itemIndex, T item);

        readonly IList<T> objects;

        public int Count => objects.Count;
        public bool IsReadOnly { get; private set; }
        public event SyncListChanged Callback;

        public enum Operation : byte
        {
            OP_ADD,
            OP_CLEAR,
            OP_INSERT,
            OP_REMOVE,
            OP_REMOVEAT,
            OP_SET,
            OP_DIRTY
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

        protected virtual void SerializeItem(NetworkWriter writer, T item) { }
        protected virtual T DeserializeItem(NetworkReader reader) => default;


        protected SyncList()
        {
            objects = new List<T>();
        }

        protected SyncList(IList<T> objects)
        {
            this.objects = objects;
        }

        public bool IsDirty => changes.Count > 0;

        // throw away all the changes
        // this should be called after a successfull sync
        public void Flush() => changes.Clear();

        void AddOperation(Operation op, int itemIndex, T item)
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException("Synclists can only be modified at the server");
            }

            Change change = new Change
            {
                operation = op,
                index = itemIndex,
                item = item
            };

            changes.Add(change);

            Callback?.Invoke(op, itemIndex, item);
        }

        void AddOperation(Operation op, int itemIndex) => AddOperation(op, itemIndex, default);

        public void OnSerializeAll(NetworkWriter writer)
        {
            // if init,  write the full list content
            writer.WritePackedUInt32((uint)objects.Count);

            for (int i = 0; i < objects.Count; i++)
            {
                T obj = objects[i];
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
                    case Operation.OP_REMOVE:
                        SerializeItem(writer, change.item);
                        break;

                    case Operation.OP_CLEAR:
                        break;

                    case Operation.OP_REMOVEAT:
                        writer.WritePackedUInt32((uint)change.index);
                        break;

                    case Operation.OP_INSERT:
                    case Operation.OP_SET:
                    case Operation.OP_DIRTY:
                        writer.WritePackedUInt32((uint)change.index);
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
                int index = 0;
                T item = default;

                switch (operation)
                {
                    case Operation.OP_ADD:
                        item = DeserializeItem(reader);
                        if (apply)
                        {
                            index = objects.Count;
                            objects.Add(item);
                        }
                        break;

                    case Operation.OP_CLEAR:
                        if (apply)
                        {
                            objects.Clear();
                        }
                        break;

                    case Operation.OP_INSERT:
                        index = (int)reader.ReadPackedUInt32();
                        item = DeserializeItem(reader);
                        if (apply)
                        {
                            objects.Insert(index, item);
                        }
                        break;

                    case Operation.OP_REMOVE:
                        item = DeserializeItem(reader);
                        if (apply)
                        {
                            objects.Remove(item);
                        }
                        break;

                    case Operation.OP_REMOVEAT:
                        index = (int)reader.ReadPackedUInt32();
                        if (apply)
                        {
                            item = objects[index];
                            objects.RemoveAt(index);
                        }
                        break;

                    case Operation.OP_SET:
                    case Operation.OP_DIRTY:
                        index = (int)reader.ReadPackedUInt32();
                        item = DeserializeItem(reader);
                        if (apply)
                        {
                            objects[index] = item;
                        }
                        break;
                }

                if (apply)
                {
                    Callback?.Invoke(operation, index, item);
                }
                // we just skipped this change
                else
                {
                    changesAhead--;
                }
            }
        }

        public void Add(T item)
        {
            objects.Add(item);
            AddOperation(Operation.OP_ADD, objects.Count - 1, item);
        }

        public void Clear()
        {
            objects.Clear();
            AddOperation(Operation.OP_CLEAR, 0);
        }

        public bool Contains(T item) => objects.Contains(item);

        public void CopyTo(T[] array, int index) => objects.CopyTo(array, index);

        public int IndexOf(T item) => objects.IndexOf(item);

        public int FindIndex(Predicate<T> match)
        {
            for (int i = 0; i < objects.Count; ++i)
                if (match(objects[i]))
                    return i;
            return -1;
        }

        public void Insert(int index, T item)
        {
            objects.Insert(index, item);
            AddOperation(Operation.OP_INSERT, index, item);
        }

        public bool Remove(T item)
        {
            bool result = objects.Remove(item);
            if (result)
            {
                AddOperation(Operation.OP_REMOVE, 0, item);
            }
            return result;
        }

        public void RemoveAt(int index)
        {
            objects.RemoveAt(index);
            AddOperation(Operation.OP_REMOVEAT, index);
        }

        public void Dirty(int index)
        {
            AddOperation(Operation.OP_DIRTY, index, objects[index]);
        }

        public T this[int i]
        {
            get => objects[i];
            set
            {
                if (!EqualityComparer<T>.Default.Equals(objects[i], value))
                {
                    objects[i] = value;
                    AddOperation(Operation.OP_SET, i, value);
                }
            }
        }

        public IEnumerator<T> GetEnumerator() => objects.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
