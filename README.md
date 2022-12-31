![Mirror Logo](https://user-images.githubusercontent.com/16416509/119120944-6db26780-ba5f-11eb-9cdd-fc8500207f4d.png)

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

## Mirror Networking 
The **#1** free **open source** game networking library for **Unity 2019 / 2020 / 2021**.

Used **in production** by major hits like [**Population: ONE**](https://www.populationonevr.com/) and many [more](#made-with-mirror).

Originally based on [**UNET**](https://blog.unity.com/technology/announcing-unet-new-unity-multiplayer-technology): battle tested **since 2014** for 8 years and counting!

Mirror is **[stable](https://mirror-networking.gitbook.io/docs/general/tests)**, [**modular**](#low-level-transports) & **[easy to use](https://mirror-networking.gitbook.io/)** for all types of games, even small [MMORPGs](#made-with-mirror) 🎮.

**Made in 🇩🇪🇺🇸🇬🇧🇸🇬🇹🇼 with ❤️**.

---
## Architecture
The **Server & Client** are **ONE project** in order to achieve maximum productivity.

Simply use **NetworkBehaviour** instead of **MonoBehaviour**.

Making multiplayer games this way is fun & easy:

```cs
public class Player : NetworkBehaviour
{
    // synced automatically
    [SyncVar] public int health = 100;
    
    // lists, dictionaries, sets too
    SyncList<Item> inventory = new SyncList<Item>();
    
    // server/client-only code
    [Server] void LevelUp() {}
    [Client] void Animate() {}
    
    void Update()
    {
        // isServer/isClient for runtime checks
        if (isServer) Heal();
        if (isClient) Move();
    }
    
    // zero overhead remote calls
    [Command]   void CmdUseItem(int slot) {} // client to server
    [Rpc]       void RpcRespawn() {}         // server to all clients
    [TargetRpc] void Hello() {}              // server to one client
}
```

There's also **NetworkServer** & **NetworkClient**. And that's about it 🤩.

---
## Free, Open & Community Funded
Mirror is **free & open source** (MIT Licensed).

"Free" as in free beer, and freedom to use it any way you like.
 
- Run [Dedicated Servers](https://mirror-networking.gitbook.io/docs/guides/server-hosting) anywhere.
- Free player hosted games thanks to [Epic Relay](https://github.com/FakeByte/EpicOnlineTransport)!

Mirror is funded by [**Donations**](https://github.com/sponsors/vis2k) from our [fantastic community](https://discordapp.com/invite/N9QVxbM) of over 14,000 users!

<img src="https://user-images.githubusercontent.com/16416509/195067704-5577b581-b829-4c9f-80d0-b6270a3a59e7.png" title="Fitzcarraldo"/>

_The top quote is from Fitzcarraldo, which is quite reminiscent of this project._

---
## Getting Started
Get **Unity 2019 / 2020 / 2021 LTS**, [Download Mirror](https://assetstore.unity.com/packages/tools/network/mirror-129321), open one of the examples & press Play!

Check out our [Documentation](https://mirror-networking.gitbook.io/) to learn how it all works.

If you are migrating from UNET, then please check out our [Migration Guide](https://mirror-networking.gitbook.io/docs/general/migration-guide).

---
## Mirror LTS (Long Term Support)
Mirror LTS is available on the [Asset Store](https://assetstore.unity.com/packages/tools/network/mirror-lts-102631).

Mirror LTS gives you peace of mind to run your game in production.
Without any breaking changes, ever!

* **Bug fixes** only. 
* **Consistent API**: update any time, without any breaking features.
* Lives along side **Unity 2019/2020/2021** LTS.
* Supported for two years at a time.

---
## Made with Mirror
### [Population: ONE](https://www.populationonevr.com/)
[![Population: ONE](https://user-images.githubusercontent.com/16416509/178141286-9494c3a8-a4a5-4b06-af2b-b05b66162201.png)](https://www.populationonevr.com/)
The [BigBoxVR](https://www.bigboxvr.com/) team started using Mirror in February 2019 for what eventually became one of the most popular Oculus Rift games.

In addition to [24/7 support](https://github.com/sponsors/vis2k) from the Mirror team, BigBoxVR also hired one of our engineers.

**Population: ONE** was [acquired by Facebook](https://uploadvr.com/population-one-facebook-bigbox-acquire/) in June 2021, and they've just released a new [Sandbox](https://www.youtube.com/watch?v=jcI0h8dn9tA) addon in 2022!

### [Nimoyd](https://www.nimoyd.com/)
[![nimoyd_smaller](https://user-images.githubusercontent.com/16416509/178142672-340bac2c-628a-4610-bbf1-8f718cb5b033.jpg)](https://www.nimoyd.com/)
Nudge Nudge Games' first title: the colorful, post-apocalyptic open world sandbox game [Nimoyd](https://store.steampowered.com/app/1313210/Nimoyd__Survival_Sandbox/) is being developed with Mirror.

_Soon to be released for PC & mobile!_

### [Dinkum](https://store.steampowered.com/app/1062520/Dinkum/)
[![dinkum](https://user-images.githubusercontent.com/16416509/180051810-50c9ebfd-973b-4f2f-8448-d599443d9ce3.jpg)](https://store.steampowered.com/app/1062520/Dinkum/)
Set in the Australian Outback, Dinkum is a relaxing farming & survival game. Made by just one developer, Dinkum already reached 1000+ "Overwhelmingly Positive" reviews 1 week after its early access release. 

James Bendon initially made the game with UNET, and then [switched to Mirror](https://www.playdinkum.com/blog/2019/1/11/devlog-13-biomes-and-traps) in 2019.

### [A Glimpse of Luna](https://www.glimpse-luna.com/)
[![a glimpse of luna](https://user-images.githubusercontent.com/16416509/178148229-5b619655-055a-4583-a1d3-18455bde631f.jpg)](https://www.glimpse-luna.com/)
[A Glimpse of Luna](https://www.glimpse-luna.com/) - a tactical multiplayer card battle game with the most beautiful concept art & soundtrack.

Made with Mirror by two brothers with [no prior game development](https://www.youtube.com/watch?v=5J2wj8l4pFA&start=12) experience.

### [Sun Haven](https://store.steampowered.com/app/1432860/Sun_Haven/)
[![sun haven](https://user-images.githubusercontent.com/16416509/185836661-2bfd6cd0-523a-4af4-bac7-c202ed01de7d.jpg)](https://store.steampowered.com/app/1432860/Sun_Haven/)
[Sun Haven](https://store.steampowered.com/app/1432860/Sun_Haven/) - A beautiful human town, a hidden elven village, and a monster city filled with farming, magic, dragons, and adventure.

After their successful [Kickstarter](https://www.kickstarter.com/projects/sunhaven/sunhaven/description), Sun Haven was released on Steam in 2021 and later on ported to Mirror in 2022.

### [Inferna](https://inferna.net/)
[![Inferna MMORPG](https://user-images.githubusercontent.com/16416509/178148768-5ba9ea5b-bcf1-4ace-ad7e-591f2185cbd5.jpg)](https://inferna.net/)
One of the first MMORPGs made with Mirror, released in 2019.

An open world experience with over 1000 CCU during its peak, spread across multiple server instances.

### [Samutale](https://www.samutale.com/)
[![samutale](https://user-images.githubusercontent.com/16416509/178149040-b54e0fa1-3c41-4925-8428-efd0526f8d44.jpg)](https://www.samutale.com/)
A sandbox survival samurai MMORPG, originally released in September 2016.

Later on, the Netherlands based Maple Media switched their netcode to Mirror.

### [Untamed Isles](https://store.steampowered.com/app/1823300/Untamed_Isles/)
[![Untamed Isles](https://user-images.githubusercontent.com/16416509/178143679-1c325b54-0938-4e84-97b6-b59db62a51e7.jpg)](https://store.steampowered.com/app/1823300/Untamed_Isles/)
The turn based, monster taming **MMORPG** [Untamed Isles](https://store.steampowered.com/app/1823300/Untamed_Isles/) is currently being developed by [Phat Loot Studios](https://untamedisles.com/about/).

After their successful [Kickstarter](https://www.kickstarter.com/projects/untamedisles/untamed-isles), the New Zealand based studio is aiming for a 2022 release date.

### [Zooba](https://play.google.com/store/apps/details?id=com.wildlife.games.battle.royale.free.zooba&gl=US)
[![Zooba](https://user-images.githubusercontent.com/16416509/178141846-60805ad5-5a6e-4840-8744-5194756c2a6d.jpg)](https://play.google.com/store/apps/details?id=com.wildlife.games.battle.royale.free.zooba&gl=US)
[Wildlife Studio's](https://wildlifestudios.com/) hit Zooba made it to rank #5 of the largest battle royal shooters in the U.S. mobile market.

The game has over **50 million** downloads on [Google Play](https://play.google.com/store/apps/details?id=com.wildlife.games.battle.royale.free.zooba&gl=US), with Wildlife Studios as one of the top 10 largest mobile gaming companies in the world.

### [Portals](https://theportal.to/)
[![Portals](https://user-images.githubusercontent.com/9826063/209373815-8e6288ba-22fc-4cee-8867-19f587188827.png)](https://theportal.to/)
Animal Crossing meets Yakuza meets Minecraft — a city builder with a multiplayer central hub. Gather, trade and build — all in the browser!

### [SCP: Secret Laboratory](https://scpslgame.com/)
[![scp - secret laboratory_smaller](https://user-images.githubusercontent.com/16416509/178142224-413b3455-cdff-472e-b918-4246631af12f.jpg)](https://scpslgame.com/)
[Northwood Studios'](https://store.steampowered.com/developer/NWStudios/about/) first title: the multiplayer horror game SCP: Secret Laboratory was one of Mirror's early adopters.

Released in December 2017, today it has more than **140,000** reviews on [Steam](https://store.steampowered.com/app/700330/SCP_Secret_Laboratory/?curator_clanid=33782778).

### [Naïca Online](https://naicaonline.com/)
[![Naica Online](https://user-images.githubusercontent.com/16416509/178147710-8ed83bbd-1bce-4e14-8465-edfb40af7c7f.png)](https://naicaonline.com/)
[Naïca](https://naicaonline.com/) is a beautiful, free to play 2D pixel art MMORPG.

The [France based team](https://naicaonline.com/en/news/view/1) was one of Mirror's early adopters, releasing their first public beta in November 2020.

### [Laurum Online](https://laurum.online/)
[![Laurum Online](https://user-images.githubusercontent.com/16416509/178149616-3852d198-6fc9-44d5-9f63-da4e52f5546a.jpg)](https://laurum.online/)
[Laurum Online](https://play.google.com/store/apps/details?id=com.project7.project7beta) - a 2D retro mobile MMORPG with over 500,000 downloads on Google Play.

### [Empires Mobile](https://knightempire.online/)
[![Empires Mobile](https://user-images.githubusercontent.com/16416509/207028553-c646f12c-c164-47d3-a1fc-ff79409c04fa.jpg)](https://knightempire.online/)
[Empires Mobile](https://knightempire.online/) - Retro mobile MMORPG for Android and iOS, reaching 5000 CCU at times. Check out their [video](https://www.youtube.com/watch?v=v69lW9aWb-w) for some _early MMORPG_ nostalgia.

### [Castaways](https://www.castaways.com/)
[![Castaways](https://user-images.githubusercontent.com/16416509/207313082-e6b95590-80c6-4685-b0d1-f1c39c236316.png)](https://www.castaways.com/)
[Castaways](https://www.castaways.com/) is a sandbox game where you are castaway to a small remote island where you must work with others to survive and build a thriving new civilization. 

Castaway runs in the Browser, thanks to Mirror's WebGL support.

### And many more...
<a href="https://store.steampowered.com/app/719200/The_Wall/"><img src="https://cdn.akamai.steamstatic.com/steam/apps/719200/header.jpg?t=1588105839" height="100" title="The wall"/></a>
<a href="https://store.steampowered.com/app/535630/One_More_Night/"><img src="https://cdn.akamai.steamstatic.com/steam/apps/535630/header.jpg?t=1584831320" height="100" title="One more night"/></a>
<img src="https://i.ytimg.com/vi/D_f_MntrLVE/maxresdefault.jpg" height="100" title="Block story"/>
<a href="https://nightz.io"><img src="https://user-images.githubusercontent.com/16416509/130729336-9c4e95d9-69bc-4410-b894-b2677159a472.jpg" height="100" title="Nightz.io"/></a>
<a href="https://store.steampowered.com/app/1016030/Wawa_United/"><img src="https://user-images.githubusercontent.com/16416509/162982300-c29d89bc-210a-43ef-8cce-6e5555bb09bc.png" height="100" title="Wawa united"/></a>
<a href="https://store.steampowered.com/app/1745640/MACE_Mapinguaris_Temple/"><img src="https://user-images.githubusercontent.com/16416509/166089837-bbecf190-0f06-4c88-910d-1ce87e2f171d.png" title="MACE" height="100"/></a>
<a href="https://www.adversator.com/"><img src="https://user-images.githubusercontent.com/16416509/178641128-37dc270c-bedf-4891-8284-33573d1776b9.jpg" title="Adversator" height="100"/></a>
<a href="https://store.steampowered.com/app/670260/Solace_Crafting/"><img src="https://user-images.githubusercontent.com/16416509/197175819-1c2720b6-97e6-4844-80b5-2197a7f22839.png" title="Solace Crafting" height="100"/></a>
<a href="https://www.unitystation.org"><img src="https://user-images.githubusercontent.com/57072365/204021428-0c621067-d580-4c88-b551-3ac70f9da39d.jpg" title="UnityStation" height="100"/></a>

## Modular Transports
Mirror uses **KCP** (reliable UDP) by default, but you may use any of our community transports for low level packet sending:
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
* [2022] mischa [400-800 CCU](https://discord.com/channels/343440455738064897/1007519701603205150/1019879180592238603) tests
* [2021] [Jesus' Benchmarks](https://docs.google.com/document/d/1GMxcWAz3ePt3RioK8k4erpVSpujMkYje4scOuPwM8Ug/edit?usp=sharing)
* [2019] [uMMORPG 480 CCU](https://youtu.be/mDCNff1S9ZU) (worst case)

## Development & Contributing
Mirror is used **in production** by everything from small indie projects to million dollar funded games that will run for a decade or more.

We prefer to work slow & thoroughly in order to not break everyone's games 🐌.

Therefore, we need to [KISS](https://en.wikipedia.org/wiki/KISS_principle) 😗.

---
# Bug Bounty
<img src="https://user-images.githubusercontent.com/16416509/110572995-718b5900-8195-11eb-802c-235c82a03bf7.png">

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

---
# Credits & Thanks 🙏
🪞 **Alexey Abramychev** (UNET)<br/>
🪞 **Alan**<br/>
🪞 **c6burns** <br/>
🪞 **Coburn** <br/>
🪞 **cooper** <br/>
🪞 **FakeByte** <br/>
🪞 **fholm**<br/>
🪞 **Gabe** (BigBoxVR)<br/>
🪞 **imer** <br/>
🪞 **James Frowen** <br/>
🪞 **JesusLuvsYooh** <br/>
🪞 **Mischa** <br/>
🪞 **Mr. Gadget**<br/>
🪞 **NinjaKickja** <br/>
🪞 **Paul Pacheco**<br/>
🪞 **Sean Riley** (UNET)<br/>


