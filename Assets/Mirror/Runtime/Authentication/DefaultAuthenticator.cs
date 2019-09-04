namespace Mirror
{
    public class DefaultAuthenticator : Authenticator
    {
        public override bool IsAuthenticated()
        {
            return true;
        }

        public override void ServerAuthenticate(NetworkConnection conn)
        {
            OnServerAuthenticated.Invoke(conn);
        }

        public override void ClientAuthenticate(NetworkConnection conn)
        {
            OnClientAuthenticated.Invoke(conn);
        }
    }
}
