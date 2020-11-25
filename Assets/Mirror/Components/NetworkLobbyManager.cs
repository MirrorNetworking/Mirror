using System;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// This is a specialized NetworkManager that includes a networked lobby.
    /// </summary>
    /// <remarks>
    /// <para>The lobby has slots that track the joined players, and a maximum player count that is enforced. It requires that the NetworkLobbyPlayer component be on the lobby player objects.</para>
    /// <para>NetworkLobbyManager is derived from NetworkManager, and so it implements many of the virtual functions provided by the NetworkManager class. To avoid accidentally replacing functionality of the NetworkLobbyManager, there are new virtual functions on the NetworkLobbyManager that begin with "OnLobby". These should be used on classes derived from NetworkLobbyManager instead of the virtual functions on NetworkManager.</para>
    /// <para>The OnLobby*() functions have empty implementations on the NetworkLobbyManager base class, so the base class functions do not have to be called.</para>
    /// </remarks>
    [AddComponentMenu("Network/NetworkLobbyManager")]
    [HelpURL("https://mirror-networking.com/docs/Articles/Components/NetworkRoomManager.html")]
    [Obsolete("Use / inherit from NetworkRoomManager instead")]
    public class NetworkLobbyManager : NetworkRoomManager { }
}
