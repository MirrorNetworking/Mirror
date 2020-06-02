![Mirror Logo](https://i.imgur.com/ikP9eYs.png)

[![Download](https://img.shields.io/badge/asset_store-brightgreen.svg)](https://assetstore.unity.com/packages/tools/network/mirror-129321)
[![Documentation](https://img.shields.io/badge/documentation-brightgreen.svg)](https://mirror-networking.com/docs)
[![Video Tutorial](https://img.shields.io/badge/video_tutorial-brightgreen.svg)](https://www.youtube.com/playlist?list=PLkx8oFug638oBYF5EOwsSS-gOVBXj1dkP)
[![Forum](https://img.shields.io/badge/forum-brightgreen.svg)](https://forum.unity.com/threads/mirror-networking-for-unity-aka-hlapi-community-edition.425437/)
[![Build status](https://img.shields.io/appveyor/ci/vis2k73562/hlapi-community-edition/Mirror.svg)](https://ci.appveyor.com/project/vis2k73562/hlapi-community-edition/branch/mirror)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=vis2k_Mirror&metric=coverage)](https://sonarcloud.io/dashboard?id=vis2k_Mirror)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=vis2k_Mirror&metric=ncloc)](https://sonarcloud.io/dashboard?id=vis2k_Mirror)
[![Discord](https://img.shields.io/discord/343440455738064897.svg)](https://discordapp.com/invite/N9QVxbM)
[![release](https://img.shields.io/github/release/vis2k/Mirror.svg)](https://github.com/vis2k/Mirror/releases/latest)


Mirror is a **high level** Networking API for Unity, built on top of the **low level** [Telepathy](https://github.com/vis2k/Telepathy) library.

Mirror is built [and tested](https://www.youtube.com/watch?v=mDCNff1S9ZU) for **MMO Scale** Networking by the developers of [uMMORPG](https://assetstore.unity.com/packages/templates/systems/ummorpg-51212), [uSurvival](https://assetstore.unity.com/packages/templates/systems/usurvival-95015) and [Cubica](https://cubica.net).

Mirror is optimized for **ease of use** and **probability of success**. Projects that use Mirror are small, concise and maintainable. uMMORPG was possible with <6000 lines of code. We needed a networking library that allows us to [launch our games](https://mirror-networking.com/showcase/), period.

With Mirror, the **Server & Client are ONE** project _(hence the name)_. Instead of having one code base for the server and one for the client, we simply use the same code for both of them.
* `[Server]` / `[Client]` tags can be used for the server-only and client-only parts.
* `[Command]`'s are used for Client->Server, and `[ClientRpc]` / `[TargetRpc]` for Server->Client communication.
* `[SyncVar]`'s and `SyncList`'s are used to automatically synchronize state.

What previously required **10.000** lines of code, now takes **1.000** lines of code. Therein lies the **magic of Mirror**.

_Note: Mirror is based on Unity's abandoned UNET Networking system. We fixed it up and pushed it to MMO Scale._

## Documentation
Check out our [Documentation](https://mirror-networking.com/docs/).

If you are migrating from UNET, then please check out our [Migration Guide](https://mirror-networking.com/docs/General/Migration.html). Don't panic, it's very easy and won't take more than 5 minutes.

## Installation
We **recommend** to download the most **stable Mirror version** from the [Asset Store](https://assetstore.unity.com/packages/tools/network/mirror-129321)!

For the latest improvements, [Download Mirror](https://github.com/vis2k/Mirror/releases) directly from this repository and extract it in your Assets folder.

## Examples
We included several smaller example projects in Mirror.

For a fully polished, complete project example, consider [uMMORPG](https://assetstore.unity.com/packages/templates/systems/ummorpg-components-edition-159401) or [uSurvival](https://assetstore.unity.com/packages/templates/systems/usurvival-95015).

## Transports
Mirror supports many different low level networking transports:

* (built in) https://github.com/vis2k/Telepathy (Telepathy)
* (built in) Unity's LLAPI
* (built in) https://github.com/ninjasource/Ninja.WebSockets (Websockets)
* https://mirror-networking.com/apathy/ (Apathy)
* https://github.com/Raystorms/FizzySteamyMirror/ (SteamNetwork)
* https://github.com/SoftwareGuy/Ignorance/ (ENet)
* https://github.com/MichalPetryka/LiteNetLib4Mirror (LiteNetLib)

## Donations & Priority Support
Please support [Mirror on GitHub](https://github.com/sponsors/vis2k). Priority support included!

## Benchmarks
* Telepathy [1000 connections](https://github.com/vis2k/Telepathy) test
* [uMMORPG 480 CCU worst case test](https://youtu.be/mDCNff1S9ZU) (everyone broadcasting to everyone else)
* [uSurvival 122 CCU worst case test](https://docs.google.com/document/d/e/2PACX-1vT28FcGXYlbG8gwi8DhD914n7K-wCAE8qhfetPkSli96ikc1Td3zJO1IiwVhfPVtKUHF0l3N7ZkM5GU/pub#h.pwbvffnwcewe)

## The Mirror Mantra
> _“Would you tell me, please, which way I ought to go from here?”_<br/>
> _“That depends a good deal on where you want to get to,"_ said the Cat.<br/>
> _"I don’t much care where—“_ said Alice.<br/>
> _“Then it doesn’t matter which way you go,”_ said the Cat.<br/>
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;- Alice in Wonderland _(Lewis Carroll)_

As Indie Developers, **our #1 goal** for Mirror is **Ease of Use**.

If you want to contribute, always follow the [KISS](https://en.wikipedia.org/wiki/KISS_principle) principle. Everything needs to be **as simple as possible**. As result, the code will be stable, easy to understand, easy to modify and easy to maintain 5 years from now when our games are still running.

Pull Requests for bug fixes are always highly appreciated. New features will be considered very carefully and will only be merged if they are the most simple solution to the given problem.
