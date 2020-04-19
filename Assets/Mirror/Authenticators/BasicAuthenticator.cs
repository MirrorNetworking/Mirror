using System.Collections;
using UnityEngine;

namespace Mirror.Authenticators
{
    [AddComponentMenu("Network/Authenticators/BasicAuthenticator")]
    public class BasicAuthenticator : NetworkAuthenticator
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(BasicAuthenticator));

        [Header("Custom Properties")]
        public NetworkManager manager;

        // set these in the inspector
        public string Username;
        public string Password;

        public class AuthRequestMessage : MessageBase
        {
            // use whatever credentials make sense for your game
            // for example, you might want to pass the accessToken if using oauth
            public string AuthUsername;
            public string AuthPassword;
        }

        public class AuthResponseMessage : MessageBase
        {
            public byte Code;
            public string Message;
        }

        public override void OnServerAuthenticate(INetworkConnection conn)
        {
            // wait for AuthRequestMessage from client
            conn.RegisterHandler<AuthRequestMessage>(OnAuthRequestMessage);
        }

        public override void OnClientAuthenticate(INetworkConnection conn)
        {
            conn.RegisterHandler<AuthResponseMessage>(OnAuthResponseMessage);

            var authRequestMessage = new AuthRequestMessage
            {
                AuthUsername = Username,
                AuthPassword = Password
            };

            conn.Send(authRequestMessage);
        }

        public void OnAuthRequestMessage(INetworkConnection conn, AuthRequestMessage msg)
        {
            logger.LogFormat(LogType.Log, "Authentication Request: {0} {1}", msg.AuthUsername, msg.AuthPassword);

            // check the credentials by calling your web server, database table, playfab api, or any method appropriate.
            if (msg.AuthUsername == Username && msg.AuthPassword == Password)
            {
                // create and send msg to client so it knows to proceed
                var authResponseMessage = new AuthResponseMessage
                {
                    Code = 100,
                    Message = "Success"
                };

                conn.Send(authResponseMessage);

                // Invoke the event to complete a successful authentication
                base.OnServerAuthenticate(conn);
            }
            else
            {
                // create and send msg to client so it knows to disconnect
                var authResponseMessage = new AuthResponseMessage
                {
                    Code = 200,
                    Message = "Invalid Credentials"
                };

                conn.Send(authResponseMessage);

                // disconnect the client after 1 second so that response message gets delivered
                StartCoroutine(DelayedDisconnect(conn, 1));
            }
        }

        public IEnumerator DelayedDisconnect(INetworkConnection conn, float waitTime)
        {
            yield return new WaitForSeconds(waitTime);
            conn.Disconnect();
        }

        public void OnAuthResponseMessage(INetworkConnection conn, AuthResponseMessage msg)
        {
            if (msg.Code == 100)
            {
                logger.LogFormat(LogType.Log, "Authentication Response: {0}", msg.Message);

                // Invoke the event to complete a successful authentication
                base.OnClientAuthenticate(conn);
            }
            else
            {
                logger.LogFormat(LogType.Error, "Authentication Response: {0}", msg.Message);

                // disconnect the client
                conn.Disconnect();
            }
        }
    }
}
