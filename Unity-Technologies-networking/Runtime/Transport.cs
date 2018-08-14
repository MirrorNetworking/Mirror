// the transport layer implementation

namespace UnityEngine.Networking
{
    public static class Transport
    {
        public static Telepathy.Client client = new Telepathy.Client();
        public static Telepathy.Server server = new Telepathy.Server();

        // hlapi needs to know max packet size to show warnings
        public static int MaxPacketSize = ushort.MaxValue;

        static Transport()
        {
            // tell Telepathy to use Unity's Debug.Log
            Telepathy.Logger.LogMethod = Debug.Log;
            Telepathy.Logger.LogWarningMethod = Debug.LogWarning;
            Telepathy.Logger.LogErrorMethod = Debug.LogError;
        }
    }
}