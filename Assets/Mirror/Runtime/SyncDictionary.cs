using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace Mirror
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class SyncDictionary<TKey, TValue> : IDictionary<TKey, TValue>, SyncObject
    {
        public delegate void SyncDictionaryChanged(Operation op, TKey key, TValue item);

        readonly IDictionary<TKey, TValue> objects;

        public int Count => objects.Count;
        public bool IsReadOnly { get; private set; }
        public event SyncDictionaryChanged Callback;

        public enum Operation : byte
        {
            OP_ADD,
            OP_CLEAR,
            OP_REMOVE,
            OP_SET,
            OP_DIRTY
        }

        struct Change
        {
            internal Operation operation;
            internal TKey key;
            internal TValue item;
        }

        readonly List<Change> changes = new List<Change>();
        // how many changes we need to ignore
        // this is needed because when we initialize the list,
        // we might later receive changes that have already been applied
        // so we need to skip them
        int changesAhead;

        protected virtual void SerializeKey(NetworkWriter writer, TKey item) { }
        protected virtual void SerializeItem(NetworkWriter writer, TValue item) { }
        protected virtual TKey DeserializeKey(NetworkReader reader) => default;
        protected virtual TValue DeserializeItem(NetworkReader reader) => default;

        public bool IsDirty => changes.Count > 0;

        public ICollection<TKey> Keys => objects.Keys;

        public ICollection<TValue> Values => objects.Values;

        // throw away all the changes
        // this should be called after a successfull sync
        public void Flush() => changes.Clear();

        protected SyncDictionary()
        {
            objects = new Dictionary<TKey, TValue>();
        }

        protected SyncDictionary(IEqualityComparer<TKey> eq)
        {
            objects = new Dictionary<TKey, TValue>(eq);
        }

        protected SyncDictionary(IDictionary<TKey, TValue> objects)
        {
            this.objects = objects;
        }

        void AddOperation(Operation op, TKey key, TValue item)
        {
            if (IsReadOnly)
            {
                throw new System.InvalidOperationException("SyncDictionaries can only be modified by the server");
            }

            Change change = new Change
            {
                operation = op,
                key = key,
                item = item
            };

            changes.Add(change);

            Callback?.Invoke(op, key, item);
        }

        public void OnSerializeAll(NetworkWriter writer)
        {
            // if init,  write the full list content
            writer.WritePackedUInt32((uint)objects.Count);

            foreach (KeyValuePair<TKey, TValue> syncItem in objects)
            {
                SerializeKey(writer, syncItem.Key);
                SerializeItem(writer, syncItem.Value);
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
                    case Operation.OP_SET:
                    case Operation.OP_DIRTY:
                        SerializeKey(writer, change.key);
                        SerializeItem(writer, change.item);
                        break;
                    case Operation.OP_CLEAR:
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
                TKey key = DeserializeKey(reader);
                TValue obj = DeserializeItem(reader);
                objects.Add(key, obj);
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
                TKey key = default;
                TValue item = default;

                switch (operation)
                {
                    case Operation.OP_ADD:
                    case Operation.OP_SET:
                    case Operation.OP_DIRTY:
                        key = DeserializeKey(reader);
                        item = DeserializeItem(reader);
                        if (apply)
                        {
                            objects[key] = item;
                        }
                        break;

                    case Operation.OP_CLEAR:
                        if (apply)
                        {
                            objects.Clear();
                        }
                        break;

                    case Operation.OP_REMOVE:
                        key = DeserializeKey(reader);
                        item = DeserializeItem(reader);
                        if (apply)
                        {
                            objects.Remove(key);
                        }
                        break;
                }

                if (apply)
                {
                    Callback?.Invoke(operation, key, item);
                }
                // we just skipped this change
                else
                {
                    changesAhead--;
                }
            }
        }

        public void Clear()
        {
            objects.Clear();
            AddOperation(Operation.OP_CLEAR, default, default);
        }

        public bool ContainsKey(TKey key) => objects.ContainsKey(key);

        public bool Remove(TKey key)
        {
            if (objects.TryGetValue(key, out TValue item) && objects.Remove(key))
            {
                AddOperation(Operation.OP_REMOVE, key, item);
                return true;
            }
            return false;
        }

        public void Dirty(TKey index)
        {
            AddOperation(Operation.OP_DIRTY, index, objects[index]);
        }

        public TValue this[TKey i]
        {
            get => objects[i];
            set
            {
                if (ContainsKey(i))
                {
                    AddOperation(Operation.OP_SET, i, value);
                }
                else
                {
                    AddOperation(Operation.OP_ADD, i, value);
                }
                objects[i] = value;
            }
        }

        public bool TryGetValue(TKey key, out TValue value) => objects.TryGetValue(key, out value);

        public void Add(TKey key, TValue value)
        {
            objects.Add(key, value);
            AddOperation(Operation.OP_ADD, key, value);
        }

        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return TryGetValue(item.Key, out TValue val) && EqualityComparer<TValue>.Default.Equals(val, item.Value);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new System.ArgumentNullException("Array Is Null");
            }
            if (arrayIndex < 0 || arrayIndex > array.Length)
            {
                throw new System.ArgumentOutOfRangeException("Array Index Out of Range");
            }
            if (array.Length - arrayIndex < Count)
            {
                throw new System.ArgumentException("The number of items in the SyncDictionary is greater than the available space from arrayIndex to the end of the destination array");
            }

            int i = arrayIndex;
            foreach (KeyValuePair<TKey, TValue> item in objects)
            {
                array[i] = item;
                i++;
            }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            bool result = objects.Remove(item.Key);
            if (result)
            {
                AddOperation(Operation.OP_REMOVE, item.Key, item.Value);
            }
            return result;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => ((IDictionary<TKey, TValue>)objects).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IDictionary<TKey, TValue>)objects).GetEnumerator();
    }
}
