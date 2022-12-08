using System;
using Mirror.Core.Events;
using UnityEngine;

namespace Mirror.Examples.Events
{
    public class Player : NetworkBehaviour
    {
        public void Start()
        {
            // Instantiate only on the local player for each object and the server
            if ((isClient && isLocalPlayer) || isServer)
            {
                EventManager.RegisterListeners(this);
            }

            if (isServer)
            {
                EventManager.Server_InvokeNetworkedEvent(new PlayerJoinEvent
                {
                    playerNetId = netId
                });
            }
        }

        public void OnDestroy()
        {
            EventManager.UnregisterListeners(this);
        }

        [Client, NetworkEventHandler]
        public void Client_OnPlayerJoined(PlayerJoinEvent playerJoinEvent)
        {
            Debug.Log($"Player joined with netId of {playerJoinEvent.playerNetId} - Ran only on client!");
        }

        [Server, NetworkEventHandler]
        public void Server_OnPlayerJoined(PlayerJoinEvent playerJoinEvent)
        {
            Debug.Log($"Player joined with netId of {playerJoinEvent.playerNetId} - Ran only on server!");
        }

        [NetworkEventHandler]
        public void OnPlayerJoined(PlayerJoinEvent playerJoinEvent)
        {
            Debug.Log($"Player joined with netId of {playerJoinEvent.playerNetId} - Ran on both client and server!");
        }

    }
}
