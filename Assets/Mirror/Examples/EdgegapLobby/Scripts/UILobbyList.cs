using System;
using System.Collections.Generic;
using Edgegap;
using UnityEngine;
using UnityEngine.UI;
namespace Mirror.Examples.EdgegapLobby
{
    public class UILobbyList : MonoBehaviour
    {
        public UILobbyCreate Create;

        public GameObject EntryPrefab;
        public Transform LobbyContent;
        public GameObject Loading;
        public Button RefreshButton;
        public InputField SearchInput;
        public Button CreateButton;
        public Text Error;
        private List<UILobbyEntry> _entries = new List<UILobbyEntry>();

        private EdgegapLobbyKcpTransport _transport => (EdgegapLobbyKcpTransport)NetworkManager.singleton.transport;
        private void Awake()
        {
            SearchInput.onValueChanged.AddListener(arg0 =>
            {
                SetLobbies(_transport.Api.Lobbies);
            });
            RefreshButton.onClick.AddListener(Refresh);
            CreateButton.onClick.AddListener(() =>
            {
                Create.gameObject.SetActive(true);
                gameObject.SetActive(false);
            });
        }
        public void Start()
        {
            Refresh();
        }

        private void Refresh()
        {
            Loading.SetActive(true);
            _transport.Api.RefreshLobbies(SetLobbies, s =>
            {
                Error.text = s;
                Loading.SetActive(false);
            });
        }

        public void Join(LobbyBrief lobby)
        {
            NetworkManager.singleton.networkAddress = lobby.lobby_id;
            NetworkManager.singleton.StartClient();
        }

        public void SetLobbies(LobbyBrief[] lobbies)
        {
            Loading.SetActive(false);
            Error.text = "";
            // Create enough entries
            for (int i = _entries.Count; i < lobbies.Length; i++)
            {
                var go = Instantiate(EntryPrefab, LobbyContent);
                _entries.Add(go.GetComponent<UILobbyEntry>());
            }

            // Update entries
            var searchText = SearchInput.text;
            for (int i = 0; i < lobbies.Length; i++)
            {
                _entries[i].Init(
                    this,
                    lobbies[i],
                    // search filter
                    searchText.Length == 0 ||
#if UNITY_2021_3_OR_NEWER
                    lobbies[i].name.Contains(searchText, StringComparison.InvariantCultureIgnoreCase)
#else
                    lobbies[i].name.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) >= 0
#endif
                );
            }

            // hide entries that are too many
            for (int i = lobbies.Length; i < _entries.Count; i++)
            {
                _entries[i].gameObject.SetActive(false);
            }
        }
    }
}
