using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace Mirror
{
    public sealed class SyncListString : SyncList<string>
    {
        protected override void SerializeItem(NetworkWriter writer, string item)
        {
            writer.Write(item);
        }

        protected override string DeserializeItem(NetworkReader reader)
        {
            return reader.ReadString();
        }

    }

    public sealed class SyncListFloat : SyncList<float>
    {
        protected override void SerializeItem(NetworkWriter writer, float item)
        {
            writer.Write(item);
        }

        protected override float DeserializeItem(NetworkReader reader)
        {
            return reader.ReadSingle();
        }

    }

    public class SyncListInt : SyncList<int>
    {
        protected override void SerializeItem(NetworkWriter writer, int item)
        {
            writer.WritePackedUInt32((uint)item);
        }

        protected override int DeserializeItem(NetworkReader reader)
        {
            return (int)reader.ReadPackedUInt32();
        }

    }

    public class SyncListUInt : SyncList<uint>
    {
        protected override void SerializeItem(NetworkWriter writer, uint item)
        {
            writer.WritePackedUInt32(item);
        }

        protected override uint DeserializeItem(NetworkReader reader)
        {
            return reader.ReadPackedUInt32();
        }

    }

    public class SyncListBool : SyncList<bool>
    {
        protected override void SerializeItem(NetworkWriter writer, bool item)
        {
            writer.Write(item);
        }

        protected override bool DeserializeItem(NetworkReader reader)
        {
            return reader.ReadBoolean();
        }
    }


    // Original UNET name is SyncListStruct and original Weaver weavers anything
    // that contains the name 'SyncListStruct', without considering the name-
    // space.
    //
    // In other words, we need another name until the original Weaver is removed
    // in Unity 2019.1.
    //
    // TODO rename back to SyncListStruct after 2019.1!
    public class SyncListSTRUCT<T> : SyncList<T> where T : struct
    {
        protected override void SerializeItem(NetworkWriter writer, T item)
        {
        }

        protected override T DeserializeItem(NetworkReader reader)
        {
            return new T();
        }

        public T GetItem(int i)
        {
            return base[i];
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class SyncList<T> : IList<T>, SyncObject
    {
        public delegate void SyncListChanged(Operation op, int itemIndex);

        readonly List<T> m_Objects = new List<T>();

        public int Count { get { return m_Objects.Count; } }
        public bool IsReadOnly { get { return false; } }
        public SyncListChanged Callback { get { return m_Callback; } set { m_Callback = value; } }

        public enum Operation : byte
        {
            OP_ADD,
            OP_CLEAR,
            OP_INSERT,
            OP_REMOVE,
            OP_REMOVEAT,
            OP_SET,
            OP_DIRTY
        };

        struct Change
        {
            internal Operation operation;
            internal int index;
            internal T item;
        }

        readonly List<Change> Changes = new List<Change>();
        // how many changes we need to ignore
        // this is needed because when we initialize the list,  
        // we might later receive changes that have already been applied
        // so we need to skip them
        int changesAhead = 0;

        INetworkBehaviour m_Behaviour;
        SyncListChanged m_Callback;


        protected abstract void SerializeItem(NetworkWriter writer, T item);
        protected abstract T DeserializeItem(NetworkReader reader);

        public void InitializeBehaviour(INetworkBehaviour beh)
        {
            m_Behaviour = beh;
        }

        public bool IsDirty 
        {
            get 
            {
                return Changes.Count > 0;
            }
        }

        // throw away all the changes
        // this should be called after a successfull sync
        public void Flush()
        {
            Changes.Clear();
        }

        void AddOperation(Operation op, int itemIndex, T item)
        {
            if (m_Behaviour == null)
            {
                if (LogFilter.logError) { Debug.LogError("SyncList not initialized"); }
                return;
            }

            Change change = new Change
            {
                operation = op,
                index = itemIndex,
                item = item
            };

            if (m_Behaviour.isServer)
            {
                // no need to track changes if this is not a server object
                Changes.Add(change);
            }

            // ensure it is invoked on host
            if (m_Behaviour.isServer && m_Behaviour.isClient && m_Callback != null)
            {
                m_Callback.Invoke(op, itemIndex);
            }
        }

        void AddOperation(Operation op, int itemIndex)
        {
            AddOperation(op, itemIndex, default(T));
        }

        public void OnSerializeAll(NetworkWriter writer)
        {
            // if init,  write the full list content
            writer.Write(m_Objects.Count);

            for (int i = 0; i < m_Objects.Count; i++)
            {
                T obj = m_Objects[i];
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

                    case Operation.OP_INSERT:
                        writer.Write(change.index);
                        SerializeItem(writer, change.item);
                        break;

                    case Operation.OP_REMOVE:
                        SerializeItem(writer, change.item);
                        break;

                    case Operation.OP_REMOVEAT:
                        writer.Write(change.index);
                        break;

                    case Operation.OP_SET:
                    case Operation.OP_DIRTY:
                        writer.Write(change.index);
                        SerializeItem(writer, change.item);
                        break;
                }
            }

        }

        public void OnDeserializeAll(NetworkReader reader)
        {
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
            int changesCount = reader.ReadInt32();

            for (int i = 0; i < changesCount; i++)
            {
                Operation operation = (Operation)reader.ReadByte();

                // apply the operation only if it is a new change
                // that we have not applied yet
                bool apply = changesAhead == 0;
                int index = 0;
                T item;

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

                    case Operation.OP_INSERT:
                        index = reader.ReadInt32();
                        item = DeserializeItem(reader);
                        if (apply)
                        {
                            m_Objects.Insert(index, item);
                        }
                        break;

                    case Operation.OP_REMOVE:
                        item = DeserializeItem(reader);
                        if (apply)
                        {
                            m_Objects.Remove(item);
                        }
                        break;

                    case Operation.OP_REMOVEAT:
                        index = reader.ReadInt32();
                        if (apply)
                        {
                            m_Objects.RemoveAt(index);
                        }
                        break;

                    case Operation.OP_SET:
                    case Operation.OP_DIRTY:
                        index = reader.ReadInt32();
                        item = DeserializeItem(reader);
                        if (apply)
                        {
                            m_Objects[index] = item;
                        }
                        break;
                }

                if (m_Callback != null && apply)
                {
                    m_Callback.Invoke(operation, index);
                }

                // we just skipped this change
                if (!apply)
                {
                    changesAhead--;
                }
            }
        }

        // used to bypass Add message.
        internal void AddInternal(T item)
        {
            m_Objects.Add(item);
        }

        public void Add(T item)
        {
            m_Objects.Add(item);
            AddOperation(Operation.OP_ADD, m_Objects.Count - 1, item);
        }

        public void Clear()
        {
            m_Objects.Clear();
            AddOperation(Operation.OP_CLEAR, 0);
        }

        public bool Contains(T item)
        {
            return m_Objects.Contains(item);
        }

        public void CopyTo(T[] array, int index)
        {
            m_Objects.CopyTo(array, index);
        }

        public int IndexOf(T item)
        {
            return m_Objects.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            m_Objects.Insert(index, item);
            AddOperation(Operation.OP_INSERT, index, item);
        }

        public bool Remove(T item)
        {
            var result = m_Objects.Remove(item);
            if (result)
            {
                AddOperation(Operation.OP_REMOVE, 0, item);
            }
            return result;
        }

        public void RemoveAt(int index)
        {
            m_Objects.RemoveAt(index);
            AddOperation(Operation.OP_REMOVEAT, index);
        }

        public void Dirty(int index)
        {
            AddOperation(Operation.OP_DIRTY, index, m_Objects[index]);
        }

        public T this[int i]
        {
            get { return m_Objects[i]; }
            set
            {
                bool changed = false;
                if (m_Objects[i] == null)
                {
                    if (value == null)
                        return;
                    else
                        changed = true;
                }
                else
                {
                    changed = !m_Objects[i].Equals(value);
                }

                m_Objects[i] = value;
                if (changed)
                {
                    AddOperation(Operation.OP_SET, i, value);
                }
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return m_Objects.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
