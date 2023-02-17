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

        private static Dictionary<string, byte> typeNameToId;

        public byte id {get; private set;}

        static NetworkEvent() {
            typeNameToId = new Dictionary<string, byte>();

            IOrderedEnumerable<Type> derivedTypes = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(NetworkEvent))).OrderBy(t => t.FullName);

            byte id = 0;
            foreach (Type type in derivedTypes) {
                string typeName = type.FullName;
                if (!typeNameToId.ContainsKey(typeName)) {
                    typeNameToId[typeName] = id++;
                }
            }
        }

        protected NetworkEvent() {
            string typeName = GetType().FullName;
            if (!typeNameToId.TryGetValue(typeName, out byte id)) {
                throw new InvalidOperationException($"Type '{typeName}' has no assigned ID.");
            }
            this.id = id;
        }

        public static Type GetTypeById(byte id) {
            foreach (KeyValuePair<string, byte> kvp in typeNameToId) {
                if (kvp.Value == id) {
                    return Type.GetType(kvp.Key);
                }
            }
            return null;
        }

        public bool isNetworked;

        public virtual void Write(NetworkWriter writer)
        {
            writer.WriteByte(id);
            writer.WriteBool(isNetworked);
        }

        public virtual void Read(NetworkReader reader)
        {
            isNetworked = reader.ReadBool();
        }

    }
}
