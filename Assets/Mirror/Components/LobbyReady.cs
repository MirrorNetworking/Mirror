using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class LobbyReady : MonoBehaviour
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkServer));

        public List<ObjectReady> ObjectReadyList = new List<ObjectReady>();

        // just a cached memory area where we can collect connections
        // for broadcasting messages
        private static readonly List<INetworkConnection> connectionsCache = new List<INetworkConnection>();

        public void SetAllClientsNotReady()
        {
            foreach(ObjectReady obj in ObjectReadyList)
            {
                obj.SetClientNotReady();
            }
        }

        public void SendToReady<T>(NetworkIdentity identity, T msg, bool includeOwner = true, int channelId = Channel.Reliable)
        {
            if (logger.LogEnabled()) logger.Log("Server.SendToReady msgType:" + typeof(T));

            connectionsCache.Clear();

            foreach (ObjectReady objectReady in ObjectReadyList)
            {
                bool isOwner = objectReady.NetIdentity == identity;
                if ((!isOwner || includeOwner) && objectReady.IsReady)
                {
                    connectionsCache.Add(objectReady.NetIdentity.ConnectionToClient);
                }
            }

            NetworkConnection.Send(connectionsCache, msg, channelId);
        }
    }
}
