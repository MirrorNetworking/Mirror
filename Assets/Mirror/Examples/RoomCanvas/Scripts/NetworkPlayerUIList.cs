using UnityEngine;

namespace Mirror.Examples.NetworkRoomCanvas
{
    public class NetworkPlayerUIList : MonoBehaviour
    {
        [SerializeField] NetworkPlayerUIItem _networkPlayerPrefab = null;
        NetworkPlayerUIItem[] _items;

        public void OnEnable()
        {
            NetworkRoomManagerExample manager = NetworkManager.singleton as NetworkRoomManagerExample;

            manager.onPlayerListChanged += AddAllPlayers;

            // call now incase event was missed
            NetworkRoomPlayerExample[] players = manager.GetPlayers();
            AddAllPlayers(players);
        }

        void OnDisable()
        {
            NetworkRoomManagerExample manager = NetworkRoomManagerExample.singleton as NetworkRoomManagerExample;

            if (manager != null)
            {
                manager.onPlayerListChanged -= AddAllPlayers;
            }
            RemoveAllPlayers();
        }

        void AddAllPlayers(NetworkRoomPlayerExample[] players)
        {
            RemoveAllPlayers();
            _items = new NetworkPlayerUIItem[players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                _items[i] = CreatePlayer(players[i]);
            }
        }

        NetworkPlayerUIItem CreatePlayer(NetworkRoomPlayerExample player)
        {
            NetworkPlayerUIItem clone = Instantiate(_networkPlayerPrefab, transform);
            clone.Setup(player);
            return clone;
        }

        void RemoveAllPlayers()
        {
            if (_items == null) { return; }
            foreach (NetworkPlayerUIItem item in _items)
            {
                Destroy(item.gameObject);
            }
            _items = null;
        }
    }
}
