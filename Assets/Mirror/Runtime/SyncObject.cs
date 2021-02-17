namespace Mirror
{
    /// <summary>
    /// A sync object is an object that can synchronize it's state
    /// between server and client, such as a SyncList
    /// </summary>
    public interface SyncObject
    {
        /// <summary>
        /// true if there are changes since the last flush
        /// </summary>
        bool IsDirty { get; }

        /// <summary>
        /// Discard all the queued changes
        /// <para>Consider the object fully synchronized with clients</para>
        /// </summary>
        void Flush();

        /// <summary>
        /// Write a full copy of the object
        /// </summary>
        /// <param name="writer"></param>
        void OnSerializeAll(NetworkWriter writer);

        /// <summary>
        /// Write the changes made to the object since last sync
        /// </summary>
        /// <param name="writer"></param>
        void OnSerializeDelta(NetworkWriter writer);

        /// <summary>
        /// Reads a full copy of the object
        /// </summary>
        /// <param name="reader"></param>
        void OnDeserializeAll(NetworkReader reader);

        /// <summary>
        /// Reads the changes made to the object since last sync
        /// </summary>
        /// <param name="reader"></param>
        void OnDeserializeDelta(NetworkReader reader);

        /// <summary>
        /// Resets the SyncObject so that it can be re-used
        /// </summary>
        void Reset();
    }
}
