namespace Mirror
{
    // A sync object is an object that can synchronize it's state 
    // between server and client,  such as a SyncList
    public interface SyncObject
    {

        // initialize the syncobject with the behavior and its id
        void InitializeBehaviour(INetworkBehaviour beh);

        // true if there are changes since the last flush
        bool IsDirty { get; set; }

        // Write a full copy of the object
        void OnSerialize(NetworkWriter writer);

        // deserialize all the data in the object
        void OnDeserialize(NetworkReader reader);
    }
}