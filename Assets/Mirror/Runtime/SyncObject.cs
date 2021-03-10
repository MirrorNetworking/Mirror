namespace Mirror
{
    /// <summary>
    /// A sync object is an object that can synchronize it's state
    /// between server and client, such as a SyncList
    /// </summary>
    public interface SyncObject
    {
        /// <summary>True if there are changes since the last flush</summary>
        bool IsDirty { get; }

        /// <summary>Discard all the queued changes</summary>
        // Consider the object fully synchronized with clients
        void Flush();

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
