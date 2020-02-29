![Mirror Logo](https://i.imgur.com/ikP9eYs.png)

[![Documentation](https://img.shields.io/badge/documentation-brightgreen.svg)](https://mirrorng.github.io/MirrorNG/)
[![Video Tutorial](https://img.shields.io/badge/video_tutorial-brightgreen.svg)](https://www.youtube.com/playlist?list=PLkx8oFug638oBYF5EOwsSS-gOVBXj1dkP)
[![Forum](https://img.shields.io/badge/forum-brightgreen.svg)](https://forum.unity.com/threads/mirror-networking-for-unity-aka-hlapi-community-edition.425437/)
[![Discord](https://img.shields.io/discord/343440455738064897.svg)](https://discordapp.com/invite/N9QVxbM)
[![release](https://img.shields.io/github/release/MirrorNG/MirrorNG.svg)](https://github.com/MirrorNG/MirrorNG/releases/latest)
[![openupm](https://img.shields.io/npm/v/com.mirrorng.mirrorng?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.mirrorng.mirrorng/)

[![Build](https://github.com/MirrorNG/MirrorNG/workflows/CI/badge.svg)](https://github.com/MirrorNG/MirrorNG/actions?query=workflow%3ACI)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_MirrorNG&metric=alert_status)](https://sonarcloud.io/dashboard?id=MirrorNG_MirrorNG)
[![SonarCloud Coverage](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_MirrorNG&metric=coverage)](https://sonarcloud.io/component_measures?id=MirrorNG_MirrorNG&metric=coverage)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_MirrorNG&metric=ncloc)](https://sonarcloud.io/dashboard?id=MirrorNG_MirrorNG)
[![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_MirrorNG&metric=sqale_index)](https://sonarcloud.io/dashboard?id=MirrorNG_MirrorNG)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_MirrorNG&metric=code_smells)](https://sonarcloud.io/dashboard?id=MirrorNG_MirrorNG)

Mirror is a **high level** Networking API for Unity, built on top of the **low level** [Telepathy](https://github.com/vis2k/Telepathy) library.

Mirror is built [and tested](https://www.youtube.com/watch?v=mDCNff1S9ZU) for **MMO Scale** Networking by the developers of [uMMORPG](https://assetstore.unity.com/packages/templates/systems/ummorpg-51212), [uSurvival](https://assetstore.unity.com/packages/templates/systems/usurvival-95015) and [Cubica](https://cubica.net).

Mirror is optimized for **ease of use** and **probability of success**. Projects that use Mirror are small, concise and maintainable. uMMORPG was possible with <6000 lines of code. We needed a networking library that allows us to [launch our games](https://mirror-networking.com/showcase/), period.

With Mirror, the **Server & Client are ONE** project _(hence the name)_. Instead of having one code base for the server and one for the client, we simply use the same code for both of them.
* `[Server]` / `[Client]` tags can be used for the server-only and client-only parts.
* `[Command]` are used for Client->Server, and `[ClientRpc]` / `[TargetRpc]` for Server->Client communication.
* `[SyncVar]`s and `SyncList`s are used to automatically synchronize state.

What previously required **10.000** lines of code, now takes **1.000** lines of code. Therein lies the **magic of Mirror**.

_Note: Mirror is based on Unity's abandoned UNET Networking system. We fixed it up and pushed it to MMO Scale._

## Why fork Mirror?
I have worked on Mirror for over a year, I am the one that came up with the name and the second contributor. It has served me well and there are some really smart people working on it.

However, the project is not moving forward as fast as I would like. There is a big emphasis on keeping backwards compatiblity, which is really good for many users, but it is seriously slowing me down.

Mirror relies heavily on manual testing.  Manual testing does not scale. I can cover so much more code with automated tests, and have much more confidence on my changes. This will require large breaking changes that will be hard to swallow for many people,  but at the end of the day I should be able to reduce the amount of defects significantly.

Mirror makes heavy use of singletons, which is considered an [anti-pattern](https://www.dotnetcurry.com/patterns-practices/1350/singleton-design-anti-pattern-csharp). Singletons are especially problematic in Unity 2019.3.  A lot of people will disable domain reloading which completely breaks singletons.

Mirror has very poor error handling. Many methods return true/false to indicate success/failure and use Debug.LogError to report errors.  Since day 1, C# has had a much better mechanism to handle abnormal conditions: Exceptions. Methods in MirrorNG should either succeed throw exceptions if something is wrong. This lets the developer catch the exception with a full explanation of what is wrong and take action:  display an error message to the user, report it to a telemetry system, etc.

I want to adhere as much as possible to the [SOLID principles](https://en.wikipedia.org/wiki/SOLID). Many things in Mirror do not.

Mirror has it's own code conventions based on one person's preference.  I would rather follow official [C# code conventions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions).  Anybody that speaks C# should feel right at home with this code.

Code review takes too long in Mirror.  I think code reviews are top priority.

## Changelog

These are some notable differences between Mirror and MirrorNG:
* [Done] CI/CD pipeline.  Tests are executed with every pull request, quality is *enforced*
* [Done] Unity package manager.  Makes it easier to install and upgrade MirrorNG in your project
* [WIP] Code quality,  the goal is to turn this [![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_MirrorNG&metric=alert_status)](https://sonarcloud.io/dashboard?id=MirrorNG_MirrorNG) green. Help welcome. This will have a great impact on reducing the amount of defects.
* [WIP] Make it work in unity 2019.3.0 with domain reload turned off 
* [WIP] Allow connection to multiple servers simultaneously

## Documentation
Check out our [Documentation](https://mirrorng.github.io/MirrorNG/).

If you are migrating from UNET, then please check out our [Migration Guide](https://mirrorng.github.io/MirrorNG//General/Migration.html). Don't panic, it's very easy and won't take more than 5 minutes.

## Installation
The preferred installation method is Unity Package manager.

If you are using unity 2019.3 or later: 

1) Open your project in unity
2) Click on Windows -> Package Manager
3) Click on the plus sign on the left and click on "Add package from git URL..."
4) enter https://github.com/MirrorNG/MirrorNG.git#upm
5) Unity will download and install MirrorNG

If you are using unity 2019.2, you can use [openupm](https://openupm.com/packages/com.mirrorng.mirrorng/) or you can manually add the url to your [packages.json](https://docs.unity3d.com/Manual/upm-git.html) file. 

Alternatively you can download it from [Download Mirror](https://github.com/MirrorNG/MirrorNG/releases) 

## Examples
We included several small example projects.

## Transports
MirrorNG supports many different low level networking transports:

* (built in) https://github.com/vis2k/Telepathy (Telepathy)
* (built in) Unity's LLAPI
* (built in) https://github.com/ninjasource/Ninja.WebSockets (Websockets)

## The MirrorNG Mantra
So many quotes to chose from.  This one in particular really encapsulates why this exists:

> _“All code is guilty, until proven innocent.” – Anonymous

I assume every line of code I write is broken in some random obscure corner case. The only way to ensure it's quality is by testing. I don't have time to test my software,  I would rather the machine tested it for me while I work on something else. So the big theme of MirrorNG is to improve on automated testing. 

## Contributing

There are several ways to contribute to this project:

* Pull requests for bug fixes and features are always appreciated.
* Pull requests to improve the documentation is also welcome
* Make tutorials on how to use this
* Test it and open issues
* Review existing pull requests
* Donations

When contributing code, please keep these things in mind:

* [KISS](https://en.wikipedia.org/wiki/KISS_principle) principle. Everything needs to be **as simple as possible**. 
* An API is like a joke,  if you have to explain it is not a good one.  Do not require people to read the documentation if you can avoid it.
* Follow [C# code conventions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions).

Pull Requests for bug fixes are always highly appreciated. New features will be considered very carefully and will only be merged if they are the most simple solution to the given problem.
