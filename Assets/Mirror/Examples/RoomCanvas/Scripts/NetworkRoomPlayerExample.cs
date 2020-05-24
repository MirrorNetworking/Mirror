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

        /// <inheritdoc/>
        public override void ReadyStateChanged(bool oldReadyState, bool readyState)
        {
            onReadyChanged?.Invoke(readyState);
        }
    }
}
