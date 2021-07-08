using System;
using System.Collections;
using System.Collections.Generic;

namespace Mirror
{
    // Deprecated 2020-10-02
    [Obsolete("Use SyncList<string> instead")]
    public class SyncListString : SyncList<string> {}

    // Deprecated 2020-10-02
    [Obsolete("Use SyncList<float> instead")]
    public class SyncListFloat : SyncList<float> {}

    // Deprecated 2020-10-02
    [Obsolete("Use SyncList<int> instead")]
    public class SyncListInt : SyncList<int> {}

    // Deprecated 2020-10-02
    [Obsolete("Use SyncList<uint> instead")]
    public class SyncListUInt : SyncList<uint> {}

    // Deprecated 2020-10-02
    [Obsolete("Use SyncList<bool> instead")]
    public class SyncListBool : SyncList<bool> {}

    public class SyncList<T> : IList<T>, IReadOnlyList<T>, SyncObject
    {
        public delegate void SyncListChanged(Operation op, int itemIndex, T oldItem, T newItem);

        enum Mode : byte
        {
            NetworkIdentity,
            GameObject,
            NetworkBehaviour,
            Normal
        }

        readonly Mode mode;

        // server always uses this for all operations
        readonly IList<T> objects;

        readonly IEqualityComparer<T> comparer;

        // used on clients to store netIds if T is NetworkIdentity or GameObject
        IList<uint> netIds;

        // used on clients to store netIds and componentIndexes tif T is NetworkBehaviour
        IList<NetworkBehaviourCache> components;

        public int Count
        {
            get
            {
                if ((netIds == null && components == null) || mode == Mode.Normal)
                    return objects.Count;
                if (mode == Mode.NetworkIdentity || mode == Mode.GameObject)
                    return netIds.Count;
                if (mode == Mode.NetworkBehaviour)
                    return components.Count;
                return 0;
            }
        }
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

        readonly List<Change> changes = new List<Change>();
        // how many changes we need to ignore
        // this is needed because when we initialize the list,
        // we might later receive changes that have already been applied
        // so we need to skip them
        int changesAhead;

        public SyncList() : this(EqualityComparer<T>.Default) {}

        public SyncList(IEqualityComparer<T> comparer)
        {
            this.comparer = comparer ?? EqualityComparer<T>.Default;
            objects = new List<T>();
            if (typeof(T) == typeof(NetworkIdentity))
            {
                mode = Mode.NetworkIdentity;
            }
            else if (typeof(T) == typeof(UnityEngine.GameObject))
            {
                mode = Mode.GameObject;
            }
            else if (typeof(NetworkBehaviour).IsAssignableFrom(typeof(T)))
            {
                mode = Mode.NetworkBehaviour;
            }
            else
            {
                mode = Mode.Normal;
            }
        }

        public SyncList(IList<T> objects, IEqualityComparer<T> comparer = null)
        {
            this.comparer = comparer ?? EqualityComparer<T>.Default;
            this.objects = objects;
            if (typeof(T) == typeof(NetworkIdentity))
            {
                mode = Mode.NetworkIdentity;
            }
            else if (typeof(T) == typeof(UnityEngine.GameObject))
            {
                mode = Mode.GameObject;
            }
            else if (typeof(NetworkBehaviour).IsAssignableFrom(typeof(T)))
            {
                mode = Mode.NetworkBehaviour;
            }
            else
            {
                mode = Mode.Normal;
            }
        }

        public bool IsDirty => changes.Count > 0;

        // throw away all the changes
        // this should be called after a successful sync
        public void Flush() => changes.Clear();

        public void Reset()
        {
            IsReadOnly = false;
            changes.Clear();
            changesAhead = 0;
            objects.Clear();
        }

        void AddOperation(Operation op, int itemIndex, T oldItem, T newItem)
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException("Synclists can only be modified at the server");
            }

            Change change = new Change
            {
                operation = op,
                index = itemIndex,
                item = newItem
            };

            changes.Add(change);

            Callback?.Invoke(op, itemIndex, oldItem, newItem);
        }

        void WriteObject(NetworkWriter writer, T obj)
        {
            switch (mode)
            {
                case Mode.NetworkIdentity:
                    var identity = obj as NetworkIdentity;
                    if (identity == null)
                    {
                        writer.WriteUInt(0);
                        return;
                    }
                    writer.WriteUInt(identity.netId);
                    break;
                case Mode.GameObject:
                    var gobj = obj as UnityEngine.GameObject;
                    if (gobj == null)
                    {
                        writer.WriteUInt(0);
                        return;
                    }
                    if (!gobj.TryGetComponent(out NetworkIdentity gobjIdentity))
                    {
                        UnityEngine.Debug.LogWarning("SyncList " + gobj + " has no NetworkIdentity");
                        writer.WriteUInt(0);
                        return;
                    }
                    writer.WriteUInt(gobjIdentity.netId);
                    break;
                case Mode.NetworkBehaviour:
                    var nb = obj as NetworkBehaviour;
                    writer.Write(new NetworkBehaviourCache(nb));
                    break;
                case Mode.Normal:
                    writer.Write(obj);
                    break;
            }
        }

        public void OnSerializeAll(NetworkWriter writer)
        {
            // if init,  write the full list content
            writer.WriteUInt((uint)objects.Count);

            for (int i = 0; i < objects.Count; i++)
            {
                T obj = objects[i];
                WriteObject(writer, obj);
            }

            // all changes have been applied already
            // thus the client will need to skip all the pending changes
            // or they would be applied again.
            // So we write how many changes are pending
            writer.WriteUInt((uint)changes.Count);
        }

        public void OnSerializeDelta(NetworkWriter writer)
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
                        WriteObject(writer, change.item);
                        break;

                    case Operation.OP_CLEAR:
                        break;

                    case Operation.OP_REMOVEAT:
                        writer.WriteUInt((uint)change.index);
                        break;

                    case Operation.OP_INSERT:
                    case Operation.OP_SET:
                        writer.WriteUInt((uint)change.index);
                        WriteObject(writer, change.item);
                        break;
                }
            }
        }

        void InitCache()
        {
            if (mode == Mode.NetworkIdentity ||
                mode == Mode.GameObject)
            {
                if (netIds == null)
                    netIds = new List<uint>();
            }
            else if (mode == Mode.NetworkBehaviour)
            {
                // for NetworkBehaviours netId and componentIndex is received
                if (components == null)
                    components = new List<NetworkBehaviourCache>();
            }
        }

        public void OnDeserializeAll(NetworkReader reader)
        {
            // This list can now only be modified by synchronization
            IsReadOnly = true;

            // If needed, prepare cache collections
            InitCache();
            
            // if init,  write the full list content
            int count = (int)reader.ReadUInt();

            objects.Clear();
            changes.Clear();

            if (mode == Mode.NetworkIdentity ||
                mode == Mode.GameObject)
            {
                // for NetworkIdentity and GameObject, netId is received
                netIds.Clear();
                for (int i = 0; i < count; i++)
                {
                    var netId = reader.ReadUInt();
                    netIds.Add(netId);
                }
            }
            else if (mode == Mode.NetworkBehaviour)
            {
                components.Clear();
                for (int i = 0; i < count; i++)
                {
                    var comp = reader.Read<NetworkBehaviourCache>();
                    components.Add(comp);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    T obj = reader.Read<T>();
                    objects.Add(obj);
                }
            }
            
            // We will need to skip all these changes
            // the next time the list is synchronized
            // because they have already been applied
            changesAhead = (int)reader.ReadUInt();
        }

        private enum ReadObjectMode { Add, Insert, Set }

        void ReadObject(NetworkReader reader, ReadObjectMode readMode, bool apply, ref T oldItem, ref T newItem, ref int index)
        {
            switch (readMode)
            {
                case ReadObjectMode.Add:
                    index = objects.Count;
                    break;
                case ReadObjectMode.Insert:
                    index = (int)reader.ReadUInt();
                    break;
                case ReadObjectMode.Set:
                    index = (int)reader.ReadUInt();
                    if (apply)
                    {
                        if (mode == Mode.NetworkIdentity)
                        {
                            NetworkIdentity.spawned.TryGetValue(netIds[index], out NetworkIdentity identity);
                            oldItem = (T)(identity as object);
                        }
                        else if (mode == Mode.GameObject)
                        {
                            if (!NetworkIdentity.spawned.TryGetValue(netIds[index], out NetworkIdentity identity))
                            {
                                oldItem = default;
                            }
                            else
                            {
                                oldItem = (T)(identity.gameObject as object);
                            }
                        }
                        else if (mode == Mode.NetworkBehaviour)
                        {
                            oldItem = (T)(components[index].Get() as object);
                        }
                        else
                        {
                            oldItem = objects[index];
                        }
                    }
                    break;
            }

            if (mode == Mode.NetworkIdentity)
            {
                var netId = reader.ReadUInt();
                NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity);
                newItem = (T)(identity as object); // no class constraint workaround
                if (apply)
                {
                    switch (readMode)
                    {
                        case ReadObjectMode.Add:
                            netIds.Add(netId);
                            break;
                        case ReadObjectMode.Insert:
                            netIds.Insert(index, netId);
                            break;
                        case ReadObjectMode.Set:
                            netIds[index] = netId;
                            break;
                    }
                }
            }
            else if (mode == Mode.GameObject)
            {
                var netId = reader.ReadUInt();
                if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity))
                {
                    newItem = (T)(identity.gameObject as object);
                }
                else
                {
                    newItem = default;
                }
                if (apply)
                {
                    switch (readMode)
                    {
                        case ReadObjectMode.Add:
                            netIds.Add(netId);
                            break;
                        case ReadObjectMode.Insert:
                            netIds.Insert(index, netId);
                            break;
                        case ReadObjectMode.Set:
                            netIds[index] = netId;
                            break;
                    }
                }
            }
            else if (mode == Mode.NetworkBehaviour)
            {
                var newCache = reader.Read<NetworkBehaviourCache>();
                newItem = (T)(newCache.Get() as object); // no T class constraint workaround

                if (apply)
                {
                    switch (readMode)
                    {
                        case ReadObjectMode.Add:
                            components.Add(newCache);
                            break;
                        case ReadObjectMode.Insert:
                            components.Insert(index, newCache);
                            break;
                        case ReadObjectMode.Set:
                            components[index] = newCache;
                            break;
                    }
                }
            }
            else
            {
                newItem = reader.Read<T>();
                if (apply)
                {
                    switch (readMode)
                    {
                        case ReadObjectMode.Add:
                            objects.Add(newItem);
                            break;
                        case ReadObjectMode.Insert:
                            objects.Insert(index, newItem);
                            break;
                        case ReadObjectMode.Set:
                            objects[index] = newItem;
                            break;
                    }
                }
            }
        }

        public void OnDeserializeDelta(NetworkReader reader)
        {
            // This list can now only be modified by synchronization
            IsReadOnly = true;

            // If needed, prepare cache collections
            InitCache();

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

                switch (operation)
                {
                    case Operation.OP_ADD:
                        ReadObject(reader, ReadObjectMode.Add, apply, ref oldItem, ref newItem, ref index);
                        break;

                    case Operation.OP_CLEAR:
                        if (apply)
                        {
                            objects.Clear();
                        }
                        break;

                    case Operation.OP_INSERT:
                        ReadObject(reader, ReadObjectMode.Insert, apply, ref oldItem, ref newItem, ref index);
                        break;

                    case Operation.OP_REMOVEAT:
                        index = (int)reader.ReadUInt();
                        if (apply)
                        {
                            oldItem = objects[index];
                            objects.RemoveAt(index);
                        }
                        break;

                    case Operation.OP_SET:
                        ReadObject(reader, ReadObjectMode.Set, apply, ref oldItem, ref newItem, ref index);
                        break;
                }

                if (apply)
                {
                    Callback?.Invoke(operation, index, oldItem, newItem);
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
            AddOperation(Operation.OP_ADD, objects.Count - 1, default, item);
        }

        public void AddRange(IEnumerable<T> range)
        {
            foreach (T entry in range)
            {
                Add(entry);
            }
        }

        public void Clear()
        {
            objects.Clear();
            AddOperation(Operation.OP_CLEAR, 0, default, default);
        }

        public bool Contains(T item) => IndexOf(item) >= 0;

        public void CopyTo(T[] array, int index)
        {
            if (mode == Mode.Normal || (netIds == null && components == null))
            {
                objects.CopyTo(array, index);
                return;
            }

            int i = index;

            foreach (var e in this)
            {
                if (i >= array.Length) return;
                array[i] = e;
                i++;
            }
        }

        public int IndexOf(T item)
        {
            for (int i = 0; i < this.Count; ++i)
                if (comparer.Equals(item, this[i]))
                    return i;
            return -1;
        }

        public int FindIndex(Predicate<T> match)
        {
            for (int i = 0; i < this.Count; ++i)
                if (match(this[i]))
                    return i;
            return -1;
        }

        public T Find(Predicate<T> match)
        {
            int i = FindIndex(match);
            return (i != -1) ? this[i] : default;
        }

        public List<T> FindAll(Predicate<T> match)
        {
            List<T> results = new List<T>();
            for (int i = 0; i < this.Count; ++i)
                if (match(this[i]))
                    results.Add(this[i]);
            return results;
        }

        public void Insert(int index, T item)
        {
            objects.Insert(index, item);
            AddOperation(Operation.OP_INSERT, index, default, item);
        }

        public void InsertRange(int index, IEnumerable<T> range)
        {
            foreach (T entry in range)
            {
                Insert(index, entry);
                index++;
            }
        }

        public bool Remove(T item)
        {
            int index = IndexOf(item);
            bool result = index >= 0;
            if (result)
            {
                RemoveAt(index);
            }
            return result;
        }

        public void RemoveAt(int index)
        {
            T oldItem = objects[index];
            objects.RemoveAt(index);
            AddOperation(Operation.OP_REMOVEAT, index, oldItem, default);
        }

        public int RemoveAll(Predicate<T> match)
        {
            List<T> toRemove = new List<T>();
            for (int i = 0; i < objects.Count; ++i)
                if (match(objects[i]))
                    toRemove.Add(objects[i]);

            foreach (T entry in toRemove)
            {
                Remove(entry);
            }

            return toRemove.Count;
        }

        public T this[int i]
        {
            get
            {
                if (netIds == null && components == null) // server only uses objects
                    return objects[i];

                switch (mode)
                {
                    case Mode.NetworkIdentity:
                        NetworkIdentity.spawned.TryGetValue(netIds[i], out NetworkIdentity identity);
                        return (T)(identity as object);
                    case Mode.GameObject:
                        if (!NetworkIdentity.spawned.TryGetValue(netIds[i], out NetworkIdentity gobjIdentity))
                        {
                            return default;
                        }
                        return (T)(gobjIdentity.gameObject as object);
                    case Mode.NetworkBehaviour:
                        return (T)(components[i].Get() as object);
                }

                return objects[i];
            }
            set
            {
                if (!comparer.Equals(this[i], value))
                {
                    T oldItem = this[i];
                    objects[i] = value;
                    AddOperation(Operation.OP_SET, i, oldItem, value);
                }
            }
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        // default Enumerator allocates. we need a custom struct Enumerator to
        // not allocate on the heap.
        // (System.Collections.Generic.List<T> source code does the same)
        //
        // benchmark:
        //   uMMORPG with 800 monsters, Skills.GetHealthBonus() which runs a
        //   foreach on skills SyncList:
        //      before: 81.2KB GC per frame
        //      after:     0KB GC per frame
        // => this is extremely important for MMO scale networking
        public struct Enumerator : IEnumerator<T>
        {
            readonly SyncList<T> list;
            int index;
            public T Current { get; private set; }

            public Enumerator(SyncList<T> list)
            {
                this.list = list;
                index = -1;
                Current = default;
            }

            public bool MoveNext()
            {
                if (++index >= list.Count)
                {
                    return false;
                }
                Current = list[index];
                return true;
            }

            public void Reset() => index = -1;
            object IEnumerator.Current => Current;
            public void Dispose() {}
        }
    }
}
