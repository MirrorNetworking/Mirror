namespace Mirror
{
    public struct NetworkMessage
    {
        public int msgType;
        public NetworkConnection conn;
        public NetworkReader reader;

        public TMsg ReadMessage<TMsg>() where TMsg : IMessageBase, new()
        {
            TMsg msg = new TMsg();
            msg.Deserialize(reader);
            return msg;
        }

        public void ReadMessage<TMsg>(TMsg msg) where TMsg : IMessageBase
        {
            msg.Deserialize(reader);
        }
    }
}
