# Fallback Transport

The FallbackTransport can be used to work around transport platform limits.

For example, our Libuv2k transport is currently only available on Windows, Mac and Linux whereas Telepathy is available on all platforms. Libuv2k has significant performance improvements, and ideally we would want Mirror to use Libuv2k if on Windows/Mac/Linux and fall back to Telepathy otherwise.

This is what the FallbackTransport allows us to do.

Usage:

1. Add a gameobject with a NetworkManager to your scene if you have not done so
2. By default, Unity will add TelepathyTransport to your NetworkManager game object
3. Add a FallbackTransport component to the gameobject
4. Assign the FallbackTransport component in your NetworkManager's transport
5. Add a Libuv2kTransport component to the gameobject
6. Add both Libuv2kTransport and TelepathyTransport to the FallbackTransport's transport property.

Important: all fallback transport need to be binary compatible with each other. For example, it might happen that the server runs Libuv2k and a client connects to it with Telepathy.

![The Fallback Transport](Fallback.PNG)
