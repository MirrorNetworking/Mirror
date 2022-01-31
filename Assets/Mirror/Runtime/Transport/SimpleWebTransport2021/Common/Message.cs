using System;

namespace Mirror.SimpleWeb
{
    public struct Message
    {
        public readonly int connId;
        public readonly EventType type;
        public readonly ArrayBuffer data;
        public readonly Exception exception;

        public Message(EventType type) : this()
        {
            this.type = type;
        }

        public Message(ArrayBuffer data) : this()
        {
            type = EventType.Data;
            this.data = data;
        }

        public Message(Exception exception) : this()
        {
            type = EventType.Error;
            this.exception = exception;
        }

        public Message(int connId, EventType type) : this()
        {
            this.connId = connId;
            this.type = type;
        }

        public Message(int connId, ArrayBuffer data) : this()
        {
            this.connId = connId;
            type = EventType.Data;
            this.data = data;
        }

        public Message(int connId, Exception exception) : this()
        {
            this.connId = connId;
            type = EventType.Error;
            this.exception = exception;
        }
    }
}
