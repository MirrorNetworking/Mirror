namespace Mirror
{
    public struct NetworkMessage
    {
        public int msgType;
        public NetworkConnection conn;
        public NetworkReader reader;

        public TMsg ReadMessage<TMsg>() where TMsg : IMessageBase, new()
        {
            // Normally I would just do:
            // TMsg msg = new TMsg();
            // but mono calls an expensive method Activator.CreateInstance
            // For value types this is unnecesary,  just use the default value
            TMsg msg = typeof(TMsg).IsValueType ? default(TMsg) : new TMsg();
            msg.Deserialize(reader);
            return msg;
        }

        public void ReadMessage<TMsg>(TMsg msg) where TMsg : IMessageBase
        {
            msg.Deserialize(reader);
        }
    }
}
