namespace Mirror
{
    public class DefaultAuthenticator : Authenticator
    {
        public override void ServerAuthenticate(NetworkConnection conn)
        {
            conn.isAuthenticated = true;
            OnServerAuthenticated.Invoke(conn);
        }

        public override void ClientAuthenticate(NetworkConnection conn)
        {
            conn.isAuthenticated = true;
            OnClientAuthenticated.Invoke(conn);
        }
    }
}
