using System.Collections;
using System.Collections.Generic;

namespace Mirror
{
    public class SyncIDictionary<TKey, TValue> : SyncObject, IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    {
        public delegate void SyncDictionaryChanged(Operation op, TKey key, TValue item);

        protected readonly IDictionary<TKey, TValue> objects;

        public int Count => objects.Count;
        public bool IsReadOnly => !IsWritable();
        public event SyncDictionaryChanged Callback;

        public enum Operation : byte
        {
            OP_ADD,
            OP_CLEAR,
            OP_REMOVE,
            OP_SET
        }

        struct Change
        {
            internal Operation operation;
            internal TKey key;
            internal TValue item;
        }

        abstract class ChangesTracker
        {
            protected readonly List<Change> changes = new List<Change>();
            public uint Count => (uint)changes.Count;
            public List<Change> CurrentChanges => changes;
            public virtual void ClearChanges() => changes.Clear();
            public virtual void AddChange(Change changeToAdd) => changes.Add(changeToAdd);
            public virtual void ResetTrackedFields() { }
        }

        // Traditional list of changes
        // -> insert/delete/clear is only ONE change
        // -> changing the same slot 10x causes 10 changes.
        // -> note that this grows until next sync(!)
        // -> may still be useful for some code that relies on knowing about every change that happens, including redundant changes
        class AllChangesTracker : ChangesTracker { /* Nothing to override here */ }

        // Map of changes
        // -> insert/delete/set of any field results in AT MOST one change
        // -> changing the same slot 10x causes 1 change
        // -> clear operation deletes all tracked changes and replaces them with just 1 single tracked change
        // -> Things only get a little trickier with tracking "changes ahead", aka "which changes were previously sent/received via SerializeAll/DeserializeAll when Deltas are being set/received."
        // -> ResetTrackedFields is important for "changes head" as it means the next time any new slot gets changed, it results in at most 1 more change being added.
        class SingleChangePerKeyTracker : ChangesTracker
        {
            readonly Dictionary<TKey, int> changeMap = new Dictionary<TKey, int>();
            int changeCountDuringLastChangeMapReset = 0;

            public override void ClearChanges()
            {
                changeMap.Clear();
                changeCountDuringLastChangeMapReset = 0;
                base.ClearChanges();
            }

            public override void AddChange(Change changeToAdd)
            {
                if(changeToAdd.operation == Operation.OP_CLEAR)
                {
                    changeMap.Clear();
                    if(changeCountDuringLastChangeMapReset == 0)
                    {
                        // ResetTrackedFields was never called so just clear the whole changes list
                        changes.Clear();
                    }
                    if(changes.Count > changeCountDuringLastChangeMapReset)
                    {
                        // Remove everything from the changes list that was added after the last call to ResetTrackedFields
                        int removeCount = changes.Count - (changeCountDuringLastChangeMapReset + 1);
                        changes.RemoveRange(changeCountDuringLastChangeMapReset, removeCount);
                    }
                    base.AddChange(changeToAdd);
                }
                else if(changeMap.TryGetValue(changeToAdd.key, out int changeIndex))
                {
                    // changeMap should never contain an index that does not exist in the changes list
                    // changeMap should always provide the index pointing to the last recorded change in the changes list for the given key
                    changes[changeIndex] = changeToAdd;
                }
                else
                {
                    base.AddChange(changeToAdd);
                    changeMap[changeToAdd.key] = changes.Count - 1;
                }
            }

            public override void ResetTrackedFields()
            {
                // This has to do with the 'changes ahead' system. By clearning changeMap, it means that up to 1 new change per slot
                // this point will get added to the changes list even if the changes list itself already contains a change for said slot.
                changeMap.Clear();
                changeCountDuringLastChangeMapReset = changes.Count;
            }
        }

        readonly ChangesTracker changeTracker;

        // how many changes we need to ignore
        // this is needed because when we initialize the list,
        // we might later receive changes that have already been applied
        // so we need to skip them
        int changesAhead;

        public override void Reset()
        {
            changeTracker.ClearChanges();
            changesAhead = 0;
            objects.Clear();
        }

        public ICollection<TKey> Keys => objects.Keys;

        public ICollection<TValue> Values => objects.Values;

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => objects.Keys;

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => objects.Values;

        // throw away all the changes
        // this should be called after a successful sync
        public override void ClearChanges() => changeTracker.ClearChanges();

        public SyncIDictionary(IDictionary<TKey, TValue> objects, bool syncAllChanges)
        {
            this.objects = objects;

            if (syncAllChanges)
            {
                // Some applications may need to sync all changes in great detail, even if those changes are redundant
                this.changeTracker = new AllChangesTracker();
            }
            else
            {
                // Nearly all of the time, we should just sync 1 change per key to save bandwidth by not sending redundant changes
                this.changeTracker = new SingleChangePerKeyTracker();
            }
        }

        void AddOperation(Operation op, TKey key, TValue item, bool checkAccess)
        {
            if (checkAccess && IsReadOnly)
            {
                throw new System.InvalidOperationException("SyncDictionaries can only be modified by the owner.");
            }

            if (IsRecording())
            {
                changeTracker.AddChange(new Change()
                {
                    operation = op,
                    key = key,
                    item = item
                });
                OnDirty?.Invoke();
            }

            Callback?.Invoke(op, key, item);
        }

        public override void OnSerializeAll(NetworkWriter writer)
        {
            // if init, write the full list content
            writer.WriteUInt((uint)objects.Count);

            foreach (KeyValuePair<TKey, TValue> syncItem in objects)
            {
                writer.Write(syncItem.Key);
                writer.Write(syncItem.Value);
            }

            // all changes have been applied already
            // thus the client will need to skip all the pending changes
            // or they would be applied again.
            // So we write how many changes are pending
            changeTracker.ResetTrackedFields();
            writer.WriteUInt(changeTracker.Count);
        }

        public override void OnSerializeDelta(NetworkWriter writer)
        {
            // write all the queued up changes
            writer.WriteUInt(changeTracker.Count);

            List<Change> changes = changeTracker.CurrentChanges;
            foreach(Change change in changes)
            {
                writer.WriteByte((byte)change.operation);
                switch (change.operation)
                {
                    case Operation.OP_ADD:
                    case Operation.OP_SET:
                        writer.Write(change.key);
                        writer.Write(change.item);
                        break;
                    case Operation.OP_REMOVE:
                        writer.Write(change.key);
                        break;
                    case Operation.OP_CLEAR:
                        break;
                }
            }
        }

        public override void OnDeserializeAll(NetworkReader reader)
        {
            // if init, write the full list content
            int count = (int)reader.ReadUInt();

            objects.Clear();
            changeTracker.ClearChanges();

            for (int i = 0; i < count; i++)
            {
                TKey key = reader.Read<TKey>();
                TValue obj = reader.Read<TValue>();
                objects.Add(key, obj);
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
                // apply the operation only if it is a new change
                // that we have not applied yet
                bool apply = changesAhead == 0;
                TKey key = default;
                TValue item = default;

                Operation operation = (Operation)reader.ReadByte();
                switch (operation)
                {
                    case Operation.OP_ADD:
                    case Operation.OP_SET:
                        key = reader.Read<TKey>();
                        item = reader.Read<TValue>();
                        if (apply)
                        {
                            // add dirty + changes.
                            // ClientToServer needs to set dirty in server OnDeserialize.
                            // no access check: server OnDeserialize can always
                            // write, even for ClientToServer (for broadcasting).
                            if (ContainsKey(key))
                            {
                                objects[key] = item; // assign after ContainsKey check
                                AddOperation(Operation.OP_SET, key, item, false);
                            }
                            else
                            {
                                objects[key] = item; // assign after ContainsKey check
                                AddOperation(Operation.OP_ADD, key, item, false);
                            }
                        }
                        break;

                    case Operation.OP_CLEAR:
                        if (apply)
                        {
                            objects.Clear();
                            // add dirty + changes.
                            // ClientToServer needs to set dirty in server OnDeserialize.
                            // no access check: server OnDeserialize can always
                            // write, even for ClientToServer (for broadcasting).
                            AddOperation(Operation.OP_CLEAR, default, default, false);
                        }
                        break;

                    case Operation.OP_REMOVE:
                        key = reader.Read<TKey>();
                        if (apply)
                        {
                            if (objects.TryGetValue(key, out item))
                            {
                                // add dirty + changes.
                                // ClientToServer needs to set dirty in server OnDeserialize.
                                // no access check: server OnDeserialize can always
                                // write, even for ClientToServer (for broadcasting).
                                objects.Remove(key);
                                AddOperation(Operation.OP_REMOVE, key, item, false);
                            }
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

        public void Clear()
        {
            objects.Clear();
            AddOperation(Operation.OP_CLEAR, default, default, true);
        }

        public bool ContainsKey(TKey key) => objects.ContainsKey(key);

        public bool Remove(TKey key)
        {
            if (objects.TryGetValue(key, out TValue item) && objects.Remove(key))
            {
                AddOperation(Operation.OP_REMOVE, key, item, true);
                return true;
            }
            return false;
        }

        public TValue this[TKey i]
        {
            get => objects[i];
            set
            {
                if (ContainsKey(i))
                {
                    objects[i] = value;
                    AddOperation(Operation.OP_SET, i, value, true);
                }
                else
                {
                    objects[i] = value;
                    AddOperation(Operation.OP_ADD, i, value, true);
                }
            }
        }

        public bool TryGetValue(TKey key, out TValue value) => objects.TryGetValue(key, out value);

        public void Add(TKey key, TValue value)
        {
            objects.Add(key, value);
            AddOperation(Operation.OP_ADD, key, value, true);
        }

        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return TryGetValue(item.Key, out TValue val) && EqualityComparer<TValue>.Default.Equals(val, item.Value);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (arrayIndex < 0 || arrayIndex > array.Length)
            {
                throw new System.ArgumentOutOfRangeException(nameof(arrayIndex), "Array Index Out of Range");
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
                AddOperation(Operation.OP_REMOVE, item.Key, item.Value, true);
            }
            return result;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => objects.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => objects.GetEnumerator();
    }

    public class SyncDictionary<TKey, TValue> : SyncIDictionary<TKey, TValue>
    {
        public SyncDictionary(bool syncAllChanges = false) : base(new Dictionary<TKey, TValue>(), syncAllChanges) {}
        public SyncDictionary(IEqualityComparer<TKey> eq, bool syncAllChanges = false) : base(new Dictionary<TKey, TValue>(eq), syncAllChanges) {}
        public SyncDictionary(IDictionary<TKey, TValue> d, bool syncAllChanges = false) : base(new Dictionary<TKey, TValue>(d), syncAllChanges) {}
        public new Dictionary<TKey, TValue>.ValueCollection Values => ((Dictionary<TKey, TValue>)objects).Values;
        public new Dictionary<TKey, TValue>.KeyCollection Keys => ((Dictionary<TKey, TValue>)objects).Keys;
        public new Dictionary<TKey, TValue>.Enumerator GetEnumerator() => ((Dictionary<TKey, TValue>)objects).GetEnumerator();
    }
}
