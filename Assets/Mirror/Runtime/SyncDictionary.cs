using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace Mirror
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class SyncDictionary<K, V> : IDictionary<K, V>, SyncObject
    {
        public delegate void SyncDictionaryChanged(Operation op, K key, V item);

        readonly IDictionary<K, V> objects;

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
            internal K key;
            internal V item;
        }

        readonly List<Change> changes = new List<Change>();
        // how many changes we need to ignore
        // this is needed because when we initialize the list,
        // we might later receive changes that have already been applied
        // so we need to skip them
        int changesAhead;

        protected virtual void SerializeKey(NetworkWriter writer, K item) {}
        protected virtual void SerializeItem(NetworkWriter writer, V item) {}
        protected virtual K DeserializeKey(NetworkReader reader) => default;
        protected virtual V DeserializeItem(NetworkReader reader) => default;

        public bool IsDirty => changes.Count > 0;

        public ICollection<K> Keys => objects.Keys;

        public ICollection<V> Values => objects.Values;

        // throw away all the changes
        // this should be called after a successfull sync
        public void Flush() => changes.Clear();

        protected SyncDictionary()
        {
            objects = new Dictionary<K, V>();
        }

        protected SyncDictionary(IEqualityComparer<K> eq)
        {
            objects = new Dictionary<K, V>(eq);
        }

        protected SyncDictionary(IDictionary<K,V> objects)
        {
            this.objects = objects;
        }

        void AddOperation(Operation op, K key, V item)
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

            foreach (KeyValuePair<K, V> syncItem in objects)
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
                writer.Write((byte)change.operation);

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
                K key = DeserializeKey(reader);
                V obj = DeserializeItem(reader);
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
                K key = default;
                V item = default;

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

        public bool ContainsKey(K key) => objects.ContainsKey(key);

        public bool Remove(K key)
        {
            if (objects.TryGetValue(key, out V item) && objects.Remove(key))
            {
                AddOperation(Operation.OP_REMOVE, key, item);
                return true;
            }
            return false;
        }

        public void Dirty(K index)
        {
            AddOperation(Operation.OP_DIRTY, index, objects[index]);
        }

        public V this[K i]
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

        public bool TryGetValue(K key, out V value) => objects.TryGetValue(key, out value);

        public void Add(K key, V value)
        {
            objects.Add(key, value);
            AddOperation(Operation.OP_ADD, key, value);
        }

        public void Add(KeyValuePair<K, V> item) => Add(item.Key, item.Value);

        public bool Contains(KeyValuePair<K, V> item)
        {
            return TryGetValue(item.Key, out V val) && EqualityComparer<V>.Default.Equals(val, item.Value);
        }

        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
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
            foreach (KeyValuePair<K,V> item in objects)
            {
                array[i] = item;
                i++;
            }
        }

        public bool Remove(KeyValuePair<K, V> item)
        {
            bool result = objects.Remove(item.Key);
            if (result)
            {
                AddOperation(Operation.OP_REMOVE, item.Key, item.Value);
            }
            return result;
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => ((IDictionary<K, V>)objects).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IDictionary<K, V>)objects).GetEnumerator();
    }
}
