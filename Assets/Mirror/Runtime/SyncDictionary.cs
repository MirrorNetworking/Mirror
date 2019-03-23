using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace Mirror
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class SyncDictionary<B, T> : IDictionary<B, T>, SyncObject
    {
        public delegate void SyncDictionaryChanged(Operation op, B key, T item);

        readonly Dictionary<B, T> m_Objects;

        public int Count => m_Objects.Count;
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
            internal B key;
            internal T item;
        }

        readonly List<Change> Changes = new List<Change>();
        // how many changes we need to ignore
        // this is needed because when we initialize the list,
        // we might later receive changes that have already been applied
        // so we need to skip them
        int changesAhead = 0;

        protected virtual void SerializeKey(NetworkWriter writer, B item) {}
        protected virtual void SerializeItem(NetworkWriter writer, T item) {}
        protected virtual B DeserializeKey(NetworkReader reader) => default;
        protected virtual T DeserializeItem(NetworkReader reader) => default;

        public bool IsDirty => Changes.Count > 0;

        public ICollection<B> Keys => m_Objects.Keys;

        public ICollection<T> Values => m_Objects.Values;

        // throw away all the changes
        // this should be called after a successfull sync
        public void Flush() => Changes.Clear();

        public SyncDictionary()
        {
            m_Objects = new Dictionary<B, T>();
        }

        public SyncDictionary(IEqualityComparer<B> eq)
        {
            m_Objects = new Dictionary<B, T>(eq);
        }

        void AddOperation(Operation op, B key, T item)
        {
            if (IsReadOnly)
            {
                throw new System.InvalidOperationException("Synclists can only be modified at the server");
            }

            Change change = new Change
            {
                operation = op,
                key = key,
                item = item
            };

            Changes.Add(change);

            Callback?.Invoke(op, key, item);
        }

        public void OnSerializeAll(NetworkWriter writer)
        {
            // if init,  write the full list content
            writer.WritePackedUInt32((uint)m_Objects.Count);

            foreach (KeyValuePair<B, T> syncItem in m_Objects)
            {
                SerializeKey(writer, syncItem.Key);
                SerializeItem(writer, syncItem.Value);
            }

            // all changes have been applied already
            // thus the client will need to skip all the pending changes
            // or they would be applied again.
            // So we write how many changes are pending
            writer.WritePackedUInt32((uint)Changes.Count);
        }

        public void OnSerializeDelta(NetworkWriter writer)
        {
            // write all the queued up changes
            writer.WritePackedUInt32((uint)Changes.Count);

            for (int i = 0; i < Changes.Count; i++)
            {
                Change change = Changes[i];
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

            m_Objects.Clear();
            Changes.Clear();

            for (int i = 0; i < count; i++)
            {
                B key = DeserializeKey(reader);
                T obj = DeserializeItem(reader);
                m_Objects.Add(key, obj);
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
                B key = default;
                T item = default;

                switch (operation)
                {
                    case Operation.OP_ADD:
                    case Operation.OP_SET:
                    case Operation.OP_DIRTY:
                        key = DeserializeKey(reader);
                        item = DeserializeItem(reader);
                        if (apply)
                        {
                            m_Objects[key] = item;
                        }
                        break;

                    case Operation.OP_CLEAR:
                        if (apply)
                        {
                            m_Objects.Clear();
                        }
                        break;

                    case Operation.OP_REMOVE:
                        key = DeserializeKey(reader);
                        item = DeserializeItem(reader);
                        if (apply)
                        {
                            m_Objects.Remove(key);
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
            m_Objects.Clear();
            AddOperation(Operation.OP_CLEAR, default, default);
        }

        public bool ContainsKey(B key) => m_Objects.ContainsKey(key);

        public bool Remove(B key)
        {
            if (m_Objects.TryGetValue(key, out T item) && m_Objects.Remove(key))
            {
                AddOperation(Operation.OP_REMOVE, key, item);
                return true;
            }
            return false;
        }

        public void Dirty(B index)
        {
            AddOperation(Operation.OP_DIRTY, index, m_Objects[index]);
        }

        public T this[B i]
        {
            get => m_Objects[i];
            set
            {
                bool existing = TryGetValue(i, out T val);
                if (existing)
                {
                    m_Objects[i] = value;
                    AddOperation(Operation.OP_SET, i, value);
                }
                else
                {
                    m_Objects[i] = value;
                    AddOperation(Operation.OP_ADD, i, value);
                }
            }
        }

        public bool TryGetValue(B key, out T value) => m_Objects.TryGetValue(key, out value);

        public void Add(B key, T value)
        {
            m_Objects.Add(key, value);
            AddOperation(Operation.OP_ADD, key, value);
        }

        public void Add(KeyValuePair<B, T> item)
        {
            m_Objects.Add(item.Key, item.Value);
            AddOperation(Operation.OP_ADD, item.Key, item.Value);
        }

        public bool Contains(KeyValuePair<B, T> item)
        {
            return TryGetValue(item.Key, out T val) && EqualityComparer<T>.Default.Equals(val, item.Value);
        }

        public void CopyTo(KeyValuePair<B, T>[] array, int arrayIndex)
        {
            int i = 0;
            foreach (KeyValuePair<B, T> item in m_Objects)
            {
                array[i] = item;
                i++;
            }
        }

        public bool Remove(KeyValuePair<B, T> item)
        {
            bool result = m_Objects.Remove(item.Key);
            if (result)
            {
                AddOperation(Operation.OP_REMOVE, item.Key, item.Value);
            }
            return result;
        }

        public IEnumerator<KeyValuePair<B, T>> GetEnumerator() => ((IDictionary<B, T>)m_Objects).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IDictionary<B, T>)m_Objects).GetEnumerator();
    }
}
