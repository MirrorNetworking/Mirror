<<<<<<< refs/remotes/vis2k/mirror
![Mirror Logo](https://i.imgur.com/5dUNWxl.png)

[![Build status](https://img.shields.io/appveyor/ci/vis2k73562/hlapi-community-edition/Improvements.svg)](https://ci.appveyor.com/project/vis2k73562/hlapi-community-edition/branch/improvements)
[![AppVeyor tests branch](https://img.shields.io/appveyor/tests/vis2k73562/hlapi-community-edition/Improvements.svg)](https://ci.appveyor.com/project/vis2k73562/hlapi-community-edition/branch/improvements/tests)
[![Discord](https://img.shields.io/discord/343440455738064897.svg)](https://discordapp.com/invite/N9QVxbM)
[![Codecov](https://codecov.io/gh/vis2k/hlapi-community-edition/branch/improvements/graph/badge.svg)](https://codecov.io/gh/vis2k/hlapi-community-edition/branch/improvements)

Mirror is a **high level** Networking API for Unity, built on top of the **low level** [Telepathy](https://github.com/vis2k/Telepathy) library.

Mirror is built [and tested](https://docs.google.com/document/d/e/2PACX-1vQqf_iqOLlBRTUqqyor_OUx_rHlYx-SYvZWMvHGuLIuRuxJ-qX3s8JzrrBB5vxDdGfl-HhYZW3g5lLW/pub#h.h4wha2mpetsc) for **MMO Scale** Networking by the developers of [uMMORPG](https://www.assetstore.unity3d.com/#!/content/51212), [uSurvival](https://www.assetstore.unity3d.com/#!/content/95015) and [Cubica](https://cubica.net).

Mirror is optimized for **ease of use** and **probability of success**. Projects that use Mirror are small, concise and maintainable. uMMORPG was possible with <6000 lines of code. We needed a networking library that allows us to [launch our games](https://ummorpg.net/showcase/), period.

With Mirror, the **Server & Client are ONE** project _(hence the name)_. Instead of having one code base for the server and one for the client, we simply use the same code for both of them.
* `[Server]` / `[Client]` tags can be used for the server-only and client-only parts.
* `[Command]` are used for Client->Server, and `[ClientRpc]` / `[TargetRpc]` for Server->Client communication.
* `[SyncVar]`s and `SyncList`s are used to automatically synchronize state.

What previously required **10.000** lines of code, now takes **1.000** lines of code. Therein lies the **magic of Mirror**.

_Note: Mirror is based on Unity's abandoned UNET Networking system. We fixed it up and pushed it to MMO Scale._

# Documentation
We are still working on the documentation, but Mirror is still similar enough to UNET that you can use the [UNET Manual](https://docs.unity3d.com/Manual/UNet.html) for now.

The only difference is that you have to use `using Mirror;` instead of `using UnityEngine.Networking;` at the top of your scripts.

_Oh, and you won't have to worry about channels, low level networking, [packet loss](https://forum.unity.com/threads/unet-deprecation-thread.543501/page-3#post-3597869), [lack of support](https://forum.unity.com/threads/is-hlapi-dead.517436/) or [bugs](https://issuetracker.unity3d.com/issues/unet-networkwriter-dot-write-causing-readstring-slash-readbytes-out-of-range-errors-in-clients) ever again. Mirror just works._

# Usage Guide
Mirror will be on the Unity Asset Store soon, which will make it as easy to use as it gets.

Until then:

1. [Download Mirror](http://noobtuts.com/_projects/Mirror/Mirror.zip) (for Unity 2017.4 and 2018). Use it at your own risk!
2. Drop the DLLs into your Project's Plugins folder
3. Select Runtime/Mirror.Runtime.dll and tell Unity to **Exclude** the Editor platform
4. Select Runtime-Editor/Mirror.Runtime.dll and tell Unity to **only Include** the Editor platform

# Migration Guide
If you are still using UNET and want to switch to Mirror, you should check out our [Migration Guide](https://github.com/vis2k/Mirror/blob/mirror/migration.md). Don't panic, it's very easy and won't take more than 5 minutes.

# Example Projects
We are building several easy to understand example projects that we will add here soon.

For a fully polished complete project example, consider [uMMORPG](https://www.assetstore.unity3d.com/#!/content/51212) or [uSurvival](https://www.assetstore.unity3d.com/#!/content/95015).

# Donations
Mirror is developed by volunteers. If you like what we are doing, consider leaving [a small donation](https://www.patreon.com/HLAPI_PRO).

# Build
Building Mirror yourself is very easy. Simply download the project, open it in Visual Studio or Rider, build it once for Release and once for Release-Editor. You will then find all the necessary DLLs in the Output directory.

# Benchmarks
* Telepathy [1000 connections](https://github.com/vis2k/Telepathy) test
* [uMMORPG 207 CCU worst case test](https://docs.google.com/document/d/e/2PACX-1vQqf_iqOLlBRTUqqyor_OUx_rHlYx-SYvZWMvHGuLIuRuxJ-qX3s8JzrrBB5vxDdGfl-HhYZW3g5lLW/pub#h.h4wha2mpetsc) (everyone broadcasting to everyone else)
* [uSurvival 122 CCU worst case test](https://docs.google.com/document/d/e/2PACX-1vT28FcGXYlbG8gwi8DhD914n7K-wCAE8qhfetPkSli96ikc1Td3zJO1IiwVhfPVtKUHF0l3N7ZkM5GU/pub#h.pwbvffnwcewe)

# Contributing
If you like to contribute, feel free to submit pull requests and visit our [Discord Server](https://discordapp.com/invite/N9QVxbM).

We follow the [KISS](https://en.wikipedia.org/wiki/KISS_principle) principle, so make sure that your Pull Requests contain no magic.

We need Mirror to be MMO Scale. Bug fixes are always highly appreciated. New features will be considered very carefully.
=======
# HLAPI Community Edition

[![Build status](https://img.shields.io/appveyor/ci/vis2k/hlapi-community-edition.svg)](https://ci.appveyor.com/project/vis2k/hlapi-community-edition/branch/master)
[![AppVeyor tests branch](https://img.shields.io/appveyor/tests/vis2k/hlapi-community-edition.svg)](https://ci.appveyor.com/project/vis2k/hlapi-community-edition/build/tests)
[![Discord](https://img.shields.io/discord/343440455738064897.svg)](https://discord.gg/wvesC6)

Unity is the best game engine in the world, which should make UNET the best multiplayer game development solution in the world, but it's not.

UNET consists of two parts:

* The LLAPI is developed by [@aabramychev](https://forum.unity.com/members/aabramychev.118911/) and deserves more credit than it gets. It's nothing short of amazing. We all love your work Alex!
* The HLAPI was developed by [Sean Riley](https://forum.unity.com/members/691722/) as an example to showcase the LLAPI. Sean Riley left Unity and everyone's hopes for HLAPI improvements remained mostly (not entirely) unanswered.

UNET's HLAPI was abandoned a long time ago when Sean Riley left unity.

UNET's HLAPI was made [open source](https://bitbucket.org/Unity-Technologies/networking) shortly after.

This project is a fork of HLAPI and picks up where HLAPI stopped. 
A lot of bugs have been fixed while maintaining compatibility.
We have refactored to improve code quality and reduce complexity.
We are also adding new features useful for developers.

# Download:

* [HLAPI Community Edition 2017.1](http://noobtuts.com/_projects/HLAPI-Pro/HLAPI_Pro_fixesonly_Unity_2017.1.zip)
* [HLAPI Community Edition 2017.2](http://noobtuts.com/_projects/HLAPI-Pro/HLAPI_Pro_fixesonly_Unity_2017.2.zip)
* [HLAPI Community Edition 2017.3](http://noobtuts.com/_projects/HLAPI-Pro/HLAPI_Pro_fixesonly_Unity_2017.3.zip)
* [HLAPI Community Edition 2017.4](http://noobtuts.com/_projects/HLAPI-Pro/HLAPI_Pro_Unity_2017.4.zip)
* [HLAPI Community Edition 2017.4_improvements](http://noobtuts.com/_projects/HLAPI-Pro/HLAPI_Pro_Unity_2017.4_improvements.zip) <- **Recommended**
* [HLAPI Community Edition 2018.1](http://noobtuts.com/_projects/HLAPI-Pro/HLAPI_Pro_Unity_2018.1.zip)


Use at your own risk. In case of concerns, feel free to inspect the .DLL files with ILSpy!

# Build:

To build this project,  clone the repository,  open Networking.sln it in visual studio 2017 and build all projects.

# Installation:
Backup the original DLL files from your Unity installation folder and replace them with the HLAPI Community Edition DLL files:

## Windows:
1. *Backup your Project! Be wise now and don't lose everything if something unexpected happens.*
2. replace C:\Program Files\Unity\Editor\Data\UnityExtensions\Unity\Networking content with the downloaded files.
3. move the Unity.UNetWeaver.dll to C:\Program Files\Unity\Editor\Data\Managed
4. Restart Unity for the UNetWeaver.dll to be reloaded properly. Rebuild all your clients/servers so they use the same DLLs.
5. Rebuild your server/client.exe so that it uses the DLL as too.
## Mac:
1. *Backup your Project! Be wise now and don't lose everything if something unexpected happens.*
2. replace Unity.app/Contents/UnityExtensions/Unity/Networking/ content with the downloaded files
3. move the Unity.UNetWeaver.dll to Unity.app/Contents/Managed/
4. Restart Unity for the UNetWeaver.dll to be reloaded properly. Rebuild all your clients/servers so they use the same DLLs.
5. Rebuild your server/client.app so that it uses the DLL as too.

Note: right click Unity.app and select 'Show Package Contents' to see the subfolders.
## Linux:
You'll find it  
(feel free to submit a pull request for filling this part)

# Branches:

We have multiple yet sometimes conflicting goals. Thus we are developing HLAPI in several branches:

* master: 2017.4 HLAPI + bug fixes. No unnecessary code changes to guarantee 100% compatibility with original HLAPI, for those who need it.
* 2018.1: 2018.1 patch. Can be rebased to latest 'master' all the time.
* improvements: the #1 goal of this branch is to make HLAPI more simple and easier to maintain. The original code is way too complicated and if we end up with 10.000 lines instead of 20.000 lines, then that would be huge. The #2 goal of this branch is to improve CCU and only add features that are completely obviously necessary (SyncVarToOwner etc.).
* features: this branch is for new features that could be useful. We can go crazy with features here, as long as we all agree that a given feature is a good idea to add. We can discuss features in Discord.


If you submit pull requests, please submit them to the proper branch. 
For example, 99% of the features submitted to 'improvements' will most likely be rejected, 
because the goal is to make this branch more simple. 
Submit new features to 'features' branch instead. 
If you want to submit a bug fix that applies to everything, then submit it to 'master', and so on.
>>>>>>> Document the project in the readme file

