using System;

namespace Mirror.Core.Events
{
    /// <summary>
    /// All network events should inherit this network event. They should also call NetworkEvent#Write() and
    /// NetworkEvent#Read() before handling their reading and writing in their implementation.
    /// </summary>
    [Serializable]
    public class NetworkEvent
    {

        public bool isNetworked;

        public virtual void Write(NetworkWriter writer)
        {
            writer.WriteString(GetType().FullName);
            writer.WriteBool(isNetworked);
        }

        public virtual void Read(NetworkReader reader)
        {
            isNetworked = reader.ReadBool();
        }

    }
}
