using System;

namespace Mirror
{
    /// <summary>SyncObjects sync state between server and client. E.g. SyncLists.</summary>
    public interface SyncObject
    {
        /// <summary>Used internally to set owner NetworkBehaviour's dirty mask bit when changed.</summary>
        Action OnDirty { get; set; }

        /// <summary>Discard all the queued changes</summary>
        // Consider the object fully synchronized with clients
        void ClearChanges();

        /// <summary>Write a full copy of the object</summary>
        void OnSerializeAll(NetworkWriter writer);

        /// <summary>Write the changes made to the object since last sync</summary>
        void OnSerializeDelta(NetworkWriter writer);

        /// <summary>Reads a full copy of the object</summary>
        void OnDeserializeAll(NetworkReader reader);

        /// <summary>Reads the changes made to the object since last sync</summary>
        void OnDeserializeDelta(NetworkReader reader);

        /// <summary>Resets the SyncObject so that it can be re-used</summary>
        void Reset();
    }
}
