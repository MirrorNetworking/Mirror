# Authentication

When you have a multiplayer game, often you need to store information about your player for later games, keep game stats or communicate with your friends. For all these use cases, you often need a way to uniquely identify a user. Being able to tell users apart is called authentication. There are several methods available, some examples include:

-   Ask the user for username and password

-   Use a third party oath or OpenID identity provider, such as Facebook, Twitter, Google

-   Use a third party service such as PlayFab, GameLift or Steam

-   Use the device id, very popular method in mobile

-   Use Google Play in Android

-   Use Game Center in IOS

-   Use a web service in your website

Mirror includes a  NetworkAuthenticator component that allows you to implement any authentication scheme you need.

## Encryption Warning

By default Mirror uses Telepathy, which is not encrypted, so if you want to do authentication through Mirror, we highly recommend you use a transport that supports encryption.

## Default Authenticator

Mirror automatically adds the NetworkAuthenticator component to any object where the Network Manager component is present. Here's what that looks like:

``` cs
namespace Mirror
{
    public class NetworkAuthenticator : MonoBehaviour
    {
        // Notify subscribers on the server when a client is authenticated
        public UnityEventConnection OnServerAuthenticated = new UnityEventConnection();

        // Notify subscribers on the client when the client is authenticated
        public UnityEventConnection OnClientAuthenticated = new UnityEventConnection();

        // Override this method to register server message handlers
        public virtual void ServerInitialize() { }

        // Override this method to register client message handlers
        public virtual void ClientInitialize() { }

        // Called on server when a client needs to authenticate
        public virtual void ServerAuthenticate(NetworkConnection conn)
        {
            conn.isAuthenticated = true;
            OnServerAuthenticated.Invoke(conn);
        }

        // Called on client when a client needs to authenticate
        public virtual void ClientAuthenticate(NetworkConnection conn)
        {
            conn.isAuthenticated = true;
            OnClientAuthenticated.Invoke(conn);
        }
    }
}
```

## Custom Authenticators

To make your own custom Authenticator, you can just create a new script in your project (not in the Mirror folders) that inherits from NetworkAuthenticator and override the methods as needed:

-   When a client is authenticated to your satisfaction, you **must** set the `isAuthenticated` flag on the `NetworkConnection` to true on **both** the server and client

-   When a client is authenticated to your satisfaction, you **must** invoke the `OnServerAuthenticated` and `OnClientAuthenticated` events on **both** the server and client

In addition to these requirements, we also *suggest* you do the following:

-   `ServerInitialize` and `ClientInitialize` are the appropriate methods to register server and client messages and their handlers.  They're called from StartServer/StartHost, and StartClient, respectively.

-   Send a message to the client if authentication fails, especially if there's some issue they can resolve.

-   Call the `Disconnect()` method of the `NetworkConnection` on the server and client when authentication fails. If you want to give the user a few tries to get their credentials right, you certainly can, but Mirror will not do the disconnect for you.

    -   Remember to put a small delay on the Disconnect call on the server if you send them a failure message so that it has a chance to be delivered before the connection is dropped.

-   `NetworkConnection` has an `AuthenticationData` object where you can drop a class instance of any data you need to persist on the server related to the authentication, such as account id's, tokens, character selection, etc.

Now that you have the foundation of a custom Authenticator component, the rest is up to you. You can exchange any number of custom message between the server and client as necessary to complete your authentication process before approving the client.

## Basic Authentication

To get you started, here's a complete example of a custom Authenticator.

-   Create a new script in your project (not in the Mirror folders) called `BasicAuthenticator`

-   Replace the boilerplate code that Unity provides with the code below and save

-   Drag the script to the inspector of the object in your scene that has Network Manager

-   Assign the Basic Authenticator component to the Authenticator field in Network Manager

-   Remove the Default Authenticator component.

When you're done, it should look like this:

![Inspector showing Basic Authentication component](BasicAuthentication.PNG)

>   **Note:** You don't need to assign anything to the event lists unless you want to subscribe to the events in your own code for your own purposes. Mirror has internal listeners for both events.

### Basic Authenticator

``` cs
using UnityEngine;
using Mirror;

public class BasicAuthenticator : NetworkAuthenticator
{
    [Header("Custom Properties")]

    // for demo purposes, set these in the inspector
    public string username;
    public string password;

    public class AuthRequestMessage : MessageBase
    {
        // use whatever credentials make sense for your game
        // for example, you might want to pass the accessToken if using oauth
        public string authUser;
        public string authPass;
    }

    public class AuthResponseMessage : MessageBase
    {
        public byte code;
        public string message;
    }

    public override void ServerInitialize()
    {
        // register a handler for the authentication request we expect from client
        NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequestMessage);
    }

    public override void ClientInitialize()
    {
        // register a handler for the authentication response we expect from server
        NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponseMessage);
    }

    public override void ServerAuthenticate(NetworkConnection conn)
    {
        // Do nothing...wait for AuthRequestMessage from client
    }

    public override void ClientAuthenticate(NetworkConnection conn)
    {
        AuthRequestMessage authRequestMessage = new AuthRequestMessage
        {
            authUser = username,
            authPass = password
        };

        NetworkClient.Send(authRequestMessage);
    }

    public void OnAuthRequestMessage(NetworkConnection conn, AuthRequestMessage msg)
    {
        Debug.LogFormat("Authentication Request: {0} {1}", msg.authUser, msg.authPass);

        // check the credentials by calling your web server, database table, playfab api, or any method appropriate.
        if (msg.authUser == username && msg.authPass == password)
        {
            // must set NetworkConnection isAuthenticated = true
            conn.isAuthenticated = true;

            // create and send msg to client so it knows to proceed
            AuthResponseMessage authResponseMessage = new AuthResponseMessage
            {
                code = 100,
                message = "Success"
            };

            NetworkServer.SendToClient(conn.connectionId, authResponseMessage);

            // must invoke server event when this connection is authenticated
            OnServerAuthenticated.Invoke(conn);
        }
        else
        {
            // must set NetworkConnection isAuthenticated = false
            conn.isAuthenticated = false;

            // create and send msg to client so it knows to disconnect
            AuthResponseMessage authResponseMessage = new AuthResponseMessage
            {
                code = 200,
                message = "Invalid Credentials"
            };

            NetworkServer.SendToClient(conn.connectionId, authResponseMessage);

            // disconnect the client after 1 second so that response message gets delivered
            Invoke(nameof(conn.Disconnect), 1);
        }
    }

    public void OnAuthResponseMessage(NetworkConnection conn, AuthResponseMessage msg)
    {
        if (msg.code == 100)
        {
            Debug.LogFormat("Authentication Response: {0}", msg.message);

            // Set this on the client for local reference
            conn.isAuthenticated = true;

            // must invoke client event when this connection is authenticated
            OnClientAuthenticated.Invoke(conn);
        }
        else
        {
            Debug.LogErrorFormat("Authentication Response: {0}", msg.message);

            // Set this on the client for local reference
            conn.isAuthenticated = false;

            // disconnect the client
            conn.Disconnect();
        }
    }
}
```
