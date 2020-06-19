# Authentication

When you have a multiplayer game, often you need to store information about your player for later games, keep game stats or communicate with your friends. For all these use cases, you often need a way to uniquely identify a user. Being able to tell users apart is called authentication. There are several methods available, some examples include:
-   Ask the user for username and password
-   Use a third party OAuth2 or OpenID identity provider, such as Facebook, Twitter, Google
-   Use a third party service such as PlayFab, GameLift or Steam
-   Use the device id, very popular method in mobile
-   Use Google Play in Android
-   Use Game Center in IOS
-   Use a web service in your website

## Encryption Notice

By default Mirror uses Telepathy, which is not encrypted, so if you want to do authentication through Mirror, we highly recommend you use a transport that supports encryption.

## Basic Authenticator

-   [Basic Authenticator](Basic.md)  
    Mirror includes a Basic Authenticator in the Mirror / Authenticators folder which just uses a simple username and password.

## Custom Authenticators

Authenticators are derived from an `Authenticator` abstract class that allows you to implement any authentication scheme you need.

From the Assets menu, click Create > Mirror > Network Authenticator to make your own custom Authenticator from our [Script Templates](../General/ScriptTemplates.md), and just fill in the messages and validation code to suit your needs. When a client is successfully authenticated,  call `base.OnServerAuthenticated.Invoke(conn)` on the server and `base.OnClientAuthenticated.Invoke(conn)` on the client. Mirror is listening for these events to proceed with the connection sequence. Subscribe to OnServerAuthenticated and OnClientAuthenticated events if you wish to perform additional steps after authentication.

## Message Registration

By default all messages registered to `NetworkServer` and `NetworkClient` require authentication unless explicitly indicated otherwise. To register messages to bypass authentication, you need to specify `false` for a bool parameter to the `RegisterMessage` method:

```
NetworkServer.RegisterHandler<AuthenticationRequest>(OnAuthRequestMessage, false);
```

Certain internal messages already have been set to bypass authentication:

-   Server
    -   `ConnectMessage`
    -   `DisconnectMessage`
    -   `ErrorMessage`
    -   `NetworkPingMessage`
-   Client
    -   `ConnectMessage`
    -   `DisconnectMessage`
    -   `ErrorMessage`
    -   `SceneMessage`
    -   `NetworkPongMessage`

### Tips

-   Register handlers for messages in `OnStartServer` and `OnStartClient`. They're called from StartServer/StartHost, and StartClient, respectively.
-   Send a message to the client if authentication fails, especially if there's some issue they can resolve.
-   Call the `Disconnect()` method of the `NetworkConnection` on the server and client when authentication fails. If you want to give the user a few tries to get their credentials right, you certainly can, but Mirror will not do the disconnect for you.
    -   Remember to put a small delay on the Disconnect call on the server if you send a failure message so that it has a chance to be delivered before the connection is dropped.
-   `NetworkConnection` has an `authenticationData` object where you can drop any data you need to persist on the server related to the authentication, such as account id's, tokens, character selection, etc.

Now that you have the foundation of a custom Authenticator component, the rest is up to you. You can exchange any number of custom messages between the server and client as necessary to complete your authentication process before approving the client.

Authentication can also be extended to character selection and customization, just by crafting additional messages and exchanging them with the client before completing the authentication process.  This means this process takes place before the client player actually enters the game or changes to the Online scene.

If you write a good authenticator, consider sharing it with other users or donating it to the Mirror project.
