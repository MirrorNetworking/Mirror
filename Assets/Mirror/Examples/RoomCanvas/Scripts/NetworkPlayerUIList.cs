using UnityEngine;

namespace Mirror.Examples.NetworkRoomCanvas
{
    public class NetworkPlayerUIList : MonoBehaviour
    {
        [SerializeField] private NetworkPlayerUIItem _networkPlayerPrefab = null;
        private NetworkPlayerUIItem[] _items;

        public void OnEnable()
        {
            NetworkRoomManagerExample manager = NetworkManager.singleton as NetworkRoomManagerExample;

            manager.onPlayerListChanged += addAllPlayers;

            // call now incase event was missed
            NetworkRoomPlayerExample[] players = manager.GetPlayers();
            addAllPlayers(players);
        }


        private void OnDisable()
        {
            NetworkRoomManagerExample manager = NetworkRoomManagerExample.singleton as NetworkRoomManagerExample;

            if (manager != null)
            {
                manager.onPlayerListChanged -= addAllPlayers;
            }
            removeAllPlayers();
        }

        private void addAllPlayers(NetworkRoomPlayerExample[] players)
        {
            removeAllPlayers();
            _items = new NetworkPlayerUIItem[players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                _items[i] = createPlayer(players[i]);
            }
        }
        private NetworkPlayerUIItem createPlayer(NetworkRoomPlayerExample player)
        {
            NetworkPlayerUIItem clone = Instantiate(_networkPlayerPrefab, transform);
            clone.Setup(player);
            return clone;
        }
        private void removeAllPlayers()
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
