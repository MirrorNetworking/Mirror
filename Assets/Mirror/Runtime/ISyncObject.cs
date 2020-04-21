namespace Mirror
{
    // A sync object is an object that can synchronize it's state
    // between server and client,  such as a SyncList
    public interface ISyncObject
    {
        // true if there are changes since the last flush
        bool IsDirty { get; }

        // Discard all the queued changes
        // Consider the object fully synchronized with clients
        void Flush();

        // Write a full copy of the object
        void OnSerializeAll(NetworkWriter writer);

        // Write the changes made to the object
        void OnSerializeDelta(NetworkWriter writer);

        // deserialize all the data in the object
        void OnDeserializeAll(NetworkReader reader);

        // deserialize changes since last sync
        void OnDeserializeDelta(NetworkReader reader);

        /// <summary>
        /// Resets the SyncObject so that it can be re-used
        /// </summary>
        void Reset();
    }
}
