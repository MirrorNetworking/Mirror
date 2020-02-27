# Authentication

When you have a multiplayer game, often you need to store information about your player for later games, keep game stats or communicate with your friends. For all these use cases, you often need a way to uniquely identify a user. Being able to tell users apart is called authentication. There are several methods available, some examples include:
-   Ask the user for username and password
-   Use a third party oath or OpenID identity provider, such as Facebook, Twitter, Google
-   Use a third party service such as PlayFab, GameLift or Steam
-   Use the device id, very popular method in mobile
-   Use Google Play in Android
-   Use Game Center in IOS
-   Use a web service in your website

In addition to the Authenticators listed below, you can make your own! Check out this [Guide](../../Guides/Authentication.md) for details.

## Authenticators

-   [Basic Authenticator](Basic.md)  
    Mirror includes a Basic Authenticator in the Mirror / Authenticators folder which just uses a simple username and password.
