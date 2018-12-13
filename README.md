![Mirror Logo](https://i.imgur.com/5dUNWxl.png)

[![Download](https://img.shields.io/badge/asset_store-brightgreen.svg)](https://www.assetstore.unity3d.com/#!/content/129321)
[![Forum](https://img.shields.io/badge/forum-brightgreen.svg)](https://forum.unity.com/threads/mirror-networking-for-unity-aka-hlapi-community-edition.425437/)
[![donate](https://img.shields.io/badge/donations-brightgreen.svg)](https://www.patreon.com/MirrorTelepathy)
[![Discord](https://img.shields.io/discord/343440455738064897.svg)](https://discordapp.com/invite/N9QVxbM)
[![release](https://img.shields.io/github/release/vis2k/Mirror.svg)](https://github.com/vis2k/Mirror/releases/latest)

Mirror is a **high level** Networking API for Unity, built on top of the **low level** [Telepathy](https://github.com/vis2k/Telepathy) library.

Mirror is built [and tested](https://docs.google.com/document/d/e/2PACX-1vQqf_iqOLlBRTUqqyor_OUx_rHlYx-SYvZWMvHGuLIuRuxJ-qX3s8JzrrBB5vxDdGfl-HhYZW3g5lLW/pub#h.h4wha2mpetsc) for **MMO Scale** Networking by the developers of [uMMORPG](https://www.assetstore.unity3d.com/#!/content/51212), [uSurvival](https://www.assetstore.unity3d.com/#!/content/95015) and [Cubica](https://cubica.net).

Mirror is optimized for **ease of use** and **probability of success**. Projects that use Mirror are small, concise and maintainable. uMMORPG was possible with <6000 lines of code. We needed a networking library that allows us to [launch our games](https://ummorpg.net/showcase/), period.

With Mirror, the **Server & Client are ONE** project _(hence the name)_. Instead of having one code base for the server and one for the client, we simply use the same code for both of them.
* `[Server]` / `[Client]` tags can be used for the server-only and client-only parts.
* `[Command]` are used for Client->Server, and `[ClientRpc]` / `[TargetRpc]` for Server->Client communication.
* `[SyncVar]`s and `SyncList`s are used to automatically synchronize state.

What previously required **10.000** lines of code, now takes **1.000** lines of code. Therein lies the **magic of Mirror**.

_Note: Mirror is based on Unity's abandoned UNET Networking system. We fixed it up and pushed it to MMO Scale._

## Changelog
This is a work in progress branch.  The differences with mirror branch are:

 * It is no longer built as dlls,  you import the source directly in your unity project
 * The TCP transport uses asynchronous instead of threads.  From our testing,  this scales a lot better.
 * It only works in Unity 2018.2 and later,  support for 2017.4 has been dropped
 * Only TCP transport is provided as of this writing,  LLAPI and websockets are WIP
 * Proper error handling.  Just override OnClientError and OnServerError in your network manager

## Documentation
Check out our [Wiki](https://github.com/vis2k/Mirror/wiki) and read the [UNET Manual](https://docs.unity3d.com/Manual/UNet.html), Mirror is still similar enough.

The main difference is that you have to use `using Mirror;` instead of `using UnityEngine.Networking;` at the top of your scripts.

_Oh, and you won't have to worry about channels, low level networking, [packet loss](https://forum.unity.com/threads/unet-deprecation-thread.543501/page-3#post-3597869), [lack of support](https://forum.unity.com/threads/is-hlapi-dead.517436/) or [bugs](https://issuetracker.unity3d.com/issues/unet-networkwriter-dot-write-causing-readstring-slash-readbytes-out-of-range-errors-in-clients) ever again. Mirror just works._

## Usage Guide

This branch is to be imported in source form in Unity 2018.2 or later.  To install it:

1. Clone this repository or download as a zip file
2. Add all files to your unity project
3. Go to player settings and set your scripting runtime version to `.Net 4.x Equivalent`
3. Restart unity

## Migration Guide
If you are still using UNET and want to switch to Mirror, you should check out our [Migration Guide](https://github.com/vis2k/Mirror/blob/mirror/migration.md). Don't panic, it's very easy and won't take more than 5 minutes.

## Example Projects
Download Mirror from the [Asset Store](https://www.assetstore.unity3d.com/#!/content/129321), we have several small example projects included.

For a fully polished complete project example, consider [uMMORPG](https://www.assetstore.unity3d.com/#!/content/51212) or [uSurvival](https://www.assetstore.unity3d.com/#!/content/95015).

## Community Transports
If you don't want to use Telepathy or UNET's LLAPI as low level transport, then check out:
* https://github.com/FizzCube/SteamNetNetworkTransport (SteamNetwork)
* https://github.com/SoftwareGuy/Ignorance/ (ENet)

Note those transports have not been ported to 2018 branch.

## Donations
Mirror is developed by volunteers. If you like what we are doing, consider leaving [a small donation](https://www.patreon.com/MirrorTelepathy).

## Build
Building Mirror yourself is very easy. Simply download the project, open it in Visual Studio or Rider, build it once for Release and once for Release-Editor. You will then find all the necessary DLLs in the Output directory.

## Benchmarks
* Telepathy [1000 connections](https://github.com/vis2k/Telepathy) test
* [uMMORPG 207 CCU worst case test](https://docs.google.com/document/d/e/2PACX-1vQqf_iqOLlBRTUqqyor_OUx_rHlYx-SYvZWMvHGuLIuRuxJ-qX3s8JzrrBB5vxDdGfl-HhYZW3g5lLW/pub#h.h4wha2mpetsc) (everyone broadcasting to everyone else)
* [uSurvival 122 CCU worst case test](https://docs.google.com/document/d/e/2PACX-1vT28FcGXYlbG8gwi8DhD914n7K-wCAE8qhfetPkSli96ikc1Td3zJO1IiwVhfPVtKUHF0l3N7ZkM5GU/pub#h.pwbvffnwcewe)

## Contributing
If you like to contribute, feel free to submit pull requests and visit our [Discord Server](https://discordapp.com/invite/N9QVxbM).

We follow the [KISS](https://en.wikipedia.org/wiki/KISS_principle) principle, so make sure that your Pull Requests contain no magic.

We need Mirror to be MMO Scale. Bug fixes are always highly appreciated. New features will be considered very carefully. 
