namespace Mirror
{
    /// <summary>
    /// Default Authenticator Component
    /// <para>This sets the NetworkConnection.isAuthenticated = true on both server and client and invokes their respective events.</para>
    /// </summary>
    public class DefaultAuthenticator : Authenticator
    {
        /// <summary>
        /// Called on server from StartServer to initialize the Authenticator
        /// </summary>
        /// <returns>True if initialization successful</returns>
        public override bool ServerInitialize()
        {
            return true;
        }

        /// <summary>
        /// Called on client from StartClient to initialize the Authenticator
        /// </summary>
        /// <returns>True if initialization successful</returns>
        public override bool ClientInitialize()
        {
            return true;
        }

        /// <summary>
        /// Called on server from OnServerAuthenticateInternal when a client needs to authenticate
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void ServerAuthenticate(NetworkConnection conn)
        {
            // setting NetworkConnection.isAuthenticated = true is Required
            conn.isAuthenticated = true;

            // invoking the event is Required
            OnServerAuthenticated.Invoke(conn);
        }

        /// <summary>
        /// Called on client from OnClientAuthenticateInternal when a client needs to authenticate
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void ClientAuthenticate(NetworkConnection conn)
        {
            // setting NetworkConnection.isAuthenticated = true is Required
            conn.isAuthenticated = true;

            // invoking the event is Required
            OnClientAuthenticated.Invoke(conn);
        }
    }
}
