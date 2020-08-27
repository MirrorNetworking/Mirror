using Mirror.Cloud.ListServerService;
using UnityEngine;

namespace Mirror.Cloud.Example
{
    /// <summary>
    /// This Script can be used to test the list server without needing to use canvas or other UI
    /// </summary>
    public class QuickListServerDebug : MonoBehaviour
    {
        ApiConnector connector;
        ServerCollectionJson? collection;

        private void Start()
        {
            NetworkManager manager = NetworkManager.singleton;
            connector = manager.GetComponent<ApiConnector>();

            connector.ListServer.ClientApi.onServerListUpdated += ClientApi_onServerListUpdated;
        }

        private void ClientApi_onServerListUpdated(ServerCollectionJson arg0)
        {
            collection = arg0;
        }

        public void OnGUI()
        {
            GUILayout.Label("List Server");
            if (GUILayout.Button("Refresh"))
            {
                connector.ListServer.ClientApi.GetServerList();
            }
            GUILayout.Space(40);

            if (collection != null)
            {
                GUILayout.Label("Servers:");
                foreach (ServerJson item in collection.Value.servers)
                {
                    GUILayout.Label($"{item.displayName}, {item.address}, {item.playerCount}/{item.maxPlayerCount}");
                }
            }
        }
    }
}
