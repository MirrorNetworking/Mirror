![mMirror Logo](https://user-images.githubusercontent.com/16416509/119120944-6db26780-ba5f-11eb-9cdd-fc8500207f4d.png)

[![Download](https://img.shields.io/badge/asset_store-brightgreen.svg)](https://assetstore.unity.com/packages/tools/network/mirror-129321)
[![Documentation](https://img.shields.io/badge/docs-brightgreen.svg)](https://mirror-networking.gitbook.io/)
[![Showcase](https://img.shields.io/badge/showcase-brightgreen.svg)](https://mirror-networking.com/showcase/)
[![Video Tutorials](https://img.shields.io/badge/video_tutorial-brightgreen.svg)](https://mirror-networking.gitbook.io/docs/community-guides/video-tutorials)
[![Forum](https://img.shields.io/badge/forum-brightgreen.svg)](https://forum.unity.com/threads/mirror-networking-for-unity-aka-hlapi-community-edition.425437/)
[![Build](https://img.shields.io/appveyor/ci/vis2k73562/hlapi-community-edition/Mirror.svg)](https://ci.appveyor.com/project/vis2k73562/hlapi-community-edition/branch/mirror)
[![Discord](https://img.shields.io/discord/343440455738064897.svg)](https://discordapp.com/invite/N9QVxbM)
[![release](https://img.shields.io/github/release/vis2k/Mirror.svg)](https://github.com/vis2k/Mirror/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-brightgreen.svg)](https://github.com/vis2k/Mirror/blob/master/LICENSE)
[![Roadmap](https://img.shields.io/badge/roadmap-blue.svg)](https://trello.com/b/fgAE7Tud)

**It's only the dreamers who ever move mountains.**

<img src="https://user-images.githubusercontent.com/16416509/119117854-3e4e2b80-ba5c-11eb-8236-ce6cfd2b6b07.png" title="Original Concept Art for Games that made us dream. Copyright Blizzard, Blizzard, Riot Games, Joymax in that order."/>

## Mirror
Mirror is a **high level** Networking library for **Unity 2019/2020 LTS**, compatible with different **low level** [Transports](https://github.com/vis2k/Mirror#low-level-transports).

Mirror is for indie games & small [MMOs](https://www.youtube.com/watch?v=mDCNff1S9ZU), made by the developers of [uMMORPG](https://assetstore.unity.com/packages/templates/systems/ummorpg-components-edition-159401) and [Cubica](https://www.youtube.com/watch?v=D_f_MntrLVE).

Mirror is optimized for **ease of use** & **probability of success**.

We needed a networking library that allows us to **[launch our games](https://mirror-networking.com/showcase/)** and **survive the next decade**.

Mirror is **[stable](https://mirror-networking.gitbook.io/docs/general/tests)** & **[production](https://www.oculus.com/experiences/quest/2564158073609422/)** ready.

## Free & Open
Mirror is **free & open source**!
* Code: MIT licensed.
* Dedicated Servers: [Anywhere](https://mirror-networking.gitbook.io/docs/guides/server-hosting)!
* Player Hosted: [Free Epic Relay](https://github.com/FakeByte/EpicOnlineTransport)!

We need Mirror for our own games, which is why we will never charge anything. 

Funded only by [Donations](https://github.com/sponsors/vis2k) from our [fantastic community](https://discordapp.com/invite/N9QVxbM) of over 10,000 people.

## Architecture
The **Server & Client** are **ONE project** in order to achieve an order of magnitude gain in productivity.

Making multiplayer games this way is fun & easy. Instead of MonoBehaviour, Mirror provides **NetworkBehaviour** components with:
* **[Server]** / **[Client]** tags for server-only / client-only code
* **[Command]** for Client->Server function calls (e.g. UseItem)
* **[ClientRpc]** / **[TargetRpc]** for Server->Client function calls (e.g. AddChatMessage)
* **[SyncVar]** / SyncList to automatically synchronize variables from Server->Client

## Getting Started
Get **Unity 2019 LTS**, download [Mirror on the Asset Store](https://assetstore.unity.com/packages/tools/network/mirror-129321), open one of the examples & press Play!

Check out our [Documentation](https://mirror-networking.gitbook.io/) to learn how it all works.

If you are migrating from UNET, then please check out our [Migration Guide](https://mirror-networking.gitbook.io/docs/general/migration-guide).

## Made with Mirror
<table align="center">
  <tr>
    <th><a href="http://www.populationonevr.com/">Population: ONE</a></th>
    <th><a href="https://wildlifestudios.com/games/zooba/">Zooba</a></th>
    <th><a href="https://store.steampowered.com/app/700330/SCP_Secret_Laboratory/">SCP: Secret Laboratory</a></th>
    <th><a href="https://naicaonline.com/">Naïca Online</a></th>
  </tr>
  <tr>
    <td><img src="https://user-images.githubusercontent.com/16416509/119758937-f145db80-bed9-11eb-9512-0ef46eb899e7.jpg" height="100"/></td>
    <td><img src="https://user-images.githubusercontent.com/16416509/119125684-ac96ec00-ba64-11eb-9c0c-c6595e00dec8.png" height="100"/></td>
    <td><img src="https://steamcdn-a.akamaihd.net/steam/apps/700330/header.jpg?t=1604668607" height="100"/></td>
    <td><img src="https://i.imgur.com/VrBqvtz.png" height="100"/></td>
  </tr>
  <tr>
    <th><a href="https://laurum.online/">Laurum Online</a></th>
    <th><a href="https://www.samutale.com/">SamuTale</a></th>
    <th><a href="https://store.steampowered.com/app/1313210/Nimoyd__Survival_Sandbox/">Nimoyd</a></th>
    <th><a href="https://store.steampowered.com/app/719200/The_Wall/">The Wall</a></th>
  </tr>
  <tr>
    <td><img src="https://camo.githubusercontent.com/6d50af6cbe0fcfc465f444f75475a356c6c14b4a3a9534156cfdd578e7d45a9f/68747470733a2f2f692e696d6775722e636f6d2f324938776e784f2e706e67" height="100"/></td>
    <td><img src="https://user-images.githubusercontent.com/16416509/119759544-07a06700-bedb-11eb-9754-97c3e8f50b0e.jpg" height="100"/></td>
    <td><img src="https://cdn.akamai.steamstatic.com/steam/apps/1313210/header.jpg?t=1616227358" height="100"/></td>
    <td><img src="https://cdn.akamai.steamstatic.com/steam/apps/719200/header.jpg?t=1588105839" height="100"/></td>
  </tr>
  <tr>
    <th><a href="https://nestables.co/">Nestables</a></th>
    <th><a href="https://www.glimpse-luna.com/">A Glimpse of Luna</a></th>
    <th><a href="https://store.steampowered.com/app/535630/One_More_Night/">One More Night</a></th>
    <th><a href="">Cubica</a></th>
  </tr>
  <tr>
    <td><img src="https://user-images.githubusercontent.com/16416509/119001349-7a32b380-b9be-11eb-86fd-a116920842d1.png" height="100"/></td>
    <td><img src="https://user-images.githubusercontent.com/16416509/119001595-b0703300-b9be-11eb-9e40-6542113dc1a2.png" height="100"/></td>
    <td><img src="https://cdn.akamai.steamstatic.com/steam/apps/535630/header.jpg?t=1584831320" height="100"/></td>
    <td><img src="https://i.ytimg.com/vi/D_f_MntrLVE/maxresdefault.jpg" height="100"/></td>
  </tr>
  <tr>
    <th><a href="https://inferna.net">Inferna</a></th>
    <th><a href="https://nightz.io">NightZ</a></th>
    <th><a href="https://store.steampowered.com/app/1547790/Roze_Blud">Roze Blud</a></th>
    <th><a href="https://store.steampowered.com/app/1016030/Wawa_United/">Wawa United</a></th>
  </tr>
  <tr>
    <td><img src="https://user-images.githubusercontent.com/16416509/119760092-f3109e80-bedb-11eb-96cd-8e7f52e483fc.png" height="100"/></td>
    <td><img src="https://user-images.githubusercontent.com/16416509/130729336-9c4e95d9-69bc-4410-b894-b2677159a472.jpg" height="100"/></td>
    <td><img src="https://user-images.githubusercontent.com/16416509/152281763-87ae700e-9648-4335-9b20-3247e09334b5.png" height="100"/></td>
    <td><img src="https://user-images.githubusercontent.com/16416509/162982300-c29d89bc-210a-43ef-8cce-6e5555bb09bc.png" height="100"/></td>
  </tr>
</table>

And [many more](https://mirror-networking.com/showcase/)...

## Mirror LTS (Long Term Support)

If you use Mirror in production, consider Mirror LTS!
* **Bug fixes** only. 
* **Consistent API**: update any time, without any breaking features.
* Lives along side **Unity 2019** LTS.
* Supported from Sept. 2021 to Sept 2022, depending on feedback.

**Mirror V46 LTS** is available to all [GitHub Sponsors](https://github.com/sponsors/vis2k).

All sponsors are invited to the [Mirror V46 LTS Repository](https://github.com/MirrorNetworking/Mirror-46-LTS) automatically.

## Low Level Transports
* (built in) [KCP](https://github.com/vis2k/kcp2k): reliable UDP
* (built in) [Telepathy](https://github.com/vis2k/Telepathy): TCP
* (built in) [Websockets](https://github.com/MirrorNetworking/SimpleWebTransport): Websockets
* [Ignorance](https://github.com/SoftwareGuy/Ignorance/): ENET UDP
* [LiteNetLib](https://github.com/MirrorNetworking/LiteNetLibTransport/) UDP
* [FizzySteam](https://github.com/Chykary/FizzySteamworks/): SteamNetwork
* [FizzyFacepunch](https://github.com/Chykary/FizzyFacepunch/): SteamNetwork
* [Epic Relay](https://github.com/FakeByte/EpicOnlineTransport): Epic Online Services
* [Bubble](https://github.com/Squaresweets/BubbleTransport): Apple GameCenter
* [Light Reflective Mirror](https://github.com/Derek-R-S/Light-Reflective-Mirror): Self-Hosted Relay
* [Oculus P2P](https://github.com/hyferg/MirrorOculusP2P): Oculus Platform Service

## Benchmarks
* [uMMORPG 480 CCU](https://youtu.be/mDCNff1S9ZU) (worst case)
* [Jesus' Benchmarks](https://docs.google.com/document/d/1GMxcWAz3ePt3RioK8k4erpVSpujMkYje4scOuPwM8Ug/edit?usp=sharing)

## Development & Contributing
Mirror is used **in production** by everything from small indie projects to million dollar funded games that will run for a decade or more.

Therefore it is important for us to follow the [KISS principle](https://en.wikipedia.org/wiki/KISS_principle) in order for our games to survive, so that we can still fix networking bugs 10 years from now if needed.


# Bug Bounty
<img src="https://user-images.githubusercontent.com/16416509/110572995-718b5900-8195-11eb-802c-235c82a03bf7.png" height="150">

A lot of projects use Mirror in production. If you found a critical bug / exploit in Mirror core, please reach out to us in private.
Depending on the severity of the exploit, we offer $50 - $500 for now.
Rewards based on Mirror's [donations](https://github.com/sponsors/vis2k), capped at amount of donations we received that month.

**Specifically we are looking for:**
* Ways to crash a Mirror server
* Ways to exploit a Mirror server
* Ways to leave a Mirror server in undefined state

We are **not** looking for DOS/DDOS attacks. The exploit should be possible with just a couple of network packets, and it should be reproducible.

**Credits / past findings / fixes:**
* 2020, fholm: fuzzing ConnectMessage to stop further connects [[#2397](https://github.com/vis2k/Mirror/pull/2397)]
