![Mirror Logo](https://i.imgur.com/we6li1x.png)

[![Download](https://img.shields.io/badge/asset_store-brightgreen.svg)](https://assetstore.unity.com/packages/tools/network/mirror-129321)
[![Documentation](https://img.shields.io/badge/docs-brightgreen.svg)](https://mirror-networking.com/docs)
[![Showcase](https://img.shields.io/badge/showcase-brightgreen.svg)](https://mirror-networking.com/showcase/)
[![Video Tutorial](https://img.shields.io/badge/video_tutorial-brightgreen.svg)](https://www.youtube.com/playlist?list=PLkx8oFug638oBYF5EOwsSS-gOVBXj1dkP)
[![Forum](https://img.shields.io/badge/forum-brightgreen.svg)](https://forum.unity.com/threads/mirror-networking-for-unity-aka-hlapi-community-edition.425437/)
[![Build](https://img.shields.io/appveyor/ci/vis2k73562/hlapi-community-edition/Mirror.svg)](https://ci.appveyor.com/project/vis2k73562/hlapi-community-edition/branch/mirror)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=vis2k_Mirror&metric=coverage)](https://sonarcloud.io/dashboard?id=vis2k_Mirror)
[![Discord](https://img.shields.io/discord/343440455738064897.svg)](https://discordapp.com/invite/N9QVxbM)
[![release](https://img.shields.io/github/release/vis2k/Mirror.svg)](https://github.com/vis2k/Mirror/releases/latest)

**Stop worrying about networking. Because we do.**

## Mirror
Mirror is a **high level** Networking library for Unity, compatible with different **low level** [Transports](https://github.com/vis2k/Mirror#low-level-transports).

Mirror is for small indie games & large scale [MMOs](https://www.youtube.com/watch?v=mDCNff1S9ZU), made by the developers of [uMMORPG](https://assetstore.unity.com/packages/templates/systems/ummorpg-components-edition-159401) and [Cubica](https://www.youtube.com/watch?v=D_f_MntrLVE).

Mirror is optimized for **ease of use** & **probability of success**. 

We needed a networking library that allows us to **[launch our games](https://mirror-networking.com/showcase/)** and **survive the next decade**.

## Architecture
The **Server & Client** are **ONE project** in order to achieve an order of magnitude gain in productivity.

Making multiplayer games this way is fun & easy. Instead of MonoBehaviour, Mirror provides **NetworkBehaviour** components with:
* **[Server]** / **[Client]** tags for server-only / client-only code
* **[Command]** for Client->Server function calls (e.g. UseItem)
* **[ClientRpc]** / **[TargetRpc]** for Server->Client function calls (e.g. AddChatMessage)
* **[SyncVar]** / SyncList to automatically synchronize variables from Server->Client

_Note: Mirror is based on Unity's abandoned UNET Networking system. We fixed it up and pushed it to MMO Scale._

## Low Level Transports
* (built in) [KCP](https://github.com/skywind3000/kcp): reliable UDP
* (built in) [Telepathy](https://github.com/vis2k/Telepathy): TCP
* (built in) [UNET LLAPI](https://github.com/SoftwareGuy/Mirror-UnityLLAPI): UDP
* (built in) [Ninja.Websockets](https://github.com/ninjasource/Ninja.WebSockets): Websockets
* Libuv: TCP powered by Node.js' native C networking
* [LiteNetLib](https://github.com/MirrorNetworking/LiteNetLibTransport/) UDP
* [Ignorance](https://github.com/SoftwareGuy/Ignorance/): ENET UDP
* [Apathy](https://mirror-networking.com/apathy/): Native TCP
* [FizzySteam](https://github.com/Raystorms/FizzySteamyMirror/): SteamNetwork

## Getting Started
Get **Unity 2018/2019 LTS**, download [Mirror on the Asset Store](https://assetstore.unity.com/packages/tools/network/mirror-129321), open one of the examples & press Play!

Check out our [Documentation](https://mirror-networking.com/docs/) to learn how it all works.

If you are migrating from UNET, then please check out our [Migration Guide](https://mirror-networking.com/docs/Articles/General/Migration.html).

## Made with Mirror
![Population: ONE](https://steamcdn-a.akamaihd.net/steam/apps/691260/header.jpg?t=1603846067)<br/>
[Population: ONE](http://www.populationonevr.com/)

![Zooba](https://i.imgur.com/4TY0XoY.png)<br/>
[Zooba](https://wildlifestudios.com/games/zooba/)

![SCP: Secret Laboratory](https://steamcdn-a.akamaihd.net/steam/apps/700330/header.jpg?t=1604668607)<br/>
[SCP: Secret Laboratory](https://store.steampowered.com/app/700330/SCP_Secret_Laboratory/)

![Naïca Online](https://i.imgur.com/VrBqvtz.png)<br/>
[Naïca Online](https://naicaonline.com/)

![Laurum Online](https://i.imgur.com/2I8wnxO.png)<br/>
[Laurum Online](https://laurum.online/)<br/>

## Funding
Mirror is free & open source software funded by Donations. If you love it, please consider supporting [Mirror on GitHub](https://github.com/sponsors/vis2k). As reward, you'll receive our [Network Profiler](https://mirror-networking.com/docs/Articles/Guides/Profiler.html?q=Profiler), priority support and more :)

## Benchmarks
* [uMMORPG 480 CCU worst case test](https://youtu.be/mDCNff1S9ZU) (everyone broadcasting to everyone else)

## Development & Contributing
Mirror is used **in production** by games ranging from small indie projects to large scale MMOs that will run for a decade or more.

10 years from now if your players encounter a networking bug and most of our contributors already moved on, someone will need to fix it. Therefore it is of utmost importance for us to follow the [KISS principle](https://en.wikipedia.org/wiki/KISS_principle) in order for our games to survive.

Keeping the next decade in mind, contributing **fixes** / **tests** / **improvements** is highly appreciated while new features have a low probability of being merged.

_At this point, what we don't add to Mirror is more important than what we do add to it!_
