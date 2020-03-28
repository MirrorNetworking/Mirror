# Network Discovery

Network Discovery uses a UDP broadcast on the LAN enabling clients to find the running server and connect to it.

See also our extended [guide document](../Guides/NetworkDiscovery.md).

![Inspector](NetworkDiscovery.png)

NetworkDiscovery and NetworkDiscoveryHUD components are included, or you can make your own from a [ScriptTemplate](../General/ScriptTemplates.md).

When a server is started, it listens on the UDP Broadcast Listen Port for requests from clients and returns a connection URI that clients apply to their transport.

You can adjust how often the clients send their requests out to find a server in seconds with the Active Discovery Interval.

The Server Found event must be assigned to a handler method, e.g. the OnDiscoveredServer method of NetworkDiscoveryHUD.

In the NetworkDiscoveryHUD, the NetworkDiscovery component should be assigned automatically.
