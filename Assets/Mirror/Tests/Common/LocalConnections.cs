namespace Mirror.Tests
{

    public static class LocalConnections 
    {
        public static (NetworkConnectionToServer, NetworkConnectionToClient) PipedConnections(bool authenticated = false)
        {
            (IConnection c1, IConnection c2) = PipeConnection.CreatePipe();
            var toServer = new NetworkConnectionToServer(c2);
            var toClient = new NetworkConnectionToClient(c1);

            toServer.isAuthenticated = authenticated;
            toClient.isAuthenticated = authenticated;

            return (toServer, toClient);
        }

    }
}