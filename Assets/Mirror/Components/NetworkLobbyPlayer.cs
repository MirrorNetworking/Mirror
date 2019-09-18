using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror
{
    /// <summary>
    /// This component works in conjunction with the NetworkLobbyManager to make up the multiplayer lobby system.
    /// <para>The LobbyPrefab object of the NetworkLobbyManager must have this component on it. This component holds basic lobby player data required for the lobby to function. Game specific data for lobby players can be put in other components on the LobbyPrefab or in scripts derived from NetworkLobbyPlayer.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkLobbyPlayer")]
    [HelpURL("https://mirror-networking.com/xmldocs/articles/Components/NetworkRoomPlayer.html")]
    [Obsolete("Use / inherit from NetworkRoomPlayer instead")]
    public class NetworkLobbyPlayer : NetworkRoomPlayer { }
}
