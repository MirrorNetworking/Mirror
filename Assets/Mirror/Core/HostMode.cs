// host mode related helper functions.
// usually they set up both server & client.
// it's cleaner to keep them in one place, instead of only in server / client.
namespace Mirror
{
    internal static class HostMode
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
    }
}
