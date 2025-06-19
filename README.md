![Mirror Logo](https://user-images.githubusercontent.com/16416509/119120944-6db26780-ba5f-11eb-9cdd-fc8500207f4d.png)
<p align="center">
<a href="https://assetstore.unity.com/packages/tools/network/mirror-129321"><img src="https://img.shields.io/badge/download-brightgreen.svg?style=for-the-badge&logo=unity&colorA=363a4f&colorB=f5a97f" alt="Download"></a>
<a href="https://github.com/MirrorNetworking/Mirror#made-with-mirror"><img src="https://img.shields.io/badge/showcase-brightgreen.svg?style=for-the-badge&logo=github&colorA=363a4f&colorB=f5a97f" alt="Showcase"></a>
<a href="https://mirror-networking.gitbook.io/"><img src="https://img.shields.io/badge/docs-brightgreen.svg?style=for-the-badge&logo=gitbook&logoColor=white&colorA=363a4f&colorB=f5a97f" alt="Documentation"></a>
<a href="https://forum.unity.com/threads/mirror-networking-for-unity-aka-hlapi-community-edition.425437/"><img src="https://img.shields.io/badge/forum-brightgreen.svg?style=for-the-badge&logo=unity&colorA=363a4f&colorB=f5a97f" alt="Forum"></a>
<a href="https://trello.com/b/fgAE7Tud"><img src="https://img.shields.io/badge/roadmap-brightgreen.svg?style=for-the-badge&logo=trello&colorA=363a4f&colorB=f5a97f" alt="Roadmap"></a>
<br>
<a href="https://github.com/vis2k/Mirror/blob/master/LICENSE"><img src="https://img.shields.io/badge/License-MIT-brightgreen.svg?style=for-the-badge&colorA=363a4f&colorB=b7bdf8" alt="License: MIT"></a>
<a href="https://ci.appveyor.com/project/vis2k73562/hlapi-community-edition/branch/mirror"><img src="https://img.shields.io/appveyor/ci/vis2k73562/hlapi-community-edition/Mirror.svg?style=for-the-badge&colorA=363a4f&colorB=b7bdf8" alt="Build"></a>
<a href="https://github.com/vis2k/Mirror/releases/latest"><img src="https://img.shields.io/github/release/vis2k/Mirror.svg?style=for-the-badge&colorA=363a4f&colorB=b7bdf8" alt="release"></a>
<a href="https://discordapp.com/invite/xVW4nU4C34"><img src="https://img.shields.io/discord/343440455738064897.svg?style=for-the-badge&colorA=363a4f&colorB=b7bdf8" alt="Discord"></a>
</p>

**It's only the dreamers who ever move mountains.**
![mmos_conceptart](https://github.com/user-attachments/assets/a95f2229-2f07-4c8c-9245-93a5e8004b7d)

## Mirror Networking 
The **#1** free **open source** game networking library for **Unity 2019 / 2020 / 2021 / 2022 / 6**.

Used **in production** by major hits like [**Population: ONE**](https://www.populationonevr.com/) and many [**more**](#made-with-mirror).

Originally based on [**UNET**](https://web.archive.org/web/20230915050929/https://blog.unity.com/technology/announcing-unet-new-unity-multiplayer-technology): battle tested **since 2014** for 10 years and counting!

Mirror is **[stable](https://mirror-networking.gitbook.io/docs/general/tests)**, [**modular**](#low-level-transports) & **[easy to use](https://mirror-networking.gitbook.io/)** for all types of games, even small [**MMORPGs**](#made-with-mirror) 🎮.

**Made in 🇩🇪🇺🇸🇬🇧🇸🇬🇹🇼 with ❤️**.

---
## Features

Mirror comes with a wide variety of features to support all game genres.<br>
Many of our features quickly became the norm across all Unity netcodes!<br>

| Feature                       | Description                                                                                                                                                   | Status          |
|-------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------|
| 🎛 **Transports**             | UDP, TCP, Websockets, Steam, Relay and more.                                                                                                                  | **Stable**      | 
| 🪜 **Interest Management**    | Spatial Hashing & Distance Checker to partition the world.                                                                                                    | **Stable**      |
| ↗️ **SyncDirection**          | Server & Client Authority - per component with one click.                                                                                                     | **Stable**      |
| 🐌 **Latency Simulation**     | Simulate latency, packet loss & jitter locally.                                                                                                               | **Stable**      |
| 🧲 **Batching**               | Minimize message overhead via batching automatically.                                                                                                         | **Stable**      |
| 💌 **RPCs & SyncVars**        | Synced vars and remote function calls built in & safe.                                                                                                        | **Stable**      |
| 🙅‍♀️ **Allocation Free**      | Free of runtime allocations and no GC (except Transports).                                                                                                    | **Stable**      |
| 🛞 **Transform & Physics**   | Transform & Physics sync built in.                                                                                                                            | **Stable**      |
| 👩‍🍼 **Child Components**    | Put networked components on anything.                                                                                                                         | **Stable**      |
| 🪚️ **IL Post Processing**    | Zero overhead [Rpcs] and [Commands] via IL post processing!                                                                                                   | **Stable**      |
| ☁️ **Two Click Hosting**      | (Optional) <a href="https://mirror-networking.gitbook.io/docs/hosting/edgegap-hosting-plugin-guide">Build & Push</a> directly from Unity Editor to the Cloud. | **Stable**     |
|                               |                                                                                                                                                               |                 |
| 📏 **Snapshot Interpolation**       | Perfectly smooth movement for all platforms & genres.                                                                                                    | **Stable**      |
| 🔫 **Lag Compensation**       | Roll back state to see what the player saw during input.                                                                                                      | **Beta**     |
| 🔒 **Encryption**             | Secure communication with end-to-end encryption.                                                                                                              | **Beta** |
| 🔒 **Cheat Detection**        | Mirror Guard safely detects Melon Loader & more.                                                                                                              | **Beta** |
| 🚀 **Unreliable Mode**       | Quake style Unreliable SyncMode for any component.                                                                                                      | **Development**     |
|                               |                                                                                                                                                               |                 |
| 🧙‍♂️ **General Purpose**     | Mirror supports all genres for all your games!                                                                                                                |                 |
| 🧘‍♀️ **Stable API**          | Long term (10 years)  stability instead of new versions!                                                                                                      |
| 🔬 **Battle Tested**          | Mirror serves over 100 million players. It just works!                                                                                                        |                 |
| 💴 **Free & Open Source**     | MIT licensed without any restrictions to minimize risk!                                                                                                       |                 |
| ❤️ **Community**              | Join our Discord with nearly 15.000 developers world wide!                                                                                                    |                 |
| 🧜🏻‍♀️ **Long Term Support** | Maintained since 2014 with optional LTS version!                                                                                                              |                 |
|                               |                                                                                                                                                               |                 |
| 📐 **Bitpacking**             | Optimized compression (bools as 1 bit etc.)                                                                                                                   | **Researching** |
| 🏎 **Prediction**             | Simulate Physics locally & apply server corrections.                                                                                                          | **Researching**        |

---
## Architecture
The **Server & Client** are **ONE project** in order to achieve maximum productivity.

Simply use **NetworkBehaviour** instead of **MonoBehaviour**.

Making multiplayer games this way is fun & easy:

```cs
public class Player : NetworkBehaviour
{
    // Synced automatically
    [SyncVar] public int health = 100;
    
    // Lists, Dictionaries, Sets too
    SyncList<Item> inventory = new SyncList<Item>();
    
    // Server/Client-only code
    [Server] void LevelUp() {}
    [Client] void Animate() {}
    
    void Update()
    {
        // isServer/isClient for runtime checks
        if (isServer) Heal();
        if (isClient) Move();
    }
    
    // Zero overhead remote calls
    [Command]   void CmdUseItem(int slot) {} // Client to Server
    [ClientRpc] void RpcRespawn() {}         // Server to all Clients
    [TargetRpc] void Hello() {}              // Server to one Client
}
```

There's also **NetworkServer** & **NetworkClient**.</br>
And that's about it 🤩

---
## Free, Open Source & Community Focused
**Mirror** is **free & open source** (MIT Licensed).

🍺 "**Free**" as in free beer, and freedom to use it any way you like.
 
- Host Game [Servers](https://mirror-networking.gitbook.io/docs/hosting/the-pragmatic-hosting-guide) anywhere!
- Customize anything freely!
- No paywalls, no CCU costs, no strings attached!

🤝 We are a team of **professional** game developers, who are paid to **use Mirror in production**. Our incentives will always align with the community, because we are Mirror users just like you! 

❤️ Our [**fantastic community**](https://discordapp.com/invite/xVW4nU4C34) of over **14,000** users contributes feedback & improvements every day. Please join us on our journey, help others, and consider a [**Donation**](https://github.com/sponsors/miwarnec) if you love our work!

<img src="https://user-images.githubusercontent.com/16416509/195067704-5577b581-b829-4c9f-80d0-b6270a3a59e7.png" title="Fitzcarraldo"/>

_The top quote is from Fitzcarraldo, which is quite reminiscent of this project._

---
## Getting Started
Get **Unity 2019 / 2020 / 2021 / 2022 LTS and 6000.1**, [Download Mirror](https://assetstore.unity.com/packages/tools/network/mirror-129321), open one of the examples & press Play!

Check out our [Documentation](https://mirror-networking.gitbook.io/) to learn how it all works.

If you are migrating from UNET, then please check out our [Migration Guide](https://mirror-networking.gitbook.io/docs/general/migration-guide).

---
## Guard - Anti Cheat 🔒
![2000x630](https://github.com/user-attachments/assets/34b5dce3-d137-4c36-b7d6-ebed62fadb7e)
Guard is a high impact, zero risk anti-cheat solution built specifically for Unity games. Unlike most commercial anti-cheats, Guard is embedded on the source code level and compiles with your project.

Guard is available on the [Asset Store](https://assetstore.unity.com/packages/tools/network/guard-multiplayer-anti-cheat-321434) and includes a Mirror Integration!

---
## Made with Mirror
### [Population: ONE](https://www.populationonevr.com/)
[![Population: ONE](https://github.com/MirrorNetworking/Mirror/assets/16416509/dddc778b-a97f-452d-b5f8-6ec42c6da4f1)](https://www.populationonevr.com/)
The [BigBoxVR](https://www.bigboxvr.com/) team started using Mirror in February 2019 for what eventually became one of the most popular Oculus Rift games.

In addition to [24/7 support](https://discordapp.com/invite/xVW4nU4C34) from the Mirror team, BigBoxVR also hired one of our engineers.

**Population: ONE** was [acquired by Meta](https://uploadvr.com/population-one-facebook-bigbox-acquire/) in June 2021, and they've just released a new [Sandbox](https://www.youtube.com/watch?v=jcI0h8dn9tA) addon in 2022!

### [Zooba](https://play.google.com/store/apps/details?id=com.wildlife.games.battle.royale.free.zooba&gl=US)
[![Zooba](https://user-images.githubusercontent.com/16416509/178141846-60805ad5-5a6e-4840-8744-5194756c2a6d.jpg)](https://play.google.com/store/apps/details?id=com.wildlife.games.battle.royale.free.zooba&gl=US)
[Wildlife Studio's](https://wildlifestudios.com/) hit Zooba made it to rank #5 of the largest battle royal shooters in the U.S. mobile market.

The game has over **100 million** downloads on [Google Play](https://play.google.com/store/apps/details?id=com.wildlife.games.battle.royale.free.zooba&gl=US), with Wildlife Studios as one of the top 10 largest mobile gaming companies in the world.

### [Swarm VR](https://www.swarmvrgame.com/)
[![swarmvr_compressed](https://user-images.githubusercontent.com/16416509/222610677-fa38f173-f76b-422f-b39d-8e0ef0cee798.jpg)](https://www.swarmvrgame.com/)
SPIDER-MAN WITH GUNS! 

SWARM is a fast-paced, arcade-style grapple shooter, with quick sessions, bright colorful worlds and globally competitive leaderboards that will take you back to the glory days of Arcade Games.

Available for the [Meta Quest](https://www.oculus.com/experiences/quest/2236053486488156/), made with Mirror.

### [Liars Bar](https://store.steampowered.com/app/3097560/Liars_Bar/)
[![liarsbar](https://github.com/user-attachments/assets/9100563e-2d9f-44f6-b8c2-332f718b8190)](https://store.steampowered.com/app/3097560/Liars_Bar/)<br/>
With over 20.000 Overwhelmingly Positive reviews on Steam, Liars Bar is one of our largest showcase games of 2024.<br/>
<br/>
This isn't your average pub – it's a den of lies, deception, and mind games. Grab a seat at a table of four and immerse yourself in the ultimate first-person multiplayer online experience where cunning and trickery are the name of the game.

### [Castaways](https://www.castaways.com/)
[![Castaways](https://user-images.githubusercontent.com/16416509/207313082-e6b95590-80c6-4685-b0d1-f1c39c236316.png)](https://www.castaways.com/)
[Castaways](https://www.castaways.com/) is a sandbox game where you are castaway to a small remote island where you must work with others to survive and build a thriving new civilization. 

Castaway runs in the Browser, thanks to Mirror's WebGL support.

### [Nimoyd](https://www.nimoyd.com/)
[![nimoyd_smaller](https://user-images.githubusercontent.com/16416509/178142672-340bac2c-628a-4610-bbf1-8f718cb5b033.jpg)](https://www.nimoyd.com/)
Nudge Nudge Games' first title: the colorful, post-apocalyptic open world sandbox game [Nimoyd](https://store.steampowered.com/app/1313210/Nimoyd__Survival_Sandbox/) is being developed with Mirror.

_Soon to be released for PC & mobile!_

### [Project Z](https://www.projektzgame.com/)
[![projectz](https://github.com/user-attachments/assets/50423fa6-982e-41ed-8a43-4823bf111818)](https://www.projektzgame.com/)
Projekt Z is a first-person coop survival shooter set in a WW2 Zombie scenario on a secret German island. The game focuses on the threat of "Projekt Z", a clandestine program run by the Nazis to turn Zombies, which have been discovered on the island earlier, into weapons to help turn the tide of the war in Nazi Germany's favor.<br/>
<br/>
_Soon to be released!_

### [Unleashed](https://www.unleashedgames.io/)
[![unleashed](https://github.com/MirrorNetworking/Mirror/assets/16416509/ef3bcf74-8fa9-4d22-801d-4d29cb59a013)](https://www.unleashedgames.io/)
From original devs of **World of Warcraft**, **Kingdoms of Amalur**, and **EverQuest** comes a new family friendly fantasy adventure. Fight against the forces of darkness, explore a world consumed by wild magic, and build a stronghold with your friends to increase your power in a new world.

_Lead by industry veterans Brian Birmingham & Irena Pereira, Unleashed is developing their next gen adventure game made with Mirror!_

Follow them on X: https://twitter.com/UnleashingGames/

### [Dinkum](https://store.steampowered.com/app/1062520/Dinkum/)
[![dinkum](https://user-images.githubusercontent.com/16416509/180051810-50c9ebfd-973b-4f2f-8448-d599443d9ce3.jpg)](https://store.steampowered.com/app/1062520/Dinkum/)
Set in the Australian Outback, Dinkum is a relaxing farming & survival game. Made by just one developer, Dinkum already reached 1000+ "Overwhelmingly Positive" reviews 1 week after its early access release. 

James Bendon initially made the game with UNET, and then [switched to Mirror](https://www.playdinkum.com/blog/2019/1/11/devlog-13-biomes-and-traps) in 2019.

### [A Glimpse of Luna](https://www.glimpse-luna.com/)
[![a glimpse of luna](https://user-images.githubusercontent.com/16416509/178148229-5b619655-055a-4583-a1d3-18455bde631f.jpg)](https://www.glimpse-luna.com/)
[A Glimpse of Luna](https://www.glimpse-luna.com/) - a tactical multiplayer card battle game with the most beautiful concept art & soundtrack.

Made with Mirror by two brothers with [no prior game development](https://www.youtube.com/watch?v=5J2wj8l4pFA&start=12) experience.

### [Havoc](https://store.steampowered.com/app/2149290/Havoc/)
![havoc fps game](https://github.com/MirrorNetworking/Mirror/assets/16416509/f3549a95-5663-41f8-9868-283b3a0fcf63)
Havoc is a tactical team-based first-person shooter with a fully destructible environment and a unique art style. Havoc has been one of our favorite made-with-Mirror games for a few years now, and we are excited to finally see it up there on Steam.

### [Sun Haven](https://store.steampowered.com/app/1432860/Sun_Haven/)
[![sun haven](https://user-images.githubusercontent.com/16416509/185836661-2bfd6cd0-523a-4af4-bac7-c202ed01de7d.jpg)](https://store.steampowered.com/app/1432860/Sun_Haven/)
[Sun Haven](https://store.steampowered.com/app/1432860/Sun_Haven/) - A beautiful human town, a hidden elven village, and a monster city filled with farming, magic, dragons, and adventure.

After their successful [Kickstarter](https://www.kickstarter.com/projects/sunhaven/sunhaven/description), Sun Haven was released on Steam in 2021 and later on ported to Mirror in 2022.

### [A Township Tale](https://townshiptale.com/)
[![A Township Tale](https://user-images.githubusercontent.com/16416509/212850393-1abdce51-1abe-4745-8a7d-67e9ebae96a7.png)](https://townshiptale.com/)
A Township Tale is an immersive VR experience, where you can build towns and explore worlds with your friends.

Made with our KCP transport, available on the [Meta Quest Store](https://www.oculus.com/experiences/quest/2913958855307200/) with over 6000+ ratings.

### [Inferna](https://inferna.net/)
[![Inferna MMORPG](https://user-images.githubusercontent.com/16416509/178148768-5ba9ea5b-bcf1-4ace-ad7e-591f2185cbd5.jpg)](https://inferna.net/)
One of the first MMORPGs made with Mirror, released in 2019.

An open world experience with over 1000 CCU during its peak, spread across multiple server instances.

### [Samutale](https://www.samutale.com/)
[![samutale](https://user-images.githubusercontent.com/16416509/178149040-b54e0fa1-3c41-4925-8428-efd0526f8d44.jpg)](https://www.samutale.com/)
A sandbox survival samurai MMORPG, originally released in September 2016.

Later on, the Netherlands based Maple Media switched their netcode to Mirror.

### [Another Dungeon](https://www.gameduo.net/en/game/ad)
![image](https://github.com/MirrorNetworking/Mirror/assets/16416509/9b47438c-e664-47aa-996e-d1701b0a2efd)
Pixel Art Dungeon MMORPG reaching 5000 CCU at peak times.

Originally developed as a single-player idle game, it underwent a transition to an MMORPG three months before release thanks to Mirror!

### [Untamed Isles](https://store.steampowered.com/app/1823300/Untamed_Isles/)
[![Untamed Isles](https://user-images.githubusercontent.com/16416509/178143679-1c325b54-0938-4e84-97b6-b59db62a51e7.jpg)](https://store.steampowered.com/app/1823300/Untamed_Isles/)
The turn based, monster taming **MMORPG** [Untamed Isles](https://store.steampowered.com/app/1823300/Untamed_Isles/) is currently being developed by [Phat Loot Studios](https://untamedisles.com/about/).

After their successful [Kickstarter](https://www.kickstarter.com/projects/untamedisles/untamed-isles), the New Zealand based studio is aiming for a 2022 release date.

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

### [Overpowered](https://overpoweredcardgame.com/)
[![Overpowered](https://github.com/MirrorNetworking/Mirror/assets/16416509/5bdbb227-970d-434e-b062-94fde1297f7c)](https://overpoweredcardgame.com/)
[Overwpowered](https://overpoweredcardgame.com/), the exciting new card game that combines strategy, myth, and fun into one riveting web-based experience. Launched in 2023, made with Mirror!

### And many more...
<a href="https://store.steampowered.com/app/1797130/Plunder_Scourge_of_the_Sea/"><img src="https://cdn.akamai.steamstatic.com/steam/apps/1797130/header.jpg?t=1698422797" height="100" title="Plunder: Scourge of the Sea Pirate MMO"/></a>
<a href="https://store.steampowered.com/app/719200/The_Wall/"><img src="https://cdn.akamai.steamstatic.com/steam/apps/719200/header.jpg?t=1588105839" height="100" title="The wall"/></a>
<a href="https://store.steampowered.com/app/535630/One_More_Night/"><img src="https://cdn.akamai.steamstatic.com/steam/apps/535630/header.jpg?t=1584831320" height="100" title="One more night"/></a>
<img src="https://i.ytimg.com/vi/D_f_MntrLVE/maxresdefault.jpg" height="100" title="Block story"/>
<a href="https://nightz.io"><img src="https://user-images.githubusercontent.com/16416509/130729336-9c4e95d9-69bc-4410-b894-b2677159a472.jpg" height="100" title="Nightz.io"/></a>
<a href="https://store.steampowered.com/app/1016030/Wawa_United/"><img src="https://user-images.githubusercontent.com/16416509/162982300-c29d89bc-210a-43ef-8cce-6e5555bb09bc.png" height="100" title="Wawa united"/></a>
<a href="https://store.steampowered.com/app/1745640/MACE_Mapinguaris_Temple/"><img src="https://user-images.githubusercontent.com/16416509/166089837-bbecf190-0f06-4c88-910d-1ce87e2f171d.png" title="MACE" height="100"/></a>
<a href="https://www.adversator.com/"><img src="https://user-images.githubusercontent.com/16416509/178641128-37dc270c-bedf-4891-8284-33573d1776b9.jpg" title="Adversator" height="100"/></a>
<a href="https://store.steampowered.com/app/670260/Solace_Crafting/"><img src="https://user-images.githubusercontent.com/16416509/197175819-1c2720b6-97e6-4844-80b5-2197a7f22839.png" title="Solace Crafting" height="100"/></a>
<a href="https://www.unitystation.org"><img src="https://user-images.githubusercontent.com/57072365/204021428-0c621067-d580-4c88-b551-3ac70f9da39d.jpg" title="UnityStation" height="100"/></a>
<a href="https://store.steampowered.com/app/1970020/__Touhou_Fairy_Knockout__One_fairy_to_rule_them_all/"><img src="https://github.com/MirrorNetworking/Mirror/assets/16416509/dc1286a8-b619-4f68-9dfe-6a501be7e233" title="Touhou Fairy Knockout" height="100"/></a>
<a href="https://store.steampowered.com/app/2168680/Nuclear_Option/"><img src="https://github.com/MirrorNetworking/Mirror/assets/16416509/4e98520e-9bde-4305-8b02-bada090a02dd" title="Nuclear Option" height="100"/></a>
<a href="https://store.steampowered.com/app/2499940/Shattered_Lands/"><img src="https://github.com/MirrorNetworking/Mirror/assets/57072365/52930403-c1d1-4c27-9477-e03215acbda5" title="Shattered Lands" height="100"/></a>
<a href="https://store.steampowered.com/app/1955340/Super_Raft_Boat_Together"><img src="https://github.com/MirrorNetworking/Mirror/assets/57072365/0d30b84a-0b2b-4790-8687-d95e2fa23df1" title="Super Raft Boat Together" height="100"/></a>
<a href="https://store.steampowered.com/app/2585860/Ruins_To_Fortress/"><img src="https://github.com/MirrorNetworking/Mirror/assets/16416509/258ac5cf-d359-46cd-8af4-c7c1844dba9c" title="Ruins to Fortress" height="100"/></a>
<a href="https://store.steampowered.com/app/2967080/Block_Trucks_Multiplayer_Racing/"><img src="https://github.com/user-attachments/assets/120794c6-81c2-445c-8f9b-b2be2bada376" title="Block Trucks" height="100"/></a>
<a href="https://nebula-dev.itch.io/drunkonauts"><img src="https://github.com/user-attachments/assets/fef572da-dfd4-49af-8062-c072793a6a26" title="Drunkonauts" height="100"/></a>


## Modular Transports
Mirror uses **KCP** (reliable UDP) by default, but you may use any of our community transports for low level packet sending:
* (built in) [KCP](https://github.com/MirrorNetworking/kcp2k): reliable UDP
* (built in) [Telepathy](https://github.com/MirrorNetworking/Telepathy): TCP
* (built in) [Websockets](https://github.com/MirrorNetworking/SimpleWebTransport): Websockets
* [Ignorance](https://github.com/SoftwareGuy/Ignorance/): ENET UDP
* [LiteNetLib](https://github.com/MirrorNetworking/LiteNetLibTransport/) UDP
* [FizzySteam](https://github.com/Chykary/FizzySteamworks/): SteamNetwork
* [FizzyFacepunch](https://github.com/Chykary/FizzyFacepunch/): SteamNetwork
* [Epic Relay](https://github.com/WeLoveJesusChrist/EOSTransport): Epic Online Services
* [Bubble](https://github.com/Squaresweets/BubbleTransport): Apple GameCenter
* [Light Reflective Mirror](https://github.com/Derek-R-S/Light-Reflective-Mirror): Self-Hosted Relay

## Benchmarks
* [2022] mischa [400-800 CCU](https://discord.com/channels/343440455738064897/1007519701603205150/1019879180592238603) tests
* [2021] [Jesus' Benchmarks](https://docs.google.com/document/d/1GMxcWAz3ePt3RioK8k4erpVSpujMkYje4scOuPwM8Ug/edit?usp=sharing)
* [2019] [uMMORPG 480 CCU](https://youtu.be/mDCNff1S9ZU) (worst case)

## Development & Contributing
Mirror is used **in production** by everything from small indie projects to million dollar funded games that will run for a decade or more.

We prefer to work slow & thoroughly in order to not break everyone's games 🐌.

Therefore, we need to [KISS](https://en.wikipedia.org/wiki/KISS_principle) 😗.

---
# Information Security
![Mirror alternative Logo](https://github.com/MirrorNetworking/Mirror/assets/16416509/ca26e97c-2f26-487d-a48e-e23ec762bc79)

**Mirror-Networking** follows common information security industry standards & best practices.

Mirror is free open source software (**MIT Licensed**), with over 80% test coverage. The company is located in Germany. We do not collect any user data, impose no restrictions on users & developers, or rely on any closed source dependencies other than Unity.

This makes Mirror an attractive choice for government agencies and large corporations with strict information security requirements.

Feel free to reach out to business [**at**] mirror-networking.com if you have any questions, or need to review any of our policies:

* **Development best Practices and SDLC**.pdf
* **Disaster Recovery Procedure**.pdf
* **Document Retention and Destruction Policy**.pdf
* **Encryption Policy**.pdf
* **Information Security Guidelines**.pdf
* **[Privacy Policy](https://mirror-networking.com/privacy-policy/)**
* **[Security Policy](https://github.com/MirrorNetworking/Mirror/blob/master/SECURITY.md)**
* **Vulnerability Management Policy**.pdf

Please reach out if you decide to use Mirror.

We are excited to hear about your project, and happy to help if needed!

---
# Incident Response & Bug Bounty
A lot of projects use Mirror in production. If you found a critical bug / exploit in Mirror core, please follow the steps outlined in our [Security Policy](SECURITY.md).

**Credits / past findings / fixes:**
* 2020, fholm: fuzzing ConnectMessage to stop further connects [[#2397](https://github.com/vis2k/Mirror/pull/2397)]
* 2023-04-05: IncludeSec: [kcp2k UDP spoofing](http://blog.includesecurity.com/?p=1407) [[#3286](https://github.com/vis2k/Mirror/pull/3286)]
* 2023-06-27: James Frowen: ClientToServer [SyncVar] [allocation attacks](https://github.com/MirrorNetworking/Mirror/pull/3562)

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


