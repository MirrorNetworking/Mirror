Docs: https://mirror-networking.gitbook.io/docs/manual/examples/edgegap-lobby
This is a copy of the Tanks example (basic scene with player controlled tanks), 
but with a lobby ui for using Edgegap's Lobby and Relay service.
It showcases how one might interact with the EdgegapLobbyKcpTransport to list, join and create lobbies. 
Providing a good starting point for anyone wanting to use Edgegap lobbies.

# Setup
As this example uses external services from Edgegap you will need to set up the transport 
on the NetworkManager gameobject before you can use it.
Please see the EdgegapLobbyKcpTransport Setup instructions on how to do that: 
https://mirror-networking.gitbook.io/docs/manual/transports/edgegap-relay-transport#setup