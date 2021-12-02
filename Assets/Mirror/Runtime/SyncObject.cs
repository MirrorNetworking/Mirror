using System;

namespace Mirror
{
    /// <summary>SyncObjects sync state between server and client. E.g. SyncLists.</summary>
    // SyncObject should be a class (instead of an interface) for a few reasons:
    // * NetworkBehaviour stores SyncObjects in a list. structs would be a copy
    //   and OnSerialize would use the copy instead of the original struct.
    // * Obsolete functions like Flush() don't need to be defined by each type
    // * OnDirty/IsRecording etc. default functions can be defined once here
    //   for example, handling 'OnDirty wasn't initialized' with a default
    //   function that throws an exception will be useful for SyncVar<T>
    public abstract class SyncObject
    {
        /// <summary>Used internally to set owner NetworkBehaviour's dirty mask bit when changed.</summary>
        public Action OnDirty;

        /// <summary>Used internally to check if we are currently tracking changes.</summary>
        // prevents ever growing .changes lists:
        // if a monster has no observers but we keep modifing a SyncObject,
        // then the changes would never be flushed and keep growing,
        // because OnSerialize isn't called without observers.
        // => Func so we can set it to () => observers.Count > 0
        //    without depending on NetworkComponent/NetworkIdentity here.
        // => virtual so it sipmly always records by default
        public Func<bool> IsRecording = () => true;

        /// <summary>Discard all the queued changes</summary>
        // Consider the object fully synchronized with clients
        public abstract void ClearChanges();

        // Deprecated 2021-09-17
        [Obsolete("Deprecated: Use ClearChanges instead.")]
        public void Flush() => ClearChanges();

        /// <summary>Write a full copy of the object</summary>
        public abstract void OnSerializeAll(NetworkWriter writer);

        /// <summary>Write the changes made to the object since last sync</summary>
        public abstract void OnSerializeDelta(NetworkWriter writer);

        /// <summary>Reads a full copy of the object</summary>
        public abstract void OnDeserializeAll(NetworkReader reader);

        /// <summary>Reads the changes made to the object since last sync</summary>
        public abstract void OnDeserializeDelta(NetworkReader reader);

        /// <summary>Resets the SyncObject so that it can be re-used</summary>
        public abstract void Reset();
    }
}
