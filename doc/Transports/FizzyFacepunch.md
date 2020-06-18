# FizzyFacepunch Transport

FizzyFacepunch is a Steam P2P transport for Mirror, it utilizes Steam's P2P service to directly connect or relay your connection to another player. FizzyFacepunch is based on the [Facepunch.Steamworks](https://github.com/Facepunch/Facepunch.Steamworks) wrapper.

You can get the release **[Here](https://github.com/Chykary/FizzyFacepunch/releases)** or you can clone the repo **[Here](https://github.com/Chykary/FizzyFacepunch)**.

## Features

* Multiple Customizable Channels : You can customize the channels in the transport, whether you want just 1 or 5 channels that are unreliable or reliable (best to leave channel 0 as reliable).
* Steam Nat Punching & Relay : The transport will use Steam to do Nat Punching to your destination, and if that doesn't work, steam's relay Server will be used to ensure you can always connect (latency may vary).
* No Code Changes Needed : If you Already use Mirror, you just need to slap this transport in (maybe add your steam App ID in your build), and everything should work the same like any other Mirror Transport. "It Just Works" -Todd Howard

![The FizzySteamworks Transport component in the Inspector window](FizzyFacepunch.PNG)

## Credits
* [Chykary](https://github.com/Chykary/FizzyFacepunch) : The author of this Transport.
* [Facepunch](https://github.com/Facepunch) : Creator of Facepunch.Steamworks.
* [vis2k](https://github.com/vis2k) : Creator of Mirror.
* Valve : Steam
