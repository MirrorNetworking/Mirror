// host mode related helper functions.
// usually they set up both server & client.
// it's cleaner to keep them in one place, instead of only in server / client.
using System;

namespace Mirror
{
    public static class HostMode
    {
        // keep the local connections setup in one function.
        // makes host setup easier to follow.
        internal static void SetupConnections()
        {
            // create local connections pair, both are connected
            Utils.CreateLocalConnections(
                out LocalConnectionToClient connectionToClient,
                out LocalConnectionToServer connectionToServer);

            // set client connection
            NetworkClient.connection = connectionToServer;

            // set server connection
            NetworkServer.SetLocalConnection(connectionToClient);
        }

        // call OnConnected on server & client.
        // public because NetworkClient.ConnectLocalServer was public before too.
        public static void InvokeOnConnected()
        {
            // call server OnConnected with server's connection to client
            NetworkServer.OnConnected(NetworkServer.localConnection);

            // call client OnConnected with client's connection to server
            // => previously we used to send a ConnectMessage to
            //    NetworkServer.localConnection. this would queue the message
            //    until NetworkClient.Update processes it.
            // => invoking the client's OnConnected event directly here makes
            //    tests fail. so let's do it exactly the same order as before by
            //    queueing the event for next Update!
            //OnConnectedEvent?.Invoke(connection);
            ((LocalConnectionToServer)NetworkClient.connection).QueueConnectedEvent();
        }

        // DEPRECATED 2023-01-28
        [Obsolete("ActivateHostScene did nothing, since identities all had .isClient set in NetworkServer.SpawnObjects.")]
        public static void ActivateHostScene() {}
    }
}
