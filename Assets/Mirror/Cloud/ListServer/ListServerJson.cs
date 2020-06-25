using System;

namespace Mirror.Cloud.ListServerService
{
    [Serializable]
    public struct ServerCollectionJson : ICanBeJson
    {
        public ServerJson[] servers;
    }

    [Serializable]
    public struct ServerJson : ICanBeJson
    {
        public string protocol;
        public int port;
        public int playerCount;
        public int maxPlayerCount;

        /// <summary>
        /// optional
        /// </summary>
        public string displayName;
        /// <summary>
        /// This is returns from the api, any incoming address fields will be ignored
        /// </summary>
        public string address;
    }

    [Serializable]
    public struct PartialServerJson : ICanBeJson
    {
        /// <summary>
        /// optional
        /// </summary>
        public int playerCount;
        /// <summary>
        /// optional
        /// </summary>
        public int maxPlayerCount;
        /// <summary>
        /// optional
        /// </summary>
        public string displayName;
    }
}
