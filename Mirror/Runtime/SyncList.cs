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

        INetworkBehaviour m_Behaviour;
        SyncListChanged m_Callback;


        protected abstract void SerializeItem(NetworkWriter writer, T item);
        protected abstract T DeserializeItem(NetworkReader reader);

        public void InitializeBehaviour(INetworkBehaviour beh)
        {
            m_Behaviour = beh;
        }

        public bool IsDirty { get; set; }

        void AddOperation(Operation op, int itemIndex, T item)
        {
            if (m_Behaviour == null)
            {
                if (LogFilter.logError) { Debug.LogError("SyncList not initialized"); }
                return;
            }

            IsDirty = true;
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

        public void OnSerialize(NetworkWriter writer)
        {
            // if init,  write the full list content
            writer.Write(m_Objects.Count);

            for (int i = 0; i < m_Objects.Count; i++)
            {
                T obj = m_Objects[i];
                SerializeItem(writer, obj);
            }
        }

        public void OnDeserialize(NetworkReader reader)
        {
            // if init,  write the full list content
            int count = reader.ReadInt32();

            m_Objects.Clear();

            for (int i = 0; i < count; i++)
            {
                T obj = DeserializeItem(reader);
                m_Objects.Add(obj);
                if (m_Callback != null)
                {
                    m_Callback.Invoke(Operation.OP_SET, i);
                }
            }
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
