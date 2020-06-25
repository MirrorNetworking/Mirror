using System;
using UnityEngine.Events;

namespace Mirror.Cloud.ListServerService
{
    public sealed class ListServer
    {
        public readonly IListServerServerApi ServerApi;
        public readonly IListServerClientApi ClientApi;

        public ListServer(IListServerServerApi serverApi, IListServerClientApi clientApi)
        {
            ServerApi = serverApi ?? throw new ArgumentNullException(nameof(serverApi));
            ClientApi = clientApi ?? throw new ArgumentNullException(nameof(clientApi));
        }
    }

    public interface IListServerServerApi : IBaseApi
    {
        /// <summary>
        /// Has a server been added to the list with this connection
        /// </summary>
        bool ServerInList { get; }
        /// <summary>
        /// Add a server to the list
        /// </summary>
        /// <param name="server"></param>
        void AddServer(ServerJson server);
        /// <summary>
        /// Update the current server
        /// </summary>
        /// <param name="newPlayerCount"></param>
        void UpdateServer(int newPlayerCount);
        /// <summary>
        /// Update the current server
        /// </summary>
        /// <param name="server"></param>
        void UpdateServer(ServerJson server);
        /// <summary>
        /// Removes the current server
        /// </summary>
        void RemoveServer();
    }

    public interface IListServerClientApi : IBaseApi
    {
        /// <summary>
        /// Called when the server list is updated
        /// </summary>
        event UnityAction<ServerCollectionJson> onServerListUpdated;

        /// <summary>
        /// Get the server list once
        /// </summary>
        void GetServerList();
        /// <summary>
        /// Start getting the server list every interval
        /// </summary>
        /// <param name="interval"></param>
        void StartGetServerListRepeat(int interval);
        /// <summary>
        /// Stop getting the server list
        /// </summary>
        void StopGetServerListRepeat();
    }
}
