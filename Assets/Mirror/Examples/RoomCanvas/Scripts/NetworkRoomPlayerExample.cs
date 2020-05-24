using System;
using UnityEngine;

namespace Mirror.Examples.NetworkRoomCanvas
{
    [AddComponentMenu("")]
    public class NetworkRoomPlayerExample : NetworkRoomPlayer
    {
        [SyncVar]
        bool _isHost;

        public bool IsHost => _isHost;

        public override void OnStartLocalPlayer()
        {
            if (isServer)
            {
                _isHost = true;
                readyToBegin = true;
            }
        }

        public event Action<bool> onReadyChanged;

        /// <summary>
        /// This is a hook that is invoked on clients when a RoomPlayer switches between ready or not ready.
        /// <para>This function is called when the a client player calls SendReadyToBeginMessage() or SendNotReadyToBeginMessage().</para>
        /// </summary>
        /// <param name="readyState">Whether the player is ready or not.</param>
        public override void ReadyStateChanged(bool oldReadyState, bool readyState)
        {
            onReadyChanged?.Invoke(readyState);
        }
    }
}
