#if ENABLE_UNET
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace UnityEngine.Networking
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

        [System.Obsolete("ReadReference is now used instead")]
        static public SyncListString ReadInstance(NetworkReader reader)
        {
            ushort count = reader.ReadUInt16();
            var result = new SyncListString();
            for (ushort i = 0; i < count; i++)
            {
                result.AddInternal(reader.ReadString());
            }
            return result;
        }

        static public void ReadReference(NetworkReader reader, SyncListString syncList)
        {
            ushort count = reader.ReadUInt16();
            syncList.Clear();
            for (ushort i = 0; i < count; i++)
            {
                syncList.AddInternal(reader.ReadString());
            }
        }

        static public void WriteInstance(NetworkWriter writer, SyncListString items)
        {
            writer.Write((ushort)items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                writer.Write(items[i]);
            }
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

        [System.Obsolete("ReadReference is now used instead")]
        static public SyncListFloat ReadInstance(NetworkReader reader)
        {
            ushort count = reader.ReadUInt16();
            var result = new SyncListFloat();
            for (ushort i = 0; i < count; i++)
            {
                result.AddInternal(reader.ReadSingle());
            }
            return result;
        }

        static public void ReadReference(NetworkReader reader, SyncListFloat syncList)
        {
            ushort count = reader.ReadUInt16();
            syncList.Clear();
            for (ushort i = 0; i < count; i++)
            {
                syncList.AddInternal(reader.ReadSingle());
            }
        }

        static public void WriteInstance(NetworkWriter writer, SyncListFloat items)
        {
            writer.Write((ushort)items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                writer.Write(items[i]);
            }
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

        [System.Obsolete("ReadReference is now used instead")]
        static public SyncListInt ReadInstance(NetworkReader reader)
        {
            ushort count = reader.ReadUInt16();
            var result = new SyncListInt();
            for (ushort i = 0; i < count; i++)
            {
                result.AddInternal((int)reader.ReadPackedUInt32());
            }
            return result;
        }

        static public void ReadReference(NetworkReader reader, SyncListInt syncList)
        {
            ushort count = reader.ReadUInt16();
            syncList.Clear();
            for (ushort i = 0; i < count; i++)
            {
                syncList.AddInternal((int)reader.ReadPackedUInt32());
            }
        }

        static public void WriteInstance(NetworkWriter writer, SyncListInt items)
        {
            writer.Write((ushort)items.Count);

            for (int i = 0; i < items.Count; i++)
            {
                writer.WritePackedUInt32((uint)items[i]);
            }
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

        [System.Obsolete("ReadReference is now used instead")]
        static public SyncListUInt ReadInstance(NetworkReader reader)
        {
            ushort count = reader.ReadUInt16();
            var result = new SyncListUInt();
            for (ushort i = 0; i < count; i++)
            {
                result.AddInternal(reader.ReadPackedUInt32());
            }
            return result;
        }

        static public void ReadReference(NetworkReader reader, SyncListUInt syncList)
        {
            ushort count = reader.ReadUInt16();
            syncList.Clear();
            for (ushort i = 0; i < count; i++)
            {
                syncList.AddInternal(reader.ReadPackedUInt32());
            }
        }

        static public void WriteInstance(NetworkWriter writer, SyncListUInt items)
        {
            writer.Write((ushort)items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                writer.WritePackedUInt32(items[i]);
            }
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

        [System.Obsolete("ReadReference is now used instead")]
        static public SyncListBool ReadInstance(NetworkReader reader)
        {
            ushort count = reader.ReadUInt16();
            var result = new SyncListBool();
            for (ushort i = 0; i < count; i++)
            {
                result.AddInternal(reader.ReadBoolean());
            }
            return result;
        }

        static public void ReadReference(NetworkReader reader, SyncListBool syncList)
        {
            ushort count = reader.ReadUInt16();
            syncList.Clear();
            for (ushort i = 0; i < count; i++)
            {
                syncList.AddInternal(reader.ReadBoolean());
            }
        }

        static public void WriteInstance(NetworkWriter writer, SyncListBool items)
        {
            writer.Write((ushort)items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                writer.Write(items[i]);
            }
        }
    }


    public class SyncListStruct<T> : SyncList<T> where T : struct
    {
        new public void AddInternal(T item)
        {
            base.AddInternal(item);
        }

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

        new public ushort Count { get { return (ushort)base.Count; } }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    abstract public class SyncList<T> : IList<T>
    {
        public delegate void SyncListChanged(Operation op, int itemIndex);

        List<T> m_Objects = new List<T>();

        public int Count { get { return m_Objects.Count; } }
        public bool IsReadOnly { get { return false; } }
        public SyncListChanged Callback { get { return m_Callback; } set { m_Callback = value; } }

        public enum Operation
        {
            OP_ADD,
            OP_CLEAR,
            OP_INSERT,
            OP_REMOVE,
            OP_REMOVEAT,
            OP_SET,
            OP_DIRTY
        };

        NetworkBehaviour m_Behaviour;
        int m_CmdHash;
        SyncListChanged m_Callback;

        abstract protected void SerializeItem(NetworkWriter writer, T item);
        abstract protected T DeserializeItem(NetworkReader reader);


        public void InitializeBehaviour(NetworkBehaviour beh, int cmdHash)
        {
            m_Behaviour = beh;
            m_CmdHash = cmdHash;
        }

        void SendMsg(Operation op, int itemIndex, T item)
        {
            if (m_Behaviour == null)
            {
                if (LogFilter.logError) { Debug.LogError("SyncList not initialized"); }
                return;
            }

            var uv = m_Behaviour.GetComponent<NetworkIdentity>();
            if (uv == null)
            {
                if (LogFilter.logError) { Debug.LogError("SyncList no NetworkIdentity"); }
                return;
            }

            if (!uv.isServer)
            {
                // object is not spawned yet, so no need to send updates.
                return;
            }

            NetworkWriter writer = new NetworkWriter();
            writer.StartMessage(MsgType.SyncList);
            writer.Write(uv.netId);
            writer.WritePackedUInt32((uint)m_CmdHash);
            writer.Write((byte)op);
            writer.WritePackedUInt32((uint)itemIndex);
            SerializeItem(writer, item);
            writer.FinishMessage();

            NetworkServer.SendWriterToReady(uv.gameObject, writer, m_Behaviour.GetNetworkChannel());

#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                MsgType.SyncList, op.ToString(), 1);
#endif

            // ensure it is invoked on host
            if (m_Behaviour.isServer && m_Behaviour.isClient && m_Callback != null)
            {
                m_Callback.Invoke(op, itemIndex);
            }
        }

        void SendMsg(Operation op, int itemIndex)
        {
            SendMsg(op, itemIndex, default(T));
        }

        public void HandleMsg(NetworkReader reader)
        {
            byte op = reader.ReadByte();
            int itemIndex = (int)reader.ReadPackedUInt32();
            T item = DeserializeItem(reader);

            switch ((Operation)op)
            {
                case Operation.OP_ADD:
                    m_Objects.Add(item);
                    break;

                case Operation.OP_CLEAR:
                    m_Objects.Clear();
                    break;

                case Operation.OP_INSERT:
                    m_Objects.Insert(itemIndex, item);
                    break;

                case Operation.OP_REMOVE:
                    m_Objects.Remove(item);
                    break;

                case Operation.OP_REMOVEAT:
                    m_Objects.RemoveAt(itemIndex);
                    break;

                case Operation.OP_SET:
                case Operation.OP_DIRTY:
                    m_Objects[itemIndex] = item;
                    break;
            }
            if (m_Callback != null)
            {
                m_Callback.Invoke((Operation)op, itemIndex);
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
            SendMsg(Operation.OP_ADD, m_Objects.Count - 1, item);
        }

        public void Clear()
        {
            m_Objects.Clear();
            SendMsg(Operation.OP_CLEAR, 0);
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
            SendMsg(Operation.OP_INSERT, index, item);
        }

        public bool Remove(T item)
        {
            var result = m_Objects.Remove(item);
            if (result)
            {
                SendMsg(Operation.OP_REMOVE, 0, item);
            }
            return result;
        }

        public void RemoveAt(int index)
        {
            m_Objects.RemoveAt(index);
            SendMsg(Operation.OP_REMOVEAT, index);
        }

        public void Dirty(int index)
        {
            SendMsg(Operation.OP_DIRTY, index, m_Objects[index]);
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
                    SendMsg(Operation.OP_SET, i, value);
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
#endif //ENABLE_UNET
