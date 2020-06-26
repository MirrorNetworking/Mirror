using UnityEngine;

namespace Mirror
{
    public static class NetworkHost
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkHost));

        public static void ConnectHost()
        {
            logger.Log("Client Connect Host to Server");

            NetworkClient.RegisterSystemHandlers(true);

            NetworkClient.connectState = ConnectState.Connected;

            // create local connection objects and connect them
            ULocalConnectionToServer connectionToServer = new ULocalConnectionToServer();
            ULocalConnectionToClient connectionToClient = new ULocalConnectionToClient();
            connectionToServer.connectionToClient = connectionToClient;
            connectionToClient.connectionToServer = connectionToServer;

            NetworkClient.SetConnection(connectionToServer);

            // create server connection to local client
            NetworkServer.SetLocalConnection(connectionToClient);
        }

        /// <summary>
        /// connect host mode
        /// </summary>
        public static void ConnectLocalServer()
        {
            NetworkServer.OnConnected(NetworkServer.localConnection);
            NetworkServer.localConnection.Send(new ConnectMessage());
        }

        /// <summary>
        /// disconnect host mode. this is needed to call DisconnectMessage for
        /// the host client too.
        /// </summary>
        public static void DisconnectLocalServer()
        {
            // only if host connection is running
            if (NetworkServer.localConnection != null)
            {
                // TODO ConnectLocalServer manually sends a ConnectMessage to the
                // local connection. should we send a DisconnectMessage here too?
                // (if we do then we get an Unknown Message ID log)
                //NetworkServer.localConnection.Send(new DisconnectMessage());
                NetworkServer.OnDisconnected(NetworkServer.localConnection.connectionId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public static void ActivateHostScene()
        {
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
            {
                if (!identity.isClient)
                {
                    if (logger.LogEnabled()) logger.Log("ActivateHostScene " + identity.netId + " " + identity);

                    identity.OnStartClient();
                }
            }
        }
    }
}
