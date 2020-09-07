namespace Mirror.Tests
{

    public static class LocalConnections 
    {
        public static (NetworkConnection, NetworkConnection) PipedConnections()
        {
            (IConnection c1, IConnection c2) = PipeConnection.CreatePipe();
            var toServer = new NetworkConnection(c2);
            var toClient = new NetworkConnection(c1);

            return (toServer, toClient);
        }

    }
}
